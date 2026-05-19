using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Text; 
using Microsoft.Extensions.Logging; 
using ZLogger; 
using VContainer; 
using My.Scripts.Global;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Networking;
using UnityEngine.Rendering;

namespace My.Scripts.Timelapse
{
    /// <summary>
    /// 웹캠 프레임을 비동기로 캡처하고 FFMPEG를 활용해 타임랩스 및 리얼타임 비디오로 인코딩함.
    /// VContainer 의존성 주입, OS 프로세스 좀비화 방지, 메인 스레드 스위칭 보장 및 GPU 메모리 누수 방어가 적용됨.
    /// </summary>
    public class TimeLapseRecorder : MonoBehaviour
    {
        // Capture Settings
        private int timelapseCaptureFPS;
        private int realtimeCaptureFPS;

        // Output Settings
        private float timelapseDuration;
        private float realtimeDuration;

        private int captureWidth;
        private int captureHeight;

        // API Retry Settings
        private int maxRetries;
        private float retryDelay;

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
        private int _activeDiskWrites;
        
        private readonly static Regex NumericRegex = new Regex("[^0-9]", RegexOptions.Compiled);

        public bool EnableTimelapseCapture { get; set; }
        public bool EnableRealtimeCapture { get; set; }

        public bool IsTimelapseProcessing { get; private set; }
        public bool IsRealtimeProcessing { get; private set; }
        public bool IsUploading { get; private set; }

        public bool IsProcessing => IsTimelapseProcessing || IsRealtimeProcessing || IsUploading;
        public float RealtimeProgress { get; private set; }

        public bool IsConversionSuccessful { get; private set; }
        public int LastExitCode { get; private set; }

        public string LastVideoPath { get; private set; }
        public string LastRealtimeVideoPath { get; private set; }

        public Color CurrentTint { get; set; }
        private Material _tintMaterial;

        // --- 의존성 주입 (DI) 변수 ---
        private ILogger<TimeLapseRecorder> _logger;
        private GameManager _gameManager;
        private SessionManager _sessionManager;

        [Inject]
        public void Construct(
            ILogger<TimeLapseRecorder> logger,
            GameManager gameManager,
            SessionManager sessionManager)
        {
            _logger = logger;
            _gameManager = gameManager;
            _sessionManager = sessionManager;
        }

        protected void Awake()
        {
            EnsurePathsInitialized();
        }

        private void InitializeDefaultSettings()
        {
            timelapseCaptureFPS = 5;
            realtimeCaptureFPS = 30;
            timelapseDuration = 20f;
            realtimeDuration = 15f;
            captureWidth = 1920;
            captureHeight = 1080;
            maxRetries = 10;
            retryDelay = 1.0f;
            CurrentTint = Color.white;
        }

        protected void Start()
        {
            _diskWriteTask = StartDiskWriteLoop();
        }

        protected void Update()
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
        
        private void EnsurePathsInitialized()
        {
            if (string.IsNullOrEmpty(_rootPath) || 
                string.IsNullOrEmpty(_outputVideoFolderPath) || 
                string.IsNullOrEmpty(_realtimeVideoPath) ||
                string.IsNullOrEmpty(_realtimeSourcePath))
            {
                InitializeDefaultSettings();

                string dataPath = Application.dataPath;
                DirectoryInfo parentDir = Directory.GetParent(dataPath);
                _rootPath = (parentDir != null) ? parentDir.FullName : dataPath;

                _baseInterval = 1f / Mathf.Max(realtimeCaptureFPS, timelapseCaptureFPS);

                UpdatePaths();
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
            try
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            }
            catch (Exception e)
            {
                _logger.ZLogError(e, $"폴더 생성 실패: {e.Message}");
            }
        }

        private void ClearFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

            try
            {
                foreach (string file in Directory.GetFiles(path)) File.Delete(file);
            }
            catch (Exception e)
            {
                _logger.ZLogWarning($"폴더 정리 오류: {e.Message}");
            }
        }

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

            while (_saveQueue.TryDequeue(out SaveTaskData _)) { }

            await UniTask.WaitUntil(this, t => t._activeDiskWrites == 0);

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

            if (_captureRT) _captureRT.Release();
            _captureRT = new RenderTexture(captureWidth, captureHeight, 0, RenderTextureFormat.ARGB32);

