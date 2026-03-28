using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using My.Scripts.Global;
using Unity.Collections;
using Wonjeong.Utils;
using Debug = UnityEngine.Debug;

namespace My.Scripts.Timelapse
{
    /// <summary>
    /// 웹캠 프레임을 비동기로 캡처하고 FFMPEG를 활용해 타임랩스 및 리얼타임 비디오로 인코딩함.
    /// </summary>
    public class TimeLapseRecorder : MonoBehaviour
    {
        public static TimeLapseRecorder Instance;

        [Header("Capture Settings")]
        private readonly int timelapseCaptureFPS = 5;
        private readonly int realtimeCaptureFPS = 30;

        [Header("Output Settings")]
        private readonly float timelapseDuration = 20f; 
        private readonly float realtimeDuration = 15f;

        private readonly int captureWidth = 1920;
        private readonly int captureHeight = 1080;

        [Header("API Retry Settings")]
        [SerializeField] private int maxRetries = 10;
        [SerializeField] private float retryDelay = 1.0f;

        private WebCamTexture _webCam;
        private bool _isRecording;

        private string _rootPath;
        private string _currentDateFolder;

        private string _sourceImageFolderPath;
        private string _outputVideoFolderPath;
        private string _realtimeSourcePath;
        private string _realtimeVideoPath;

        private int _globalFrameIndex;
        private int _realtimeFrameIndex;

        private float _timer;
        private float _baseInterval;

        private Texture2D _encodeTexture;
        private RenderTexture _captureRT;

        private string _currentLevelID;

        private UniTask _diskWriteTask;

        private struct SaveTaskData
        {
            public string path;
            public byte[] data;
        }

        private readonly ConcurrentQueue<SaveTaskData> _saveQueue = new ConcurrentQueue<SaveTaskData>();
        private CancellationTokenSource _cts;

        private float _realtimeRecordingStartTime;
        private float _realtimeTotalDuration;
        private bool _isRealtimeRecordingActive;

        private float _timelapseTimer;
        private int _activeDiskWrites = 0;

        public bool EnableTimelapseCapture { get; set; } = false;
        public bool EnableRealtimeCapture { get; set; } = false;
        
        public bool IsTimelapseProcessing { get; private set; }
        public bool IsRealtimeProcessing { get; private set; }
        public bool IsUploading { get; private set; } 
        
        public bool IsProcessing => IsTimelapseProcessing || IsRealtimeProcessing || IsUploading;
        public bool IsConverting => IsProcessing;

        public float RealtimeProgress { get; private set; } 

        public bool IsConversionSuccessful { get; private set; }
        public int LastExitCode { get; private set; }

        public string LastVideoPath { get; private set; }
        public string LastRealtimeVideoPath { get; private set; }

        public Color CurrentTint { get; set; } = Color.white;
        private Material _tintMaterial;

        /// <summary>
        /// 싱글톤 초기화 및 저장소 루트 경로를 설정함.
        /// </summary>
        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                string dataPath = Application.dataPath;
                DirectoryInfo parentDir = Directory.GetParent(dataPath);
                _rootPath = (parentDir != null) ? parentDir.FullName : dataPath;

                // ex: Max(30, 5) = 30 -> 1f / 30 = 0.0333f
                _baseInterval = 1f / Mathf.Max(realtimeCaptureFPS, timelapseCaptureFPS);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 디스크 쓰기 전용 백그라운드 스레드를 시작함.
        /// </summary>
        private void Start()
        {
            _diskWriteTask = StartDiskWriteLoop();
        }

        /// <summary>
        /// 영상 변환 시 UI 반영을 위해 진행도를 보간하여 갱신함.
        /// </summary>
        private void Update()
        {
            if (IsRealtimeProcessing && RealtimeProgress < 0.95f)
            {
                RealtimeProgress += Time.deltaTime * 0.1f;
            }
        }

