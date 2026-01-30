using System;
using System.Collections;
using System.IO;
using UnityEngine;
using System.Diagnostics;
using Wonjeong.Utils;
using Debug = UnityEngine.Debug;

/// <summary> 
/// 타임랩스 녹화 및 영상 변환 관리자 
/// <para>기능: 웹캠 프레임 캡처, 날짜별 폴더 관리, FFmpeg 인코딩, 원본 이미지 정리</para>
/// </summary>
public class TimeLapseRecorder : MonoBehaviour
{
    public static TimeLapseRecorder Instance;

    [Header("Capture Settings")]
    public float captureFPS = 15f;      // 초당 캡처 프레임 수
    public int captureWidth = 1280;     // 캡처 해상도 너비
    public int captureHeight = 720;     // 캡처 해상도 높이

    private WebCamTexture _webCam;      // 웹캠 텍스처 참조
    private bool _isRecording;          // 녹화 진행 여부
    
    // --- 경로 관리 변수 ---
    private string _rootPath;               // 프로젝트 루트 경로 (Assets 상위)
    private string _currentDateFolder;      // 현재 날짜 폴더명 (yyyy-MM-dd)
    private string _sourceImageFolderPath;  // 캡처된 이미지 저장 경로 (Timelapse_Source/Date)
    private string _outputVideoFolderPath;  // 완성된 영상 저장 경로 (Timelapse_Video/Date)
    
    private int _globalFrameIndex;      // 프레임 번호 (파일명 생성용)
    private float _timer;               // 캡처 주기 타이머
    private float _captureInterval;     // 캡처 간격 (1/FPS)
    private Texture2D _tempTexture;     // 캡처용 임시 텍스처
    private Process _ffmpegProcess;     // FFmpeg 외부 프로세스

    // --- 상태 확인 프로퍼티 ---
    public bool IsConverting => IsProcessing;       // 변환 중 여부 (외부용)
    public bool IsProcessing { get; private set; }  // 현재 작업 진행 중 여부
    public bool IsConversionSuccessful { get; private set; } // 변환 성공 여부
    public int LastExitCode { get; private set; }   // FFmpeg 종료 코드
    
