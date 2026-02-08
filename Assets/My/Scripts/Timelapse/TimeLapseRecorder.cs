using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering; 
using Cysharp.Threading.Tasks; 
using Wonjeong.Utils;
using Debug = UnityEngine.Debug;

namespace My.Scripts.Timelapse
{
    public class TimeLapseRecorder : MonoBehaviour
    {
        public static TimeLapseRecorder Instance;

        [Header("Capture Settings")]
        public int realtimeCaptureFPS = 30; 
        public int timelapseCaptureFPS = 15; 
        
        [Header("Output Settings")]
        public float timelapseDuration = 20f; 
        public float realtimeDuration = 30f;  
        
        public int captureWidth = 1280;     
        public int captureHeight = 720;     

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
        
        private float _timelapseTimer = 0f;

        public bool IsConverting => IsProcessing;       
        public bool IsProcessing { get; private set; }  
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
            StartDiskWriteLoop().Forget();
        }

        public void SetCurrentLevel(string levelID)
        {
            _currentLevelID = levelID;
            bool isTarget = IsRealtimeTargetLevel(levelID);
            
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

        public void ClearRecordingData()
        {
            _globalFrameIndex = 0;
            _realtimeFrameIndex = 0;
            IsProcessing = false;
            IsConversionSuccessful = false;
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

        private async UniTaskVoid CaptureLoopRoutine()
        {
            float timelapseInterval = 1f / timelapseCaptureFPS; 

            while (_isRecording && _webCam != null && _webCam.isPlaying)
            {
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

                    if (_webCam == null || !_webCam.isPlaying) break;

                    Graphics.Blit(_webCam, _captureRT);
                    var request = AsyncGPUReadback.Request(_captureRT);
                    await request.ToUniTask();

                    if (request.hasError) continue;

                    if (_encodeTexture != null)
                    {
                        _encodeTexture.LoadRawTextureData(request.GetData<byte>());
                        _encodeTexture.Apply();

                        byte[] bytes = _encodeTexture.EncodeToJPG(70);

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

        private async UniTaskVoid StartDiskWriteLoop()
        {
            _cts = new CancellationTokenSource();
            await UniTask.SwitchToThreadPool(); 

            while (!_cts.IsCancellationRequested)
            {
                if (_saveQueue.TryDequeue(out var task))
                {
                    try { File.WriteAllBytes(task.path, task.data); } catch { }
                }
                else
                {
                    await UniTask.Delay(1, cancellationToken: _cts.Token);
                }
            }
        }

        private bool IsRealtimeTargetLevel(string levelID)
        {
            if (string.IsNullOrEmpty(levelID)) return false;
            string numPart = Regex.Replace(levelID, "[^0-9]", ""); 
            if (int.TryParse(numPart, out int num))
            {
                if ((num >= 1 && num <= 5) || (num >= 11 && num <= 15)) return true;
            }
            return false;
        }

        // --- 변환 로직 (수정됨) ---

        public void ConvertToVideo()
        {
            if (IsProcessing) 
            {
                Debug.LogWarning("[TimeLapse] 이미 다른 변환 작업 중입니다.");
                return;
            }

            // [수정] 호출 즉시 Lock을 걸어서 LevelManager가 대기하도록 함
            IsProcessing = true;

            float fps = 30f;
            if (_globalFrameIndex > 0 && timelapseDuration > 0)
            {
                fps = (float)_globalFrameIndex / timelapseDuration;
            }
            Debug.Log($"[Timelapse] 변환 시작: {_globalFrameIndex}장 / {timelapseDuration}초 목표 (FPS: {fps:F2})");
            
            ConversionSequence(_sourceImageFolderPath, _outputVideoFolderPath, "Test_Timelapse", fps).Forget();
        }

        public void ConvertToRealtimeVideo()
        {
            if (IsProcessing) 
            {
                Debug.LogWarning("[TimeLapse] 이미 다른 변환 작업 중입니다.");
                return;
            }

            if (_realtimeFrameIndex <= 0)
            {
                Debug.LogWarning($"[Realtime] 데이터 부족으로 변환 취소. (Frame: {_realtimeFrameIndex})");
                return;
            }

            // [수정] 호출 즉시 Lock! (매우 중요)
            IsProcessing = true;

            float fps = 30f;
            if (_realtimeFrameIndex > 0 && realtimeDuration > 0)
            {
                fps = (float)_realtimeFrameIndex / realtimeDuration;
            }
            Debug.Log($"[Realtime] 변환 시작: {_realtimeFrameIndex}장 / {realtimeDuration}초 목표 (FPS: {fps:F2})");

            ConversionSequence(_realtimeSourcePath, _realtimeVideoPath, "Test_Realtime", fps).Forget();
        }

       private async UniTaskVoid ConversionSequence(string sourceFolder, string outputFolder, string filePrefix, float fps)
        {
            try
            {
                // 1. 저장 대기
                await UniTask.WaitUntil(() => _saveQueue.IsEmpty);

                if (string.IsNullOrEmpty(sourceFolder)) UpdatePaths();

                // [수정] 중요! 소스 파일이 존재하는지 먼저 확인
                // (이전 로직이 원본 영상을 지워버리는 참사를 방지)
                bool hasSourceFiles = false;
                if (Directory.Exists(sourceFolder))
                {
                    var files = Directory.GetFiles(sourceFolder, "img_*.jpg");
                    if (files.Length > 0) hasSourceFiles = true;
                }

                string outputFileName = $"{filePrefix}.mp4";
                string outputPath = Path.Combine(outputFolder, outputFileName);

                // 소스 파일이 없는 경우 처리
                if (!hasSourceFiles)
                {
                    // 소스는 없지만 이미 만들어진 영상이 있다면 성공으로 간주
                    if (File.Exists(outputPath))
                    {
                        Debug.LogWarning($"[TimeLapseRecorder] 소스 파일은 없지만 영상이 이미 존재하여 성공 처리함: {outputFileName}");
                        IsConversionSuccessful = true;
                        LastVideoPath = outputPath;
                        if (filePrefix.Contains("Realtime")) LastRealtimeVideoPath = outputPath;
                    }
                    else
                    {
                        Debug.LogError($"[TimeLapseRecorder] 변환 실패: 소스 이미지가 없습니다. ({sourceFolder})");
                        IsConversionSuccessful = false;
                    }
                    // 여기서 작업 종료 (파일 삭제 로직 실행 안 함)
                    return; 
                }

                // 2. 정상 진행: (소스가 있으므로) 기존 파일 삭제 후 새로 변환 시작
                IsConversionSuccessful = false;
                LastExitCode = -1;

                string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");
                string inputPattern = Path.Combine(sourceFolder, "img_%05d.jpg");

                if (File.Exists(outputPath)) File.Delete(outputPath);

                if (fps < 1.0f) fps = 10f;

                string args = $"-framerate {fps:F2} -i \"{inputPattern}\" -c:v libx264 -pix_fmt yuv420p \"{outputPath}\"";
                
                await UniTask.SwitchToThreadPool();

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = ffmpegPath;
                    process.StartInfo.Arguments = args;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    process.WaitForExit(); 
                    LastExitCode = process.ExitCode;
                }

                await UniTask.SwitchToMainThread();

                if (LastExitCode == 0)
                {
                    IsConversionSuccessful = true;
                    LastVideoPath = outputPath;
                    if (filePrefix.Contains("Realtime")) LastRealtimeVideoPath = outputPath;
                    
                    Debug.Log($"[TimeLapseRecorder] 변환 성공: {outputPath}");
                    ClearFolder(sourceFolder);
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
                IsProcessing = false;
            }
        }

        private void OnDestroy()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
            }
            if (_captureRT != null) _captureRT.Release();
            if (_encodeTexture != null) Destroy(_encodeTexture);
        }
    }
}