        /// <summary>
        /// 현재 레벨 식별자를 설정하고 리얼타임 녹화 대상인지 검사하여 상태를 전환함.
        /// </summary>
        public void SetCurrentLevel(string levelID)
        {
            _currentLevelID = levelID;
            bool isTarget = IsRealtimeTargetLevel(levelID);

            if (isTarget && !_isRealtimeRecordingActive)
            {
                _isRealtimeRecordingActive = true;
                _realtimeRecordingStartTime = Time.time;
            }
        }

        /// <summary>
        /// 현재 날짜를 기반으로 원본 이미지와 비디오 저장 폴더 경로를 갱신함.
        /// </summary>
        private void UpdatePaths()
        {
            // # TODO: DateTime.Now 문자열 할당은 캐싱하여 하루에 한 번만 갱신하도록 구조 개선 필요
            _currentDateFolder = DateTime.Now.ToString("yyyy-MM-dd");
            _sourceImageFolderPath = Path.Combine(_rootPath, "Timelapse", "Timelapse_Source", _currentDateFolder);
            _outputVideoFolderPath = Path.Combine(_rootPath, "Timelapse", "Timelapse_Video", _currentDateFolder);
            _realtimeSourcePath = Path.Combine(_rootPath, "Timelapse", "Realtime_Source", _currentDateFolder);
            _realtimeVideoPath = Path.Combine(_rootPath, "Timelapse", "Realtime_Video", _currentDateFolder);

            CreateDirectorySafe(_sourceImageFolderPath);
            CreateDirectorySafe(_outputVideoFolderPath);
            CreateDirectorySafe(_realtimeSourcePath);
            CreateDirectorySafe(_realtimeVideoPath);
        }

        private void CreateDirectorySafe(string path)
        {
            try { if (!Directory.Exists(path)) Directory.CreateDirectory(path); }
            catch (Exception e) { Debug.LogError($"폴더 생성 실패: {e.Message}"); }
        }

        private void ClearFolder(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                try 
                { 
                    foreach (string file in Directory.GetFiles(path)) File.Delete(file); 
                }
                catch (Exception e) 
                { 
                    Debug.LogWarning($"폴더 정리 중 오류: {e.Message}"); 
                }
            }
        }

        /// <summary>
        /// 캡처 상태 초기화 및 대기열의 디스크 쓰기가 완료될 때까지 기다린 후 소스 폴더를 정리함.
        /// </summary>
        public void ClearRecordingData()
        {
            ClearRecordingDataAsync().Forget();
        }

        private async UniTaskVoid ClearRecordingDataAsync()
        {
            _globalFrameIndex = 0;
            _realtimeFrameIndex = 0;

            IsTimelapseProcessing = false;
            IsRealtimeProcessing = false;
            IsUploading = false;
            IsConversionSuccessful = false;
            RealtimeProgress = 0f;

            LastVideoPath = string.Empty;
            LastRealtimeVideoPath = string.Empty;
            _realtimeTotalDuration = 0f;
            _isRealtimeRecordingActive = false;
            
            CurrentTint = Color.white;

            while (_saveQueue.TryDequeue(out _)) { }

            await UniTask.WaitUntil(() => _activeDiskWrites == 0);

            ClearFolder(_sourceImageFolderPath);
            ClearFolder(_realtimeSourcePath);
        }

        /// <summary>
        /// 웹캠 피드 수신을 시작하고 캡처에 필요한 텍스처 메모리를 할당함.
        /// </summary>
        public void StartCapture(WebCamTexture cam)
        {
            if (!enabled) return;

            UpdatePaths();

            _webCam = cam;
            _isRecording = true;
            _timer = 0f;
            _timelapseTimer = 0f;

            _baseInterval = 1f / Mathf.Max(realtimeCaptureFPS, timelapseCaptureFPS);

            if (_captureRT) _captureRT.Release();
            _captureRT = new RenderTexture(captureWidth, captureHeight, 0, RenderTextureFormat.ARGB32);

            if (_encodeTexture) Destroy(_encodeTexture);
            _encodeTexture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGBA32, false);

