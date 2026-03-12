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
using Debug = UnityEngine.Debug;

namespace My.Scripts.Timelapse
{
    /// <summary>
    /// 웹캠 화면을 캡처하여 타임랩스(고속) 및 리얼타임(1배속) 영상을 생성하는 레코더입니다.
    /// 비동기 GPU Readback과 백그라운드 스레드 파일 쓰기를 통해 메인 스레드 부하(프레임 드랍)를 최소화합니다.
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

        // 비동기 스레드 환경에서 안전하게 파일 쓰기 작업을 대기시키기 위한 스레드-세이프 큐
        private readonly ConcurrentQueue<SaveTaskData> _saveQueue = new ConcurrentQueue<SaveTaskData>();
        private CancellationTokenSource _cts;

        private float _realtimeRecordingStartTime;
        private float _realtimeTotalDuration;
        private bool _isRealtimeRecordingActive;

        private float _timelapseTimer;
        
        // 파일 쓰기(File.WriteAllBytesAsync)의 동시 진행 작업 수를 추적하여 안전한 정리를 보장하기 위한 카운터
        private int _activeDiskWrites = 0;

        // --- 외부 제어 및 상태 프로퍼티 ---
        public bool EnableTimelapseCapture { get; set; } = false;
        public bool EnableRealtimeCapture { get; set; } = false;
        
        public bool IsTimelapseProcessing { get; private set; }
        public bool IsRealtimeProcessing { get; private set; }
        public bool IsProcessing => IsTimelapseProcessing || IsRealtimeProcessing;
        public bool IsConverting => IsProcessing;

        public float RealtimeProgress { get; private set; } 

        public bool IsConversionSuccessful { get; private set; }
        public int LastExitCode { get; private set; }

        public string LastVideoPath { get; private set; }
        public string LastRealtimeVideoPath { get; private set; }

        /// <summary> 싱글톤 초기화 및 저장 경로 루트를 설정합니다. </summary>
        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                string dataPath = Application.dataPath;
                DirectoryInfo parentDir = Directory.GetParent(dataPath);
                _rootPath = (parentDir != null) ? parentDir.FullName : dataPath;

