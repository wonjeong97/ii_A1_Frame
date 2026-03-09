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
    /// 비동기 GPU Readback과 별도 스레드 파일 쓰기를 통해 메인 스레드 부하를 최소화합니다.
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

        private readonly ConcurrentQueue<SaveTaskData> _saveQueue = new ConcurrentQueue<SaveTaskData>();
        private CancellationTokenSource _cts;

        private float _realtimeRecordingStartTime;
        private float _realtimeTotalDuration;
        private bool _isRealtimeRecordingActive;

        private float _timelapseTimer;

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

        private void Start()
        {
            _diskWriteTask = StartDiskWriteLoop();
        }

        private void Update()
        {
            if (IsRealtimeProcessing && RealtimeProgress < 0.95f)
            {
                RealtimeProgress += Time.deltaTime * 0.1f;
            }
        }

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

        private void ClearFolder(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                try { foreach (string file in Directory.GetFiles(path)) File.Delete(file); }
                catch (Exception e) { Debug.LogWarning($"[TimeLapse] 폴더 정리 중 오류 ({path}): {e.Message}"); }
            }
        }

        public void ClearRecordingData()
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

            ClearFolder(_sourceImageFolderPath);
            ClearFolder(_realtimeSourcePath);
        }

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
                    
                    // var 제거 및 명시적 타입 지정
                    AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(captureRT);
                    await request.ToUniTask();

                    if (_captureRT != captureRT) break;
                    if (request.hasError) continue;

                    // var 제거
                    NativeArray<byte> nativeData = request.GetData<byte>();

                    NativeArray<byte> encodedNativeData = ImageConversion.EncodeNativeArrayToJPG(
                        nativeData, captureRT.graphicsFormat, (uint)captureWidth, (uint)captureHeight, 0, 70
                    );

                    byte[] bytes = encodedNativeData.ToArray();
                    encodedNativeData.Dispose();

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

        private void EnqueueSave(byte[] data, string folder, int index)
        {
            string path = Path.Combine(folder, $"img_{index:D5}.jpg");
            _saveQueue.Enqueue(new SaveTaskData { path = path, data = data });
        }

        private async UniTask StartDiskWriteLoop()
        {
            _cts = new CancellationTokenSource();
            await UniTask.SwitchToThreadPool();

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    // var 제거
                    if (_saveQueue.TryDequeue(out SaveTaskData task))
                    {
                        try { await File.WriteAllBytesAsync(task.path, task.data); }
                        catch (Exception e) { Debug.LogError($"[TimeLapseRecorder] 파일 쓰기 실패: {e.Message}"); }
                    }
                    else await UniTask.Delay(50, cancellationToken: _cts.Token);
                }
            }
            catch (OperationCanceledException) { }
        }

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

        public void ResetRealtimeProcessing()
        {
            IsRealtimeProcessing = false;
            RealtimeProgress = 0f;
        }

        private async UniTaskVoid ConversionSequence(string sourceFolder, string outputFolder, string filePrefix, float fps, bool isRealtime)
        {
            try
            {
                await UniTask.WaitUntil(() => _saveQueue.IsEmpty);

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
                string args = $"-framerate {fpsStr} -i \"{inputPattern}\" -c:v libx264 -profile:v baseline -pix_fmt yuv420p -color_primaries bt709 -color_trc bt709 -colorspace bt709 \"{outputPath}\"";

                await UniTask.SwitchToThreadPool();

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = ffmpegPath;
                    process.StartInfo.Arguments = args;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();

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

        private IEnumerator UploadVideoRoutine(string filePath)
        {
            if (!File.Exists(filePath)) yield break;

            int idxUser = 0;
            string uid = "";
            string baseUrl = "";

            if (GameManager.Instance)
            {
                idxUser = SessionManager.Instance.CurrentUserId;
                uid = SessionManager.Instance.PlayerAUid;
                if (GameManager.Instance.ApiConfig != null) baseUrl = GameManager.Instance.ApiConfig.UploadFileUrl;
            }

            if (string.IsNullOrEmpty(baseUrl)) yield break;

            byte[] videoBytes = File.ReadAllBytes(filePath);
            string url = $"{baseUrl}?idx_user={idxUser}&uid={uid}&code=a1&type=mp4";

            using (UnityWebRequest webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                UploadHandlerRaw uploadHandler = new UploadHandlerRaw(videoBytes);
                uploadHandler.contentType = "video/mp4"; 

                webRequest.uploadHandler = uploadHandler;
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.timeout = 60;

                yield return webRequest.SendWebRequest();
            }
        }

        private string GetUserIdString()
        {
            if (GameManager.Instance)
            {
                return SessionManager.Instance.CurrentUserId.ToString();
            }
            return "0";
        }

        private void OnDestroy()
        {
            if (_cts != null) // C# 표준 객체이므로 != null 사용
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
        }
    }
}