            CaptureLoopRoutine().Forget();
        }

        /// <summary>
        /// 웹캠 캡처를 중단하고 리얼타임 녹화 누적 시간을 기록함.
        /// </summary>
        public void StopCapture()
        {
            _isRecording = false;
            _webCam = null;

            if (_isRealtimeRecordingActive)
            {
                float duration = Time.time - _realtimeRecordingStartTime;
                _realtimeTotalDuration += duration;
                _isRealtimeRecordingActive = false;
            }
        }

        /// <summary>
        /// 메인 스레드 렌더링 파이프라인과 동기화하여 GPU 메모리의 프레임을 비동기 리드백(Readback)함.
        /// </summary>
        private async UniTaskVoid CaptureLoopRoutine()
        {
            RenderTexture captureRT = _captureRT;
            // ex: 1f / 5 = 0.2f
            float timelapseInterval = 1f / timelapseCaptureFPS;

            // Managed-Native 간 비용 우회를 위해 극단적 최적화(object.ReferenceEquals) 적용
            while (_isRecording && !object.ReferenceEquals(_webCam, null) && _webCam.isPlaying)
            {
                try
                {
                    if (!object.ReferenceEquals(_captureRT, captureRT)) break;

                    _timer += Time.deltaTime;
                    _timelapseTimer += Time.deltaTime;

                    if (_timer >= _baseInterval)
                    {
                        _timer -= _baseInterval;

                        bool saveToRealtime = EnableRealtimeCapture && IsRealtimeTargetLevel(_currentLevelID);
                        bool saveToTimelapse = EnableTimelapseCapture && _timelapseTimer >= timelapseInterval;

                        if (!saveToRealtime && !saveToTimelapse)
                        {
                            await UniTask.Yield(PlayerLoopTiming.Update);
                            continue;
                        }

                        if (saveToTimelapse) _timelapseTimer = 0f;

                        await UniTask.WaitForEndOfFrame(this);

                        if (!object.ReferenceEquals(_captureRT, captureRT) || object.ReferenceEquals(_webCam, null) || !_webCam.isPlaying) break;

                        if (CurrentTint == Color.white)
                        {
                            Graphics.Blit(_webCam, captureRT);
                        }
                        else
                        {
                            if (object.ReferenceEquals(_tintMaterial, null))
                            {
                                _tintMaterial = new Material(Shader.Find("Sprites/Default"));
                            }
                            _tintMaterial.color = CurrentTint;
                            Graphics.Blit(_webCam, captureRT, _tintMaterial);
                        }
                        
                        byte[] bytes = null;

                        if (SystemInfo.supportsAsyncGPUReadback)
                        {
                            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(captureRT, 0, TextureFormat.RGBA32);
                            await request.ToUniTask();

                            if (!object.ReferenceEquals(_captureRT, captureRT) || !_isRecording) break;

                            if (!request.hasError)
                            {
                                NativeArray<byte> nativeData = request.GetData<byte>();
                                byte[] rawBytes = nativeData.ToArray(); 
                                
                                try
                                {
                                    bytes = await UniTask.RunOnThreadPool(() =>
                                    {
                                        return ImageConversion.EncodeArrayToJPG(rawBytes, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, (uint)captureWidth, (uint)captureHeight, 0, 70);
                                    });
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning($"스레드 인코딩 실패: {ex.Message}");
                                }
                            }
                        }

                        if (bytes == null)
                        {
                            if (object.ReferenceEquals(_encodeTexture, null)) break;

                            RenderTexture prev = RenderTexture.active;
                            RenderTexture.active = captureRT;
                            _encodeTexture.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
                            _encodeTexture.Apply();
                            RenderTexture.active = prev;

                            bytes = ImageConversion.EncodeToJPG(_encodeTexture, 80);
                        }

                        if (bytes == null || bytes.Length == 0) continue;

                        if (saveToTimelapse)
                        {
                            int globalIndex = Interlocked.Increment(ref _globalFrameIndex) - 1;
                            EnqueueSave(bytes, _sourceImageFolderPath, globalIndex);
                        }

                        if (saveToRealtime)
                        {
                            int realtimeIndex = Interlocked.Increment(ref _realtimeFrameIndex) - 1;
                            EnqueueSave(bytes, _realtimeSourcePath, realtimeIndex);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"캡처 중 프레임 스킵됨: {e.Message}");
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        /// <summary>
        /// 인코딩된 바이트 데이터를 디스크 쓰기 대기열(Queue)에 적재함.
        /// </summary>
        private void EnqueueSave(byte[] data, string folder, int index)
        {
            // # TODO: 루프 내 빈번한 문자열 조합으로 GC 부하 발생 가능. 캐싱 버퍼 도입 고려
            string path = Path.Combine(folder, $"img_{index:D5}.jpg");
            _saveQueue.Enqueue(new SaveTaskData { path = path, data = data });
        }

        /// <summary>
        /// 백그라운드 스레드에서 대기열의 이미지 데이터를 파일 시스템에 순차적으로 기록함.
        /// </summary>
        private async UniTask StartDiskWriteLoop()
        {
            _cts = new CancellationTokenSource();
            await UniTask.SwitchToThreadPool();

            try
            {
                // # TODO: 빈번한 Directory.Exists 호출 최적화 필요
                while (!_cts.IsCancellationRequested)
                {
                    if (_saveQueue.TryDequeue(out SaveTaskData task))
                    {
                        Interlocked.Increment(ref _activeDiskWrites);
                        try 
                        { 
                            string dir = Path.GetDirectoryName(task.path);
                            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                            await File.WriteAllBytesAsync(task.path, task.data); 
                        }
                        catch (Exception e) 
                        { 
                            Debug.LogError($"파일 쓰기 실패: {e.Message}"); 
                        }
                        finally 
                        { 
                            Interlocked.Decrement(ref _activeDiskWrites); 
                        }
                    }
                    else
                    {
                        await UniTask.Delay(50, cancellationToken: _cts.Token);
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// 현재 레벨이 11번부터 15번 사이의 리얼타임 녹화 대상인지 식별함.
        /// </summary>
        private bool IsRealtimeTargetLevel(string levelID)
        {
            if (string.IsNullOrEmpty(levelID)) return false;
            
            // # TODO: 정규식 객체 캐싱을 통해 GC 발생 최소화
            string numPart = Regex.Replace(levelID, "[^0-9]", "");
            
            if (int.TryParse(numPart, out int num))
            {
                if (num >= 11 && num <= 15) return true;
            }
            return false;
        }

        /// <summary>
        /// 저장된 원본 프레임을 FFMPEG를 활용해 타임랩스 비디오로 변환함.
        /// </summary>
        public void ConvertToVideo()
        {
            if (IsTimelapseProcessing) return;
            IsTimelapseProcessing = true;

            float fps = 30f;
            if (_globalFrameIndex > 0 && timelapseDuration > 0)
            {
                // ex: 100 frames / 20.0f = 5 fps
                fps = (float)_globalFrameIndex / timelapseDuration;
            }

            string fileName = $"{GetUserIdString()}_Timelapse";
            Debug.Log($"변환 시작: {_globalFrameIndex}장 / {timelapseDuration}초 목표");

            ConversionSequence(_sourceImageFolderPath, _outputVideoFolderPath, fileName, fps, false).Forget();
        }

        /// <summary>
        /// 저장된 원본 프레임을 FFMPEG를 활용해 리얼타임 비디오로 변환함.
        /// </summary>
        public void ConvertToRealtimeVideo()
        {
            if (IsRealtimeProcessing) return;

            if (_realtimeFrameIndex <= 0)
            {
                if (!IsRealtimeTargetLevel(_currentLevelID)) return;
                Debug.LogWarning("데이터 부족으로 변환 취소.");
                return;
            }

            IsRealtimeProcessing = true;
            RealtimeProgress = 0f; 

            float fps = 30f;
            if (_realtimeFrameIndex > 0 && realtimeDuration > 0)
            {
                fps = (float)_realtimeFrameIndex / realtimeDuration;
            }

            string fileName = $"{GetUserIdString()}_Realtime";
            ConversionSequence(_realtimeSourcePath, _realtimeVideoPath, fileName, fps, true).Forget();
        }

        public void ResetRealtimeProcessing()
        {
            IsRealtimeProcessing = false;
            RealtimeProgress = 0f;
        }

        /// <summary>
        /// FFMPEG 프로세스를 실행하여 이미지 시퀀스를 mp4 포맷으로 인코딩함.
        /// </summary>
        private async UniTaskVoid ConversionSequence(string sourceFolder, string outputFolder, string filePrefix, float fps, bool isRealtime)
        {
            try
            {
                await UniTask.WaitUntil(() => _saveQueue.IsEmpty && _activeDiskWrites == 0);

                if (string.IsNullOrEmpty(sourceFolder) || string.IsNullOrEmpty(outputFolder))
                {
                    UpdatePaths();
                    if (isRealtime) { sourceFolder = _realtimeSourcePath; outputFolder = _realtimeVideoPath; }
                    else { sourceFolder = _sourceImageFolderPath; outputFolder = _outputVideoFolderPath; }
                }

                bool hasSourceFiles = Directory.Exists(sourceFolder) && Directory.GetFiles(sourceFolder, "img_*.jpg").Length > 0;

                string outputFileName = $"{filePrefix}.mp4";
                string outputPath = Path.Combine(outputFolder, outputFileName);

                if (!hasSourceFiles)
                {
                    if (File.Exists(outputPath))
                    {
                        IsConversionSuccessful = true;
                        LastVideoPath = outputPath;
                        
                        if (isRealtime)
                        {
                            LastRealtimeVideoPath = outputPath;
                            RealtimeProgress = 1f;
                        }
                        if (!isRealtime) StartCoroutine(UploadVideoRoutine(outputPath));
                    }
                    else
                    {
                        IsConversionSuccessful = false;
                        if (isRealtime) RealtimeProgress = 0f;
                    }
                    return;
                }

                IsConversionSuccessful = false;
                LastExitCode = -1;

                string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");
                string inputPattern = Path.Combine(sourceFolder, "img_%05d.jpg");

                if (File.Exists(outputPath)) File.Delete(outputPath);
                
                // 설정값이 의도치 않게 누락된 경우를 대비한 방어 로직 대신 경고 출력
                if (fps < 1.0f)
                {
                    Debug.LogWarning($"산출된 프레임레이트({fps})가 너무 낮아 인코딩 오류 방지를 위해 10f로 임시 조정됨.");
                    fps = 10f;
                }

                string fpsStr = fps.ToString("F2", CultureInfo.InvariantCulture);
                string args = $"-framerate {fpsStr} -i \"{inputPattern}\" -c:v libx264 -profile:v baseline -pix_fmt yuv420p -x264-params colorprim=bt709:transfer=bt709:colormatrix=bt709 -color_primaries bt709 -color_trc bt709 -colorspace bt709 -color_range tv \"{outputPath}\"";

                await UniTask.SwitchToThreadPool();

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = ffmpegPath;
                    process.StartInfo.Arguments = args;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();

                    // 최대 60초 대기
                    if (process.WaitForExit(60000))
                    {
                        LastExitCode = process.ExitCode;
                    }
                    else
                    {
                        try { process.Kill(); process.WaitForExit(1000); } catch { }
                        LastExitCode = -1;
                    }
                }

                await UniTask.SwitchToMainThread();

                if (LastExitCode == 0)
                {
                    IsConversionSuccessful = true;
                    LastVideoPath = outputPath;
                    
                    if (isRealtime)
                    {
                        LastRealtimeVideoPath = outputPath;
                        RealtimeProgress = 1f; 
                    }
                    
                    ClearFolder(sourceFolder);
                    if (!isRealtime) StartCoroutine(UploadVideoRoutine(outputPath));
                }
                else
                {
                    if (isRealtime) RealtimeProgress = 0f; 
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"비디오 변환 시퀀스 에러 발생: {e.Message}");
            }
            finally
            {
                if (isRealtime) IsRealtimeProcessing = false;
                else IsTimelapseProcessing = false;
            }
        }

        /// <summary>
        /// 완성된 타임랩스 비디오 파일을 API 서버로 멀티파트(Multipart) 업로드함.
        /// </summary>
        private IEnumerator UploadVideoRoutine(string filePath)
        {
            IsUploading = true;

            if (!File.Exists(filePath)) 
            {
                IsUploading = false;
                yield break;
            }

            int idxUser = 0;
            string uid = "";
            string baseUrl = "";

            if (GameManager.Instance)
            {
                if (SessionManager.Instance)
                {
                    idxUser = SessionManager.Instance.CurrentUserId;
                    uid = SessionManager.Instance.PlayerAUid;
                }
                
                if (GameManager.Instance.ApiConfig != null)
                {
                    baseUrl = GameManager.Instance.ApiConfig.UploadFileUrl;
                }
                else
                {
                    Debug.LogWarning("ApiConfig가 설정되지 않아 업로드 불가.");
                }
            }

            if (string.IsNullOrEmpty(baseUrl)) 
            {
                IsUploading = false;
                yield break;
            }

            string url = $"{baseUrl}?idx_user={idxUser}&uid={uid}&code=A1&type=mp4";

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                using (UnityWebRequest webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    webRequest.uploadHandler = new UploadHandlerFile(filePath);
                    webRequest.uploadHandler.contentType = "video/mp4"; 
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.timeout = 300; 

                    yield return webRequest.SendWebRequest();

                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log($"영상 업로드 성공 (응답 코드: {webRequest.responseCode})");
                        IsUploading = false;
                        yield break;
                    }

                    if (attempt < maxRetries - 1)
                    {
                        Debug.LogWarning($"영상 업로드 실패 ({attempt + 1}/{maxRetries}): {webRequest.error}. {retryDelay}초 후 재시도.");
                        yield return CoroutineData.GetWaitForSeconds(retryDelay);
                    }
                    else
                    {
                        Debug.LogError($"영상 업로드 최종 실패: {webRequest.error}");
                    }
                }
            }

            IsUploading = false;
        }

        /// <summary>
        /// 세션 매니저에서 현재 유저의 ID를 문자열 형태로 반환함.
        /// </summary>
        private string GetUserIdString()
        {
            if (GameManager.Instance && SessionManager.Instance)
            {
                return SessionManager.Instance.CurrentUserId.ToString();
            }
            return "0";
        }

        /// <summary>
        /// 인스턴스 파괴 시 백그라운드 스레드를 취소하고 언매니지드 리소스를 해제함.
        /// </summary>
        private void OnDestroy()
        {
            if (_cts != null) 
            {
                _cts.Cancel();
                long timeoutTicks = DateTime.Now.Ticks + 1000 * 10000; 
                while (!_diskWriteTask.GetAwaiter().IsCompleted && DateTime.Now.Ticks < timeoutTicks)
                {
                    Thread.Sleep(10); 
                }
                _cts.Dispose();
            }

            if (_captureRT) _captureRT.Release();
            if (_encodeTexture) Destroy(_encodeTexture);
            if (_tintMaterial) Destroy(_tintMaterial);
        }
    }
}