using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using My.Scripts.Global;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace My.Scripts.Timelapse
{
    /// <summary>
    /// 웹캠 프레임을 비동기로 캡처하고 FFMPEG를 활용해 타임랩스 및 리얼타임 비디오로 인코딩함.
    /// UniTask를 사용하여 모든 비동기 시퀀스에서 가비지 할당을 최소화함.
    /// </summary>
    public class TimeLapseRecorder : MonoBehaviour
    {
        public static TimeLapseRecorder Instance;

        [Header("Capture Settings")]
        private int timelapseCaptureFPS;

        private int realtimeCaptureFPS;

        [Header("Output Settings")]
        private float timelapseDuration;

        private float realtimeDuration;

        private int captureWidth;
        private int captureHeight;

        [Header("API Retry Settings")]
        [SerializeField] private int maxRetries;

        [SerializeField] private float retryDelay;

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

        /// <summary>
        /// 싱글톤 초기화 및 런타임 상수를 설정함.
        /// 필드 선언부 초기화 금지 규칙에 따라 Awake에서 기본값을 할당함.
        /// </summary>
        protected void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                InitializeDefaultSettings();

                string dataPath = Application.dataPath;
                DirectoryInfo parentDir = Directory.GetParent(dataPath);
                _rootPath = (parentDir != null) ? parentDir.FullName : dataPath;

                _baseInterval = 1f / Mathf.Max(realtimeCaptureFPS, timelapseCaptureFPS);
            }
            else
            {
                Destroy(gameObject);
            }
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
                Debug.LogError(string.Format("폴더 생성 실패: {0}", e.Message));
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
                Debug.LogWarning(string.Format("폴더 정리 오류: {0}", e.Message));
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

            while (_saveQueue.TryDequeue(out SaveTaskData _))
            {
            }

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

            CaptureLoopAsync().Forget();
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

        private async UniTaskVoid CaptureLoopAsync()
        {
            RenderTexture captureRT = _captureRT;
            float timelapseInterval = 1f / timelapseCaptureFPS;

            while (_isRecording && _webCam && _webCam.isPlaying)
            {
                if (!ReferenceEquals(_captureRT, captureRT)) break;

                _timer += Time.deltaTime;
                _timelapseTimer += Time.deltaTime;

                if (ShouldCaptureThisFrame(timelapseInterval, out bool saveToTimelapse, out bool saveToRealtime))
                {
                    await CaptureAndEnqueueFrame(captureRT, saveToTimelapse, saveToRealtime);
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
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

        /// <summary>
        /// 현재 프레임을 렌더링하고 이미지 데이터를 추출하여 저장 큐에 적재함.
        /// 렌더링, 데이터 추출, 인덱싱 로직을 분리하여 메서드 복잡도를 완화함.
        /// </summary>
        private async UniTask CaptureAndEnqueueFrame(RenderTexture captureRT, bool saveToTimelapse, bool saveToRealtime)
        {
            await UniTask.WaitForEndOfFrame(this);

            BlitWebCamToRT(captureRT);

            byte[] imageBytes = await GetFrameBytesAsync(captureRT);
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

        private async UniTask<byte[]> GetFrameBytesAsync(RenderTexture targetRT)
        {
            byte[] bytes = await TryAsyncReadback(targetRT);
            if (bytes == null)
            {
                bytes = SyncCaptureFallback(targetRT);
            }

            return bytes;
        }

        private void EnqueueSaveTask(byte[] data, string folder, ref int indexCounter)
        {
            int index = Interlocked.Increment(ref indexCounter) - 1;
            string fileName = string.Format("img_{0:D5}.jpg", index);
            string fullPath = Path.Combine(folder, fileName);

            _saveQueue.Enqueue(new SaveTaskData
            {
                path = fullPath,
                data = data
            });
        }

        private async UniTask<byte[]> TryAsyncReadback(RenderTexture captureRT)
        {
            if (!SystemInfo.supportsAsyncGPUReadback) return null;

            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(captureRT, 0, TextureFormat.RGBA32);
            await request.ToUniTask();

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

        /// <summary>
        /// 백그라운드 스레드에서 저장 큐를 모니터링하며 디스크 쓰기 작업을 수행함.
        /// </summary>
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
                        await UniTask.Delay(50, cancellationToken: _cts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTask ProcessDiskWriteAsync(SaveTaskData task)
        {
            Interlocked.Increment(ref _activeDiskWrites);

            try
            {
                string directory = Path.GetDirectoryName(task.path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllBytesAsync(task.path, task.data);
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("TimeLapseRecorder: 파일 쓰기 중 예외 발생 - {0}", e.Message));
            }
            finally
            {
                Interlocked.Decrement(ref _activeDiskWrites);
            }
        }

        private bool IsRealtimeTargetLevel(string levelID)
        {
            if (string.IsNullOrEmpty(levelID)) return false;

            string numPart = Regex.Replace(levelID, "[^0-9]", "");
            return int.TryParse(numPart, out int num) && num >= 11 && num <= 15;
        }

        public void ConvertToVideo()
        {
            if (IsTimelapseProcessing) return;

            IsTimelapseProcessing = true;

            float fps = (_globalFrameIndex > 0 && timelapseDuration > 0)
                ? _globalFrameIndex / timelapseDuration
                : 30f;

            string fileName = string.Format("{0}_Timelapse", GetUserIdString());
            ConversionSequence(_sourceImageFolderPath, _outputVideoFolderPath, fileName, fps, false).Forget();
        }

        public void ConvertToRealtimeVideo()
        {
            if (IsRealtimeProcessing || _realtimeFrameIndex <= 0) return;

            IsRealtimeProcessing = true;
            RealtimeProgress = 0f;

            float fps = (_realtimeFrameIndex > 0 && realtimeDuration > 0)
                ? _realtimeFrameIndex / realtimeDuration
                : 30f;

            string fileName = string.Format("{0}_Realtime", GetUserIdString());
            ConversionSequence(_realtimeSourcePath, _realtimeVideoPath, fileName, fps, true).Forget();
        }

        private async UniTaskVoid ConversionSequence(string sourceFolder, string outputFolder, string filePrefix,
            float fps, bool isRealtime)
        {
            try
            {
                await UniTask.WaitUntil(this, t => t._saveQueue.IsEmpty && t._activeDiskWrites == 0);

                string outputPath = Path.Combine(outputFolder, string.Format("{0}.mp4", filePrefix));
                if (!ValidateSourceFiles(sourceFolder, outputPath, isRealtime)) return;

                bool success = await ExecuteFfmpeg(sourceFolder, outputPath, fps);
                HandleConversionResult(success, outputPath, sourceFolder, isRealtime);
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("비디오 변환 시퀀스 에러: {0}", e.Message));
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
                if (File.Exists(outputPath)) HandleConversionResult(true, outputPath, sourceFolder, isRealtime);
                return false;
            }

            if (File.Exists(outputPath)) File.Delete(outputPath);
            return true;
        }

        private async UniTask<bool> ExecuteFfmpeg(string sourceFolder, string outputPath, float fps)
        {
            string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");
            string inputPattern = Path.Combine(sourceFolder, "img_%05d.jpg");
            string fpsStr = Mathf.Max(10f, fps).ToString("F2", CultureInfo.InvariantCulture);
            string args = string.Format(
                "-framerate {0} -i \"{1}\" -c:v libx264 -profile:v baseline -pix_fmt yuv420p -x264-params colorprim=bt709:transfer=bt709:colormatrix=bt709 -color_primaries bt709 -color_trc bt709 -colorspace bt709 -color_range tv \"{2}\"",
                fpsStr, inputPattern, outputPath);

            await UniTask.SwitchToThreadPool();
            return RunProcess(ffmpegPath, args, 60000);
        }

        private bool RunProcess(string fileName, string args, int timeoutMs)
        {
            using (Process process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                    { FileName = fileName, Arguments = args, UseShellExecute = false, CreateNoWindow = true };
                process.Start();
                if (process.WaitForExit(timeoutMs)) return process.ExitCode == 0;

                try
                {
                    process.Kill();
                }
                catch
                {
                }

                return false;
            }
        }

        private void HandleConversionResult(bool success, string outputPath, string sourceFolder, bool isRealtime)
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
                    UploadVideoAsync(outputPath).Forget();
                }

                ClearFolder(sourceFolder);
            }
        }

        private async UniTaskVoid UploadVideoAsync(string filePath)
        {
            IsUploading = true;
            string url = ConstructUploadUrl();

            if (string.IsNullOrEmpty(url) || !File.Exists(filePath))
            {
                IsUploading = false;
                return;
            }

            await ExecuteUploadWithRetryAsync(url, filePath);
            IsUploading = false;
        }

        private string ConstructUploadUrl()
        {
            if (!GameManager.Instance || !SessionManager.Instance || GameManager.Instance.ApiConfig == null)
                return null;

            return string.Format("{0}?idx_user={1}&uid={2}&code=A1&type=mp4",
                GameManager.Instance.ApiConfig.UploadFileUrl,
                SessionManager.Instance.CurrentUserId,
                SessionManager.Instance.PlayerAUid);
        }

        private async UniTask ExecuteUploadWithRetryAsync(string url, string filePath)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    request.uploadHandler = new UploadHandlerFile(filePath) { contentType = "video/mp4" };
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.timeout = 300;

                    try
                    {
                        await request.SendWebRequest().ToUniTask();
                        if (request.result == UnityWebRequest.Result.Success) return;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning(string.Format("업로드 시도 {0} 실패: {1}", attempt + 1, e.Message));
                    }

                    await UniTask.Delay(TimeSpan.FromSeconds(retryDelay));
                }
            }
        }

        private string GetUserIdString() =>
            (SessionManager.Instance) ? SessionManager.Instance.CurrentUserId.ToString() : "0";

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