                _baseInterval = 1f / Mathf.Max(realtimeCaptureFPS, timelapseCaptureFPS);
            }
            else Destroy(gameObject);
        }

        /// <summary> 앱 시작과 동시에 백그라운드 파일 쓰기 루프를 가동합니다. </summary>
        private void Start()
        {
            _diskWriteTask = StartDiskWriteLoop();
        }

        /// <summary> 리얼타임 영상 변환 진행률의 시각적 피드백을 위해 임의로 프로그레스를 증가시킵니다. </summary>
        private void Update()
        {
            if (IsRealtimeProcessing && RealtimeProgress < 0.95f)
            {
                RealtimeProgress += Time.deltaTime * 0.1f;
            }
        }

        /// <summary> 현재 진행 중인 레벨을 설정하고, 리얼타임 녹화 대상(11~15)일 경우 타이머를 가동합니다. </summary>
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

        /// <summary> 날짜가 바뀔 것을 대비해 촬영 시작 전 폴더 경로를 갱신합니다. </summary>
        private void UpdatePaths()
        {
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
            catch (Exception e) { Debug.LogError($"[TimeLapse] 폴더 생성 실패 ({path}): {e.Message}"); }
        }

        /// <summary> 변환이 완료된 소스 프레임 이미지들을 삭제하여 디스크 용량을 확보합니다. </summary>
        private void ClearFolder(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                try { foreach (string file in Directory.GetFiles(path)) File.Delete(file); }
                catch (Exception e) { Debug.LogWarning($"[TimeLapse] 폴더 정리 중 오류 ({path}): {e.Message}"); }
            }
        }

        /// <summary> 새로운 플레이 시작(Q1 진입 등) 시 이전 촬영 데이터를 전면 초기화합니다. </summary>
        public void ClearRecordingData()
        {
            ClearRecordingDataAsync().Forget();
        }

        /// <summary> 대기 큐 및 현재 진행 중인 디스크 I/O 작업(_activeDiskWrites)이 모두 완료될 때까지 안전하게 대기한 뒤 파일을 삭제합니다. </summary>
        private async UniTaskVoid ClearRecordingDataAsync()
        {
            _globalFrameIndex = 0;
            _realtimeFrameIndex = 0;

            IsTimelapseProcessing = false;
            IsRealtimeProcessing = false;
            IsConversionSuccessful = false;
            RealtimeProgress = 0f;

            LastVideoPath = string.Empty;
            LastRealtimeVideoPath = string.Empty;
            _realtimeTotalDuration = 0f;
            _isRealtimeRecordingActive = false;

            while (_saveQueue.TryDequeue(out _)) { }

            await UniTask.WaitUntil(() => _activeDiskWrites == 0);

            ClearFolder(_sourceImageFolderPath);
            ClearFolder(_realtimeSourcePath);
        }

        /// <summary> 카메라 렌더텍스처를 초기화하고 비동기 캡처 루프를 시작합니다. </summary>
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

        /// <summary> 카메라 캡처를 중지하고 리얼타임 누적 촬영 시간을 정산합니다. </summary>
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
        /// 프레임 드랍을 완벽히 차단하기 위해 AsyncGPUReadback으로 비동기 메모리 복사를 수행한 뒤, 
        /// 무거운 JPG 인코딩 작업을 스레드 풀(백그라운드)로 오프로드합니다.
        /// </summary>
        private async UniTaskVoid CaptureLoopRoutine()
        {
            RenderTexture captureRT = _captureRT;
            float timelapseInterval = 1f / timelapseCaptureFPS;

            while (_isRecording && _webCam && _webCam.isPlaying)
            {
                if (_captureRT != captureRT) break;

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

                    if (_captureRT != captureRT || !_webCam || !_webCam.isPlaying) break;

                    Graphics.Blit(_webCam, captureRT);
                    
                    AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(captureRT);
                    await request.ToUniTask();

                    if (_captureRT != captureRT) break;
                    if (request.hasError) continue;

                    NativeArray<byte> nativeData = request.GetData<byte>();
                    byte[] rawBytes = nativeData.ToArray(); // 메인 스레드에서 관리되는 배열로 고속 복사
                    UnityEngine.Experimental.Rendering.GraphicsFormat format = captureRT.graphicsFormat;
                    
                    // 무거운 JPG 인코딩을 백그라운드 스레드에서 수행하여 CPU 스파이크(프레임 끊김) 원천 차단
                    byte[] bytes = await UniTask.RunOnThreadPool(() =>
                    {
                        return ImageConversion.EncodeArrayToJPG(rawBytes, format, (uint)captureWidth, (uint)captureHeight, 0, 70);
                    });

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
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        /// <summary> 디스크 I/O를 메인 스레드에서 분리하기 위해 작업 데이터를 큐에 삽입합니다. </summary>
        private void EnqueueSave(byte[] data, string folder, int index)
        {
            string path = Path.Combine(folder, $"img_{index:D5}.jpg");
            _saveQueue.Enqueue(new SaveTaskData { path = path, data = data });
        }

        /// <summary> 백그라운드 스레드 풀에서 무한 루프를 돌며 큐에 쌓인 이미지를 디스크에 저장합니다. </summary>
        private async UniTask StartDiskWriteLoop()
        {
            _cts = new CancellationTokenSource();
            await UniTask.SwitchToThreadPool();

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    if (_saveQueue.TryDequeue(out SaveTaskData task))
                    {
                        Interlocked.Increment(ref _activeDiskWrites);
                        try 
                        { 
                            await File.WriteAllBytesAsync(task.path, task.data); 
                        }
                        catch (Exception e) 
                        { 
                            Debug.LogError($"[TimeLapseRecorder] 파일 쓰기 실패: {e.Message}"); 
                        }
                        finally 
                        { 
                            Interlocked.Decrement(ref _activeDiskWrites); 
                        }
                    }
                    else await UniTask.Delay(50, cancellationToken: _cts.Token);
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary> 11~15번 문제 구간에서만 리얼타임 영상 소스를 수집하기 위한 검증 로직입니다. </summary>
        private bool IsRealtimeTargetLevel(string levelID)
        {
            if (string.IsNullOrEmpty(levelID)) return false;
            string numPart = Regex.Replace(levelID, "[^0-9]", "");
            if (int.TryParse(numPart, out int num))
            {
                if (num >= 11 && num <= 15) return true;
            }
            return false;
        }

        /// <summary> 전체 플레이 과정 중 수집된 프레임 이미지를 FFmpeg을 사용해 하나의 타임랩스 영상으로 인코딩합니다. </summary>
        public void ConvertToVideo()
        {
            if (IsTimelapseProcessing) return;
            IsTimelapseProcessing = true;

            float fps = 30f;
            if (_globalFrameIndex > 0 && timelapseDuration > 0)
            {
                fps = (float)_globalFrameIndex / timelapseDuration;
            }

            string fileName = $"{GetUserIdString()}_Timelapse";
            Debug.Log($"[Timelapse] 변환 시작: {_globalFrameIndex}장 / {timelapseDuration}초 목표 (FPS: {fps:F2})");

            ConversionSequence(_sourceImageFolderPath, _outputVideoFolderPath, fileName, fps, false).Forget();
        }

        /// <summary> 후반부(Q11~15) 플레이 과정 중 수집된 프레임 이미지를 FFmpeg을 사용해 리얼타임 영상으로 인코딩합니다. </summary>
        public void ConvertToRealtimeVideo()
        {
            if (IsRealtimeProcessing) return;

            if (_realtimeFrameIndex <= 0)
            {
                if (!IsRealtimeTargetLevel(_currentLevelID)) return;
                Debug.LogWarning($"[Realtime] 데이터 부족으로 변환 취소.");
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

        /// <summary> 예외 발생 시 리얼타임 진행 상태를 강제로 초기화합니다. </summary>
        public void ResetRealtimeProcessing()
        {
            IsRealtimeProcessing = false;
            RealtimeProgress = 0f;
        }

        /// <summary> 
        /// 외부 프로세스(FFmpeg)를 백그라운드 스레드에서 가동하여 수천 장의 이미지를 H.264 mp4 영상으로 병합합니다. 
        /// </summary>
        private async UniTaskVoid ConversionSequence(string sourceFolder, string outputFolder, string filePrefix, float fps, bool isRealtime)
        {
            try
            {
                // 변환 시작 전 큐에 남은 파일과 실제 디스크 I/O가 완벽히 종료될 때까지 대기하여 누락 방지
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
                if (fps < 1.0f) fps = 10f;

                string fpsStr = fps.ToString("F2", CultureInfo.InvariantCulture);
                string args = $"-framerate {fpsStr} -i \"{inputPattern}\" -c:v libx264 -profile:v baseline -pix_fmt yuv420p -x264-params colorprim=bt709:transfer=bt709:colormatrix=bt709 -color_primaries bt709 -color_trc bt709 -colorspace bt709 -color_range tv \"{outputPath}\"";

                // 메인 스레드 프리징을 막기 위해 외부 프로세스 실행 및 대기를 스레드 풀에서 수행
                await UniTask.SwitchToThreadPool();

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = ffmpegPath;
                    process.StartInfo.Arguments = args;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();

                    // 최대 60초간 응답 대기, 실패 시 강제 종료
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
                Debug.LogError($"[TimeLapseRecorder] 예외 발생: {e.Message}");
            }
            finally
            {
                if (isRealtime) IsRealtimeProcessing = false;
                else IsTimelapseProcessing = false;
            }
        }

        /// <summary> 
        /// 디스크에 생성된 대용량 비디오 파일을 스트림 방식을 사용하여 안전하게 서버로 전송합니다.
        /// </summary>
        private IEnumerator UploadVideoRoutine(string filePath)
        {
            if (!File.Exists(filePath)) yield break;

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
                if (GameManager.Instance.ApiConfig != null) baseUrl = GameManager.Instance.ApiConfig.UploadFileUrl;
            }

            if (string.IsNullOrEmpty(baseUrl)) yield break;

            string url = $"{baseUrl}?idx_user={idxUser}&uid={uid}&code=a1&type=mp4";

            using (UnityWebRequest webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                // 스트림 방식을 사용하여 대용량 파일을 메모리에 통째로 올리지 않고 디스크에서 직접 읽어 전송합니다.
                webRequest.uploadHandler = new UploadHandlerFile(filePath);
                webRequest.uploadHandler.contentType = "video/mp4"; 

                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.timeout = 60;

                yield return webRequest.SendWebRequest();
            }
        }

        private string GetUserIdString()
        {
            if (GameManager.Instance && SessionManager.Instance)
            {
                return SessionManager.Instance.CurrentUserId.ToString();
            }
            return "0";
        }

        /// <summary> 오브젝트 파괴 시 진행 중인 백그라운드 파일 쓰기 태스크를 취소하고 렌더 텍스처 메모리를 반환합니다. </summary>
        private void OnDestroy()
        {
            if (_cts != null) 
            {
                _cts.Cancel();
                // Task 취소 대기 중 메인 스레드 무한 루프 방지를 위한 타임아웃(1초) 설정
                long timeoutTicks = DateTime.Now.Ticks + 1000 * 10000; 
                while (!_diskWriteTask.GetAwaiter().IsCompleted && DateTime.Now.Ticks < timeoutTicks)
                {
                    Thread.Sleep(10); 
                }
                _cts.Dispose();
            }

            if (_captureRT) _captureRT.Release();
            if (_encodeTexture) Destroy(_encodeTexture);
        }
    }
}