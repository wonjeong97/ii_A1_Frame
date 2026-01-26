using System.Collections;
using System.IO;
using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

/// <summary> 타임랩스 녹화 및 영상 변환 관리자 </summary>
public class TimeLapseRecorder : MonoBehaviour
{
    public static TimeLapseRecorder Instance; // 싱글톤 인스턴스

    [Header("Settings")]
    [Tooltip("초당 몇 장을 찍을지 설정 (15 권장)")]
    public float captureFPS = 15f; // 캡처 FPS
    
    public int captureWidth = 1280; // 캡처 너비
    public int captureHeight = 720; // 캡처 높이

    private WebCamTexture _webCam; // 웹캠 참조
    private bool _isRecording; // 녹화 중 여부
    private string _saveFolderPath; // 저장 폴더 경로
    private int _globalFrameIndex; // 프레임 인덱스
    
    private float _timer; // 타이머
    private float _captureInterval; // 캡처 간격
    private Texture2D _tempTexture; // 임시 텍스처
    
    private Process _ffmpegProcess; // FFmpeg 프로세스
    
    // 외부 확인용 프로퍼티
    public bool IsConverting => IsProcessing; // 변환 중 여부 (Alias)
    public bool IsProcessing { get; private set; } // 프로세스 실행 중 여부
    public bool IsConversionSuccessful { get; private set; } // 변환 성공 여부
    public int LastExitCode { get; private set; } // 마지막 종료 코드

    /// <summary> 초기화 및 저장 폴더 생성 </summary>
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 저장 경로 설정
            string dataPath = Application.dataPath;
            DirectoryInfo parentDir = Directory.GetParent(dataPath);
            string rootPath = (parentDir != null) ? parentDir.FullName : dataPath;
            
            _saveFolderPath = Path.Combine(rootPath, "Timelapse");

            // 폴더 생성 시도
            try
            {
                if (!Directory.Exists(_saveFolderPath))
                {
                    Directory.CreateDirectory(_saveFolderPath);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TimeLapseRecorder] 폴더 생성 실패. 비활성화.\n에러: {e.Message}");
                this.enabled = false;
                return;
            }
            
            // FPS 설정
            if (captureFPS > 0)  _captureInterval = 1f / captureFPS;
            else
            {
                Debug.LogWarning("[TimeLapseRecorder] FPS 0 이하. 기본값 15 설정");
                captureFPS = 15f;
                _captureInterval = 1f / captureFPS;
            }
        }
        else Destroy(gameObject);
    }

    /// <summary> 이전 녹화 데이터 삭제 및 초기화 </summary>
    public void ClearRecordingData()
    {
        _globalFrameIndex = 0;
        IsProcessing = false;
        IsConversionSuccessful = false;
        
        if (Directory.Exists(_saveFolderPath))
        {
            try
            {
                string[] files = Directory.GetFiles(_saveFolderPath);
                foreach (string file in files)
                {
                    try { File.Delete(file); } catch { }
                }
                Debug.Log("[TimeLapseRecorder] 데이터 초기화 완료");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TimeLapseRecorder] 초기화 실패: {e.Message}");
            }
        }
    }

    /// <summary> 캡처 시작 (웹캠 연결) </summary>
    public void StartCapture(WebCamTexture cam)
    {
        if (!this.enabled) 
        {
            Debug.LogError("[TimeLapseRecorder] 비활성 상태 (초기화 실패)");
            return;
        }

        _webCam = cam;
        _isRecording = true;
        _timer = 0f;

        if (captureFPS > 0) _captureInterval = 1f / captureFPS;
        
        // 텍스처 초기화
        if (_tempTexture == null || _tempTexture.width != captureWidth || _tempTexture.height != captureHeight)
            _tempTexture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
    }

    /// <summary> 캡처 중지 </summary>
    public void StopCapture()
    {
        _isRecording = false;
        _webCam = null;
    }

    /// <summary> 프레임 타이머 갱신 </summary>
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

    /// <summary> 프레임 캡처 및 파일 저장 코루틴 </summary>
    private IEnumerator CaptureFrameRoutine()
    {
        yield return new WaitForEndOfFrame();

        if (_webCam != null && _webCam.isPlaying)
        {
            // RT 생성 및 블릿
            RenderTexture rt = RenderTexture.GetTemporary(captureWidth, captureHeight);
            Graphics.Blit(_webCam, rt);
            
            // 텍스처 읽기
            RenderTexture.active = rt;
            _tempTexture.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            _tempTexture.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            // JPG 인코딩
            byte[] bytes = _tempTexture.EncodeToJPG(70); 
            
            // 파일명 생성
            string fileName = $"img_{_globalFrameIndex:D5}.jpg"; 
            string path = Path.Combine(_saveFolderPath, fileName);
            
            // 파일 저장
            try
            {
                File.WriteAllBytes(path, bytes);
                _globalFrameIndex++; // 성공 시 인덱스 증가
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TimeLapseRecorder] 저장 실패: {e.Message}");
            }
        }
    }

    /// <summary> 저장 폴더 열기 </summary>
    public void OpenFolder()
    {
        Application.OpenURL($"file://{_saveFolderPath}");
    }
    
    /// <summary> FFmpeg 영상 변환 실행 </summary>
    public void ConvertToVideo()
    {
        string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe"); 
        
        if (!File.Exists(ffmpegPath))
        {
            Debug.LogError($"[FFmpeg] 실행 파일 없음: {ffmpegPath}");
            return;
        }
        
        string inputPattern = Path.Combine(_saveFolderPath, "img_%05d.jpg");
        string outputPath = Path.Combine(_saveFolderPath, "Final_Timelapse.mp4");
        
        // 기존 파일 정리
        try
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FFmpeg] 파일 삭제 실패: {e.Message}");
        }

        Debug.Log($"[FFmpeg] 변환 시작: {inputPattern} -> {outputPath}");
        
        // 인자 설정 (30fps 인코딩)
        string args = $"-framerate 30 -i \"{inputPattern}\" -c:v libx264 -pix_fmt yuv420p \"{outputPath}\"";
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // 프로세스 시작
        try 
        {
            _ffmpegProcess = Process.Start(startInfo);

            if (_ffmpegProcess != null)
            {
                StartCoroutine(WaitForFFmpegRoutine(_ffmpegProcess));
            }
            else
            {
                Debug.LogError("[TimeLapseRecorder] 프로세스 시작 실패 (null)");
                IsProcessing = false;
                IsConversionSuccessful = false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TimeLapseRecorder] 실행 예외: {e.Message}");
            IsProcessing = false;
            IsConversionSuccessful = false;
        }
    }

    /// <summary> 변환 프로세스 대기 코루틴 </summary>
    private IEnumerator WaitForFFmpegRoutine(Process process)
    {
        IsProcessing = true;
        IsConversionSuccessful = false;
        LastExitCode = -1;

        // 종료 대기
        while (!process.HasExited)
        {
            yield return null;
        }

        LastExitCode = process.ExitCode;
        IsProcessing = false;
        _ffmpegProcess = null;

        // 결과 확인
        if (LastExitCode == 0)
        {
            IsConversionSuccessful = true;
            Debug.Log("[TimeLapseRecorder] 변환 성공");
        }
        else
        {
            IsConversionSuccessful = false;
            Debug.LogError($"[TimeLapseRecorder] 변환 실패. 코드: {LastExitCode}");
        }
    }
}