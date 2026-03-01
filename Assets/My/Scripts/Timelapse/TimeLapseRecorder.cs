using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering; 
using Cysharp.Threading.Tasks;
using My.Scripts.Global;
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
        public int timelapseCaptureFPS = 10;
        public int realtimeCaptureFPS = 30;
        
        [Header("Output Settings")]
        public float timelapseDuration = 20f; 
        public float realtimeDuration = 15f;  
        
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
        
        // 디스크 쓰기 루프 태스크 추적용 (OnDestroy 시 안전한 종료 대기 위함)
        private UniTask _diskWriteTask;

        private struct SaveTaskData
        {
            public string path;
            public byte[] data;
        }
        
        // Thread-Safe한 큐를 사용하여 캡처 스레드와 파일 쓰기 스레드 간 데이터 전달
        private readonly ConcurrentQueue<SaveTaskData> _saveQueue = new ConcurrentQueue<SaveTaskData>();
        private CancellationTokenSource _cts;

        private float _realtimeRecordingStartTime;
        private float _realtimeTotalDuration;
        private bool _isRealtimeRecordingActive;
        
        private float _timelapseTimer = 0f;

        // 타임랩스와 리얼타임 변환 상태를 분리하여 상호 데드락 방지
        public bool IsTimelapseProcessing { get; private set; }
        public bool IsRealtimeProcessing { get; private set; }
        
        // 호환성을 위해 하나라도 처리 중이면 true
        public bool IsProcessing => IsTimelapseProcessing || IsRealtimeProcessing;  
        public bool IsConverting => IsProcessing; 
        
        public bool IsConversionSuccessful { get; private set; } 
        public int LastExitCode { get; private set; }   
        
        public string LastVideoPath { get; private set; } 
        public string LastRealtimeVideoPath { get; private set; }

        private void Awake()
        {
            if (Instance == null)
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
            // 파일 쓰기 루프 시작 (Forget 하지 않고 Task 보관)
            _diskWriteTask = StartDiskWriteLoop();
        }

        /// <summary>
        /// 현재 레벨 ID를 설정하고 리얼타임 녹화 대상인지 판단하여 시간을 측정합니다.
        /// </summary>
        public void SetCurrentLevel(string levelID)
        {
            _currentLevelID = levelID;
            bool isTarget = IsRealtimeTargetLevel(levelID);
            
            // 리얼타임 녹화 대상 레벨에 진입하면 녹화 활성 상태로 전환
            if (isTarget && !_isRealtimeRecordingActive)
            {
                _isRealtimeRecordingActive = true;
                _realtimeRecordingStartTime = Time.time;
                Debug.Log($"[TimeLapse] 리얼타임 녹화 시간 측정 시작 (Level: {levelID})");
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

        /// <summary>
        /// 새로운 세션을 위해 이전 녹화 데이터를 모두 초기화합니다.
        /// </summary>
        public void ClearRecordingData()
        {
            _globalFrameIndex = 0;
            _realtimeFrameIndex = 0;
            
            // 상태 플래그 초기화
            IsTimelapseProcessing = false;
            IsRealtimeProcessing = false;
            IsConversionSuccessful = false;
            
            LastVideoPath = string.Empty;
            LastRealtimeVideoPath = string.Empty;
            _realtimeTotalDuration = 0f;
            _isRealtimeRecordingActive = false;
            
            // 큐 비우기
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

            if (_captureRT != null) _captureRT.Release();
            _captureRT = new RenderTexture(captureWidth, captureHeight, 0, RenderTextureFormat.ARGB32);
            
            if (_encodeTexture != null) Destroy(_encodeTexture);
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
                Debug.Log($"[TimeLapse] 리얼타임 녹화 종료. 추가: {duration:F2}s, 누적: {_realtimeTotalDuration:F2}s");
            }
        }

        /// <summary>
        /// 웹캠 프레임을 비동기로 캡처하고 인코딩하여 저장 큐에 넣는 메인 루프입니다.
        /// </summary>
        private async UniTaskVoid CaptureLoopRoutine()
        {
            // 비동기 중 StartCapture 재호출로 리소스가 교체될 경우를 대비해 로컬 변수에 참조 캡처
            var captureRT = _captureRT;
            var encodeTexture = _encodeTexture;

            float timelapseInterval = 1f / timelapseCaptureFPS; 

            while (_isRecording && _webCam != null && _webCam.isPlaying)
            {
                // 리소스가 변경되었거나 해제되었으면 안전하게 루프 종료
                if (_captureRT != captureRT || _encodeTexture != encodeTexture) break;

                _timer += Time.deltaTime;
                _timelapseTimer += Time.deltaTime;

                if (_timer >= _baseInterval)
                {
                    _timer -= _baseInterval;

                    bool saveToRealtime = IsRealtimeTargetLevel(_currentLevelID);
                    bool saveToTimelapse = _timelapseTimer >= timelapseInterval;

                    if (!saveToRealtime && !saveToTimelapse)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update);
                        continue;
                    }

                    if (saveToTimelapse) _timelapseTimer = 0f;

                    await UniTask.WaitForEndOfFrame(this);

                    // 대기 후 유효성 재확인 (비동기 사이 상태 변경 체크)
                    if (_captureRT != captureRT || _encodeTexture != encodeTexture) break;
                    if (_webCam == null || !_webCam.isPlaying) break;

                    // GPU Blit 및 비동기 Readback 요청
                    Graphics.Blit(_webCam, captureRT);
                    var request = AsyncGPUReadback.Request(captureRT);
                    await request.ToUniTask();

                    // Readback 대기 후 유효성 재확인
                    if (_captureRT != captureRT || _encodeTexture != encodeTexture) break;

                    if (request.hasError) continue;

                    if (encodeTexture)
                    {
                        encodeTexture.LoadRawTextureData(request.GetData<byte>());
                        encodeTexture.Apply();

                        // JPG 인코딩 (메인 스레드 부하가 있지만 Texture2D 조작을 위해 필요)
                        byte[] bytes = encodeTexture.EncodeToJPG(70);

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

                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        private void EnqueueSave(byte[] data, string folder, int index)
        {
            string path = Path.Combine(folder, $"img_{index:D5}.jpg");
            _saveQueue.Enqueue(new SaveTaskData { path = path, data = data });
        }

        /// <summary>
        /// 별도 스레드에서 파일 저장을 처리하는 루프입니다.
        /// </summary>
        private async UniTask StartDiskWriteLoop()
        {
            _cts = new CancellationTokenSource();
            await UniTask.SwitchToThreadPool(); 

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    if (_saveQueue.TryDequeue(out var task))
                    {
                        try 
                        { 
                            File.WriteAllBytes(task.path, task.data); 
                        } 
                        catch (Exception e) 
                        {
                            // IO 예외 발생 시 로그 출력 후 계속 진행
                            Debug.LogError($"[TimeLapseRecorder] 파일 쓰기 실패 ({task.path}): {e.Message}");
                        }
                    }
                    else
                    {
                        // 빈 큐 대기 시 CPU 스핀 방지를 위해 50ms 대기
                        await UniTask.Delay(50, cancellationToken: _cts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 취소 요청(OnDestroy 등) 시 정상 종료
            }
        }

        private bool IsRealtimeTargetLevel(string levelID)
        {
            if (string.IsNullOrEmpty(levelID)) return false;
            string numPart = Regex.Replace(levelID, "[^0-9]", ""); 
            if (int.TryParse(numPart, out int num))
            {
                // Q11~Q15 레벨에서만 리얼타임 저장
                if (num >= 11 && num <= 15) return true;
            }
            return false;
        }

        // --- 변환 로직 ---

        public void ConvertToVideo()
        {
            // 타임랩스 전용 플래그 체크
            if (IsTimelapseProcessing) 
            {
                Debug.LogWarning("[TimeLapse] 이미 타임랩스 변환 작업 중입니다.");
                return;
            }

            // Lock 걸기
            IsTimelapseProcessing = true;

            float fps = 30f;
            if (_globalFrameIndex > 0 && timelapseDuration > 0)
            {
                fps = (float)_globalFrameIndex / timelapseDuration;
            }
            
            string fileName = $"{GetCombinedPlayerNames()}_Timelapse";
            Debug.Log($"[Timelapse] 변환 시작: {_globalFrameIndex}장 / {timelapseDuration}초 목표 (FPS: {fps:F2}) -> 파일명: {fileName}.mp4");
            
            ConversionSequence(_sourceImageFolderPath, _outputVideoFolderPath, fileName, fps, false).Forget();
        }

        public void ConvertToRealtimeVideo()
        {
            // 리얼타임 전용 플래그 체크
            if (IsRealtimeProcessing) 
            {
                Debug.LogWarning("[TimeLapse] 이미 리얼타임 변환 작업 중입니다.");
                return;
            }

            if (_realtimeFrameIndex <= 0)
            {
                // 녹화 대상 레벨이 아닌 경우(Q1~Q10 등), 데이터가 없는 것이 정상이므로 경고 없이 종료
                if (!IsRealtimeTargetLevel(_currentLevelID))
                {
                    return; 
                }

                Debug.LogWarning($"[Realtime] 데이터 부족으로 변환 취소. (Frame: {_realtimeFrameIndex})");
                return;
            }

            // Lock 걸기
            IsRealtimeProcessing = true;

            float fps = 30f;
            if (_realtimeFrameIndex > 0 && realtimeDuration > 0)
            {
                fps = (float)_realtimeFrameIndex / realtimeDuration;
            }
            
            string fileName = $"{GetCombinedPlayerNames()}_Realtime";
            Debug.Log($"[Realtime] 변환 시작: {_realtimeFrameIndex}장 / {realtimeDuration}초 목표 (FPS: {fps:F2}) -> 파일명: {fileName}.mp4");

            ConversionSequence(_realtimeSourcePath, _realtimeVideoPath, fileName, fps, true).Forget();
        }

        /// <summary>
        /// 외부(LevelManager 등)에서 타임아웃 발생 시 리얼타임 변환 상태를 강제 초기화합니다.
        /// </summary>
        public void ResetRealtimeProcessing()
        {
            IsRealtimeProcessing = false;
        }

        private async UniTaskVoid ConversionSequence(string sourceFolder, string outputFolder, string filePrefix, float fps, bool isRealtime)
        {
            try
            {
                // 1. 디스크 쓰기가 모두 끝날 때까지 대기
                await UniTask.WaitUntil(() => _saveQueue.IsEmpty);

                // 경로가 비어있다면 재설정 및 로컬 변수 갱신 (NullReference 방지)
                if (string.IsNullOrEmpty(sourceFolder) || string.IsNullOrEmpty(outputFolder))
                {
                    UpdatePaths();
                    
                   if (isRealtime)
                    {
                        sourceFolder = _realtimeSourcePath;
                        outputFolder = _realtimeVideoPath;
                    }
                    else
                    {
                        sourceFolder = _sourceImageFolderPath;
                        outputFolder = _outputVideoFolderPath;
                    }
                }

                bool hasSourceFiles = false;
                if (Directory.Exists(sourceFolder))
                {
                    var files = Directory.GetFiles(sourceFolder, "img_*.jpg");
                    if (files.Length > 0) hasSourceFiles = true;
                }

                string outputFileName = $"{filePrefix}.mp4";
                string outputPath = Path.Combine(outputFolder, outputFileName);

                if (!hasSourceFiles)
                {
                    // 소스는 없지만 이미 영상이 있다면 성공으로 처리 (재진입 시)
                    if (File.Exists(outputPath))
                    {
                        Debug.LogWarning($"[TimeLapseRecorder] 소스 파일이 없지만 영상이 존재하여 성공 처리함: {outputFileName}");
                        IsConversionSuccessful = true;
                        LastVideoPath = outputPath;
                        if (filePrefix.Contains("Realtime")) LastRealtimeVideoPath = outputPath;
                    }
                    else
                    {
                        Debug.LogError($"[TimeLapseRecorder] 변환 실패: 소스 이미지가 없습니다. ({sourceFolder})");
                        IsConversionSuccessful = false;
                    }
                    return; 
                }

                // 2. FFMPEG 변환 시작
                IsConversionSuccessful = false;
                LastExitCode = -1;

                string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");
                string inputPattern = Path.Combine(sourceFolder, "img_%05d.jpg");

                if (File.Exists(outputPath)) File.Delete(outputPath);

                if (fps < 1.0f) fps = 10f;
                
                // FPS 포맷팅 시 로케일 이슈(쉼표) 방지를 위해 InvariantCulture 사용
                string fpsStr = fps.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                string args = $"-framerate {fpsStr} -i \"{inputPattern}\" -c:v libx264 -pix_fmt yuv420p \"{outputPath}\"";
                
                await UniTask.SwitchToThreadPool();

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = ffmpegPath;
                    process.StartInfo.Arguments = args;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    
                    // 무한 대기 방지를 위해 타임아웃 설정 (60초)
                    // 변환이 60초 이상 걸리면 문제가 있는 것으로 간주하고 강제 종료합니다.
                    if (process.WaitForExit(60000)) 
                    {
                        LastExitCode = process.ExitCode;
                    }
                    else
                    {
                        Debug.LogError($"[TimeLapseRecorder] FFMPEG 프로세스 타임아웃 (60초 초과). 강제 종료합니다.");
                        try 
                        { 
                            process.Kill();
                            // Kill 호출 후 프로세스 리소스 정리를 위해 잠시 대기
                            process.WaitForExit(1000);
                        } 
                        catch (Exception killEx) 
                        {
                            Debug.LogWarning($"[TimeLapseRecorder] 프로세스 강제 종료 중 오류: {killEx.Message}");
                        }
                        LastExitCode = -1; // 실패 코드로 설정
                    }
                }

                await UniTask.SwitchToMainThread();

                if (LastExitCode == 0)
                {
                    IsConversionSuccessful = true;
                    LastVideoPath = outputPath;
                    if (filePrefix.Contains("Realtime")) LastRealtimeVideoPath = outputPath;
                    
                    Debug.Log($"[TimeLapseRecorder] 변환 성공: {outputPath}");
                    ClearFolder(sourceFolder); // 소스 이미지 정리
                }
                else
                {
                    Debug.LogError($"[TimeLapseRecorder] 변환 실패. ExitCode: {LastExitCode}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[TimeLapseRecorder] 예외 발생: {e.Message}");
            }
            finally
            {
                // 작업이 끝났으므로 해당 플래그 해제 (독립적 관리)
                if (isRealtime) IsRealtimeProcessing = false;
                else IsTimelapseProcessing = false;
            }
        }
        
        private string GetCombinedPlayerNames()
        {
            string nameA = "PlayerA";
            string nameB = "PlayerB";

            if (GameManager.Instance != null)
            {
                nameA = GameManager.Instance.PlayerALastName;
                nameB = GameManager.Instance.PlayerBLastName;
            }

            string combined = $"{nameA}{nameB}";
            string clean = combined.Replace("\n", "").Replace("\r", "").Trim();
            
            // 윈도우 파일명에 쓸 수 없는 특수문자 제거
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            return Regex.Replace(clean, invalidRegStr, "");
        }

        private void OnDestroy()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                
                // 백그라운드 태스크가 종료될 때까지 대기 (ObjectDisposedException 방지)
                // OnDestroy는 동기 메서드이므로, 짧은 시간 동안 폴링하며 완료를 기다립니다.
                long timeoutTicks = DateTime.Now.Ticks + 1000 * 10000; // 최대 1초 대기
                while (!_diskWriteTask.GetAwaiter().IsCompleted && DateTime.Now.Ticks < timeoutTicks)
                {
                    Thread.Sleep(10); // 메인 스레드 블로킹 최소화
                }

                _cts.Dispose();
            }
            
            if (_captureRT != null) _captureRT.Release();
            if (_encodeTexture != null) Destroy(_encodeTexture);
        }
    }
}