    /// <summary> 마지막으로 생성된 비디오 파일의 전체 경로 </summary>
    public string LastVideoPath { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 데이터 경로 설정 (Assets 폴더 상위 경로)
            string dataPath = Application.dataPath;
            DirectoryInfo parentDir = Directory.GetParent(dataPath);
            _rootPath = (parentDir != null) ? parentDir.FullName : dataPath;
            
            // 캡처 간격 계산
            if (captureFPS > 0)  _captureInterval = 1f / captureFPS;
            else
            {
                captureFPS = 15f;
                _captureInterval = 1f / captureFPS;
            }
        }
        else Destroy(gameObject);
    }

    /// <summary> 
    /// 녹화 시작 시 호출되어 저장 경로를 갱신하고 폴더를 생성합니다.
    /// <para>구조: Root/Timelapse_Source(or Video)/yyyy-MM-dd/</para>
    /// </summary>
    private void UpdatePaths()
    {
        // 1. 날짜 기준 폴더명 갱신
        _currentDateFolder = DateTime.Now.ToString("yyyy-MM-dd");

        // 2. 소스 이미지 폴더 경로 설정
        _sourceImageFolderPath = Path.Combine(_rootPath, "Timelapse_Source", _currentDateFolder);
        
        // 3. 결과 영상 폴더 경로 설정
        _outputVideoFolderPath = Path.Combine(_rootPath, "Timelapse_Video", _currentDateFolder);

        // 4. 실제 폴더 생성
        try
        {
            if (!Directory.Exists(_sourceImageFolderPath)) Directory.CreateDirectory(_sourceImageFolderPath);
            if (!Directory.Exists(_outputVideoFolderPath)) Directory.CreateDirectory(_outputVideoFolderPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[TimeLapseRecorder] 폴더 생성 실패: {e.Message}");
        }
    }

    /// <summary> 이전 녹화 데이터를 초기화합니다. (메모리 및 변수 리셋) </summary>
    public void ClearRecordingData()
    {
        _globalFrameIndex = 0;
        IsProcessing = false;
        IsConversionSuccessful = false;
        LastVideoPath = string.Empty;

        // 주의: 현재 날짜의 '소스 이미지' 폴더 내용만 비웁니다. (영상은 유지)
        if (!string.IsNullOrEmpty(_sourceImageFolderPath) && Directory.Exists(_sourceImageFolderPath))
        {
            try
            {
                string[] files = Directory.GetFiles(_sourceImageFolderPath);
                foreach (string file in files) File.Delete(file);
                Debug.Log("[TimeLapseRecorder] 소스 이미지 데이터 초기화 완료");
            }
            catch (Exception e)
            {
                Debug.LogError($"[TimeLapseRecorder] 초기화 실패: {e.Message}");
            }
        }
    }

    /// <summary> 웹캠 캡처를 시작합니다. </summary>
    public void StartCapture(WebCamTexture cam)
    {
        if (!this.enabled) return;

        // 캡처 시작 시점에 날짜 및 경로 갱신
        UpdatePaths();

        _webCam = cam;
        _isRecording = true;
        _timer = 0f;

        if (captureFPS > 0) _captureInterval = 1f / captureFPS;
        
        // 텍스처 메모리 할당
        if (_tempTexture == null || _tempTexture.width != captureWidth || _tempTexture.height != captureHeight)
        {
            if (_tempTexture != null) Destroy(_tempTexture);
            _tempTexture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
        }
    }

    /// <summary> 캡처를 중단합니다. </summary>
    public void StopCapture()
    {
        _isRecording = false;
        _webCam = null;
    }

    private void Update()
    {
        if (!_isRecording || _webCam == null || !_webCam.isPlaying) return;

        _timer += Time.deltaTime;

        if (_timer >= _captureInterval)
        {
            _timer -= _captureInterval;
            StartCoroutine(CaptureFrameRoutine());
        }
    }

    /// <summary> 프레임 캡처 및 JPG 저장 코루틴 </summary>
    private IEnumerator CaptureFrameRoutine()
    {
        // 최적화된 WaitForEndOfFrame 사용
        yield return CoroutineData.WaitForEndOfFrame;
        
        if (_webCam != null && _webCam.isPlaying)
        {
            // 1. RenderTexture 생성 및 블릿
            RenderTexture rt = RenderTexture.GetTemporary(captureWidth, captureHeight);
            Graphics.Blit(_webCam, rt);
            
            // 2. 텍스처 읽기
            RenderTexture.active = rt;
            _tempTexture.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            _tempTexture.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            // 3. 인덱스 증가 및 JPG 인코딩
            int frameIndex = System.Threading.Interlocked.Increment(ref _globalFrameIndex) - 1;
            byte[] bytes = _tempTexture.EncodeToJPG(70); 
            
            // 4. 소스 이미지 폴더에 저장 (img_00001.jpg 형식)
            string fileName = $"img_{frameIndex:D5}.jpg"; 
            string path = Path.Combine(_sourceImageFolderPath, fileName);
            
            try
            {
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"[TimeLapseRecorder] 저장 실패: {e.Message}");
            }
        }
    }

    /// <summary> 저장 폴더 열기 (디버깅용) </summary>
    public void OpenFolder()
    {
        string target = Directory.Exists(_outputVideoFolderPath) ? _outputVideoFolderPath : _rootPath;
        Application.OpenURL($"file://{target}");
    }
    
    /// <summary> 수집된 이미지를 FFmpeg를 사용하여 영상으로 변환합니다. </summary>
    public void ConvertToVideo()
    {
        if (IsProcessing) return;

        // 경로 재확인 (혹시 캡처 없이 바로 변환 시도 시 대비)
        if (string.IsNullOrEmpty(_sourceImageFolderPath)) UpdatePaths();

        string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe"); 
        
        if (!File.Exists(ffmpegPath))
        {
            Debug.LogError($"[FFmpeg] 실행 파일 없음: {ffmpegPath}");
            return;
        }
        
        // 입력 패턴: 소스 폴더의 img_XXXXX.jpg
        string inputPattern = Path.Combine(_sourceImageFolderPath, "img_%05d.jpg");
        
        // 출력 파일: 비디오 폴더의 Timelapse_시간.mp4
        string outputFileName = $"Timelapse_{DateTime.Now:HHmmss}.mp4";
        string outputPath = Path.Combine(_outputVideoFolderPath, outputFileName);
        
        // 결과 경로 저장 (EndingPage에서 재생하기 위해 사용)
        LastVideoPath = outputPath;

        // 기존 파일 존재 시 삭제
        try
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FFmpeg] 기존 파일 삭제 실패: {e.Message}");
        }

        Debug.Log($"[FFmpeg] 변환 시작\nInput: {inputPattern}\nOutput: {outputPath}");
        
        // FFmpeg 명령어 구성
        string args = $"-framerate 30 -i \"{inputPattern}\" -c:v libx264 -pix_fmt yuv420p \"{outputPath}\"";
        
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try 
        {
            IsProcessing = true;
            IsConversionSuccessful = false;
            LastExitCode = -1;
            _ffmpegProcess = Process.Start(startInfo);
            
            if (_ffmpegProcess != null)
            {
                StartCoroutine(WaitForFFmpegRoutine(_ffmpegProcess));
            }
            else
            {
                IsProcessing = false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[TimeLapseRecorder] 실행 예외: {e.Message}");
            IsProcessing = false;
        }
    }

    /// <summary> FFmpeg 프로세스 완료 대기 코루틴 </summary>
    private IEnumerator WaitForFFmpegRoutine(Process process)
    {
        IsProcessing = true;
        IsConversionSuccessful = false;
        LastExitCode = -1;

        // 프로세스 종료 대기
        while (!process.HasExited)
        {
            yield return null;
        }

        LastExitCode = process.ExitCode;
        IsProcessing = false;
        process.Dispose();
        _ffmpegProcess = null;

        if (LastExitCode == 0)
        {
            IsConversionSuccessful = true;
            Debug.Log($"[TimeLapseRecorder] 변환 성공: {LastVideoPath}");
            
            // [중요] 변환 성공 시 원본 소스 이미지 정리 (용량 확보)
            CleanupSourceImages();
        }
        else
        {
            IsConversionSuccessful = false;
            Debug.LogError($"[TimeLapseRecorder] 변환 실패. 코드: {LastExitCode}");
        }
    }

    /// <summary> 소스 이미지 폴더의 모든 파일을 삭제합니다. </summary>
    private void CleanupSourceImages()
    {
        if (Directory.Exists(_sourceImageFolderPath))
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(_sourceImageFolderPath);
                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }
                Debug.Log("[TimeLapseRecorder] 소스 이미지 정리 완료 (Cleanup)");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TimeLapseRecorder] 소스 정리 중 오류: {e.Message}");
            }
        }
    }

    private void OnDestroy()
    {
        if (_tempTexture != null)
        {
            Destroy(_tempTexture);
            _tempTexture = null;
        }
        
        if (_ffmpegProcess != null)
        {
            try
            {
                if (!_ffmpegProcess.HasExited) _ffmpegProcess.Kill();
                _ffmpegProcess.Dispose();
            }
            catch { }
            _ffmpegProcess = null;
        }
        
        if (Instance == this)  Instance = null;
    }
}