            if (_encodeTexture) Destroy(_encodeTexture);
            _encodeTexture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGBA32, false);

            CaptureLoopAsync(this.GetCancellationTokenOnDestroy()).Forget();
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

        private async UniTaskVoid CaptureLoopAsync(CancellationToken ct)
        {
            RenderTexture captureRT = _captureRT;
            float timelapseInterval = 1f / timelapseCaptureFPS;

            while (_isRecording && _webCam && _webCam.isPlaying && !ct.IsCancellationRequested)
            {
                if (!ReferenceEquals(_captureRT, captureRT)) break;

                _timer += Time.deltaTime;
                _timelapseTimer += Time.deltaTime;

                if (ShouldCaptureThisFrame(timelapseInterval, out bool saveToTimelapse, out bool saveToRealtime))
                {
                    await CaptureAndEnqueueFrame(captureRT, saveToTimelapse, saveToRealtime, ct);
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        private bool ShouldCaptureThisFrame(float timelapseInterval, out bool saveToTimelapse, out bool saveToRealtime)
        {
            saveToTimelapse = false;
            saveToRealtime = false;

            if (_timer < _baseInterval) return false;

            _timer -= _baseInterval;
            saveToRealtime = EnableRealtimeCapture && IsRealtimeTargetLevel(_currentLevelID);

            if (EnableTimelapseCapture && _timelapseTimer >= timelapseInterval)
            {
                saveToTimelapse = true;
                _timelapseTimer = 0f;
            }

            return saveToRealtime || saveToTimelapse;
        }

        private async UniTask CaptureAndEnqueueFrame(RenderTexture captureRT, bool saveToTimelapse, bool saveToRealtime, CancellationToken ct)
        {
            await UniTask.WaitForEndOfFrame(this, ct);

            BlitWebCamToRT(captureRT);

            byte[] imageBytes = await GetFrameBytesAsync(captureRT, ct);
            if (imageBytes == null || imageBytes.Length == 0)
            {
                return;
            }

            if (saveToTimelapse)
            {
                EnqueueSaveTask(imageBytes, _sourceImageFolderPath, ref _globalFrameIndex);
            }

            if (saveToRealtime)
            {
                EnqueueSaveTask(imageBytes, _realtimeSourcePath, ref _realtimeFrameIndex);
            }
        }

        private void BlitWebCamToRT(RenderTexture captureRT)
        {
            if (CurrentTint == Color.white)
            {
                Graphics.Blit(_webCam, captureRT);
                return;
            }

            if (!_tintMaterial) _tintMaterial = new Material(Shader.Find("Sprites/Default"));
            _tintMaterial.color = CurrentTint;
            Graphics.Blit(_webCam, captureRT, _tintMaterial);
        }

        private async UniTask<byte[]> GetFrameBytesAsync(RenderTexture targetRT, CancellationToken ct)
        {
            byte[] bytes = await TryAsyncReadback(targetRT, ct);
            if (bytes == null)
            {
                bytes = SyncCaptureFallback(targetRT);
            }

            return bytes;
        }

        private void EnqueueSaveTask(byte[] data, string folder, ref int indexCounter)
        {
            int index = Interlocked.Increment(ref indexCounter) - 1;
            
            string fileName = ZString.Format("img_{0:D5}.jpg", index);
            string fullPath = Path.Combine(folder, fileName);

            _saveQueue.Enqueue(new SaveTaskData
            {
                path = fullPath,
                data = data
            });
        }

        private async UniTask<byte[]> TryAsyncReadback(RenderTexture captureRT, CancellationToken ct)
        {
            if (!SystemInfo.supportsAsyncGPUReadback) return null;

            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(captureRT, 0, TextureFormat.RGBA32);
            
            await request.ToUniTask(cancellationToken: ct);

            if (request.hasError || !_isRecording) return null;

            NativeArray<byte> nativeData = request.GetData<byte>();
            byte[] rawBytes = nativeData.ToArray();

            return await UniTask.RunOnThreadPool(() =>
                ImageConversion.EncodeArrayToJPG(rawBytes, GraphicsFormat.R8G8B8A8_UNorm, (uint)captureWidth,
                    (uint)captureHeight, 0, 70));
        }

        private byte[] SyncCaptureFallback(RenderTexture captureRT)
        {
            if (!_encodeTexture) return null;

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = captureRT;
            _encodeTexture.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            _encodeTexture.Apply();
            RenderTexture.active = prev;

            return ImageConversion.EncodeToJPG(_encodeTexture, 80);
        }

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
                        await ProcessDiskWriteAsync(task);
                    }
                    else
                    {
                        await System.Threading.Tasks.Task.Delay(50, _cts.Token);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                _logger?.ZLogError(e, $"디스크 쓰기 루프 강제 종료됨: {e.Message}");
            }
        }

        private async UniTask ProcessDiskWriteAsync(SaveTaskData task)
        {
            Interlocked.Increment(ref _activeDiskWrites);
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                string directory = Path.GetDirectoryName(task.path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllBytesAsync(task.path, task.data);
                
                sw.Stop();
                if (sw.ElapsedMilliseconds > 100) // 100ms 이상 소요 시 경고 로그
                {
                    _logger?.ZLogWarning($"[TimeLapseRecorder] 디스크 쓰기 병목 감지: {sw.ElapsedMilliseconds}ms 소요 (경로: {task.path})");
                }
            }
            catch (Exception e)
            {
                _logger?.ZLogError(e, $"TimeLapseRecorder: 파일 쓰기 중 예외 발생 - {e.Message}");
            }
            finally
            {
                Interlocked.Decrement(ref _activeDiskWrites);
            }
        }

        private bool IsRealtimeTargetLevel(string levelID)
        {
            if (string.IsNullOrEmpty(levelID)) return false;

            string numPart = NumericRegex.Replace(levelID, "");
            return int.TryParse(numPart, out int num) && num >= 11 && num <= 15;
        }

        public void ConvertToVideo()
        {
            if (IsProcessing) return;

            IsTimelapseProcessing = true;

            float fps = (_globalFrameIndex > 0 && timelapseDuration > 0) ? _globalFrameIndex / timelapseDuration : 30f;

            string fileName = ZString.Format("{0}_Timelapse", GetUserIdString());
            ConversionSequence(_sourceImageFolderPath, _outputVideoFolderPath, fileName, fps, false, this.GetCancellationTokenOnDestroy()).Forget();
        }

        public void ConvertToRealtimeVideo()
        {
            if (IsProcessing) return;

            IsRealtimeProcessing = true;
            RealtimeProgress = 0f;

            float fps = (_realtimeFrameIndex > 0 && realtimeDuration > 0) ? _realtimeFrameIndex / realtimeDuration : 30f;

            string fileName = ZString.Format("{0}_Realtime", GetUserIdString());
            ConversionSequence(_realtimeSourcePath, _realtimeVideoPath, fileName, fps, true, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid ConversionSequence(string sourceFolder, string outputFolder, string filePrefix,
            float fps, bool isRealtime, CancellationToken ct)
        {
            try
            {   
                if (string.IsNullOrEmpty(outputFolder))
                {
                    _logger?.ZLogWarning($"[TimeLapseRecorder] Output Folder 경로가 null입니다. 변환을 건너뜁니다.");
                    return;
                }
                await UniTask.WaitUntil(this, t => t._saveQueue.IsEmpty && t._activeDiskWrites == 0, cancellationToken: ct);

                string outputPath = Path.Combine(outputFolder, ZString.Format("{0}.mp4", filePrefix));
                if (!ValidateSourceFiles(sourceFolder, outputPath, isRealtime)) return;

                Stopwatch sw = Stopwatch.StartNew();
                _logger?.ZLogInformation($"[TimeLapseRecorder] 영상 변환 시작: {filePrefix} (FPS: {fps})");

                bool success = await ExecuteFfmpeg(sourceFolder, outputPath, fps, ct);
                
                sw.Stop();
                _logger?.ZLogInformation($"[TimeLapseRecorder] 영상 변환 완료: {filePrefix} ({sw.Elapsed.TotalSeconds:F1}초 소요)");
                
                HandleConversionResult(success, outputPath, sourceFolder, isRealtime, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                _logger.ZLogError(e, $"비디오 변환 시퀀스 에러: {e.Message}");
            }
            finally
            {
                if (isRealtime) IsRealtimeProcessing = false;
                else IsTimelapseProcessing = false;
            }
        }

        private bool ValidateSourceFiles(string sourceFolder, string outputPath, bool isRealtime)
        {
            bool hasFiles = Directory.Exists(sourceFolder) && Directory.GetFiles(sourceFolder, "img_*.jpg").Length > 0;
            if (!hasFiles)
            {
                // Validate 도중 빠져나갈 때는 Upload가 필요없으므로 ct는 무시
                if (File.Exists(outputPath)) HandleConversionResult(true, outputPath, sourceFolder, isRealtime, CancellationToken.None);
                return false;
            }

            if (File.Exists(outputPath)) File.Delete(outputPath);
            return true;
        }

        private async UniTask<bool> ExecuteFfmpeg(string sourceFolder, string outputPath, float fps, CancellationToken ct)
        {
            string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");
            string inputPattern = Path.Combine(sourceFolder, "img_%05d.jpg");
            string fpsStr = Mathf.Max(10f, fps).ToString("F2", CultureInfo.InvariantCulture);
            
            string args = ZString.Format(
                "-framerate {0} -i \"{1}\" -c:v libx264 -profile:v baseline -pix_fmt yuv420p -x264-params colorprim=bt709:transfer=bt709:colormatrix=bt709 -color_primaries bt709 -color_trc bt709 -colorspace bt709 -color_range tv \"{2}\"",
                fpsStr, inputPattern, outputPath);

            // 1. 스레드풀 진입 (FFMPEG 실행으로 인한 메인 스레드 멈춤 방지)
            await UniTask.SwitchToThreadPool();
            
            bool result = RunProcess(ffmpegPath, args, 60000, ct);
            
            // 2. 이어지는 UnityWebRequest 객체 호출을 위해 반드시 유니티 메인 스레드로 복귀
            await UniTask.SwitchToMainThread();
            
            return result;
        }

        private bool RunProcess(string fileName, string args, int timeoutMs, CancellationToken ct)
        {
            using (Process process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                    { FileName = fileName, Arguments = args, UseShellExecute = false, CreateNoWindow = true };
                process.Start();
                
                int elapsed = 0;
                while (!process.HasExited)
                {
                    if (ct.IsCancellationRequested || elapsed > timeoutMs)
                    {
                        try { process.Kill(); } catch { }
                        return false;
                    }
                    Thread.Sleep(100);
                    elapsed += 100;
                }

                return process.ExitCode == 0;
            }
        }

        private void HandleConversionResult(bool success, string outputPath, string sourceFolder, bool isRealtime, CancellationToken ct)
        {
            IsConversionSuccessful = success;
            if (success)
            {
                if (isRealtime)
                {
                    LastRealtimeVideoPath = outputPath;
                    RealtimeProgress = 1f;
                }
                else
                {
                    LastVideoPath = outputPath;
                    UploadVideoAsync(outputPath, ct).Forget();
                }

                ClearFolder(sourceFolder);
            }
        }

        private async UniTaskVoid UploadVideoAsync(string filePath, CancellationToken ct)
        {
            IsUploading = true;
            try
            {
                string url = ConstructUploadUrl();
                if (string.IsNullOrEmpty(url) || !File.Exists(filePath)) return;
                await ExecuteUploadWithRetryAsync(url, filePath, ct);
            }
            finally
            {
                IsUploading = false;
            }
        }

        private string ConstructUploadUrl()
        {
            if (!_gameManager || !_sessionManager || _gameManager.ApiConfig == null)
                return null;

            return ZString.Format("{0}?idx_user={1}&uid={2}&code={3}&type=mp4",
                _gameManager.ApiConfig.UploadFileUrl,
                _sessionManager.CurrentUserId,
                _sessionManager.PlayerAUid,
                GameConstants.Module.Code);
        }

        private async UniTask ExecuteUploadWithRetryAsync(string url, string filePath, CancellationToken ct)
        {
#if UNITY_EDITOR
            _logger?.ZLogInformation($"[TimeLapseRecorder] 유니티 에디터 환경: 영상 업로드를 스킵합니다.");
            return;
#endif
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    request.uploadHandler = new UploadHandlerFile(filePath) { contentType = "video/mp4" };
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.timeout = 300;

                    try
                    {
                        await request.SendWebRequest().ToUniTask(cancellationToken: ct);
                        if (request.result == UnityWebRequest.Result.Success) return;
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception e)
                    {
                        _logger.ZLogWarning($"업로드 시도 {attempt + 1} 실패: {e.Message}");
                    }

                    await UniTask.Delay(TimeSpan.FromSeconds(retryDelay), cancellationToken: ct);
                }
            }
        }

        private string GetUserIdString() =>
            (_sessionManager) ? _sessionManager.CurrentUserId.ToString() : "0";

        protected void OnDestroy()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
            }

            if (_captureRT) _captureRT.Release();
            if (_encodeTexture) Destroy(_encodeTexture);
            if (_tintMaterial) Destroy(_tintMaterial);
        }
    }
}