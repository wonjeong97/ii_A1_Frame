using System.Collections;
using System.IO;
using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class TimeLapseRecorder : MonoBehaviour
{
    public static TimeLapseRecorder Instance;

    [Header("Settings")]
    [Tooltip("초당 몇 장을 찍을지 설정 (15 권장)")]
    public float captureFPS = 15f; 
    
    public int captureWidth = 1280; 
    public int captureHeight = 720;

    private WebCamTexture _webCam;
    private bool _isRecording;
    private string _saveFolderPath;
    private int _globalFrameIndex; 
    
    private float _timer;
    private float _captureInterval;
    private Texture2D _tempTexture;
    
    private Process _ffmpegProcess;
    
    // 외부 확인용 프로퍼티
    public bool IsConverting => IsProcessing; 
    public bool IsProcessing { get; private set; }
    public bool IsConversionSuccessful { get; private set; }
    public int LastExitCode { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            string dataPath = Application.dataPath;
            DirectoryInfo parentDir = Directory.GetParent(dataPath);
            string rootPath = (parentDir != null) ? parentDir.FullName : dataPath;
            
            _saveFolderPath = Path.Combine(rootPath, "Timelapse");

            // [수정] 폴더 생성 실패 시 치명적 오류 처리
            try
            {
                if (!Directory.Exists(_saveFolderPath))
                {
                    Directory.CreateDirectory(_saveFolderPath);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TimeLapseRecorder] 저장 폴더 생성 불가. 스크립트를 비활성화합니다.\n경로: {_saveFolderPath}\n에러: {e.Message}");
                this.enabled = false;
                return;
            }
            
            if (captureFPS > 0)  _captureInterval = 1f / captureFPS;
            else
            {
                Debug.LogWarning("[TimeLapseRecorder] captureFPS가 0 이하입니다. 기본값 15로 설정합니다.");
                captureFPS = 15f;
                _captureInterval = 1f / captureFPS;
            }
        }
        else Destroy(gameObject);
    }

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
                Debug.Log("[TimeLapseRecorder] 이전 데이터 초기화 완료");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TimeLapseRecorder] 데이터 초기화 실패: {e.Message}");
            }
        }
    }

    public void StartCapture(WebCamTexture cam)
    {
        // Awake에서 폴더 생성 실패했다면 실행 차단
        if (!this.enabled) 
        {
            Debug.LogError("[TimeLapseRecorder] 컴포넌트 비활성화됨 (초기화 실패)");
            return;
        }

        _webCam = cam;
        _isRecording = true;
        _timer = 0f;

        if (captureFPS > 0) _captureInterval = 1f / captureFPS;
        
        if (_tempTexture == null || _tempTexture.width != captureWidth || _tempTexture.height != captureHeight)
            _tempTexture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
    }

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

    private IEnumerator CaptureFrameRoutine()
    {
        yield return new WaitForEndOfFrame();

        if (_webCam != null && _webCam.isPlaying)
        {
            RenderTexture rt = RenderTexture.GetTemporary(captureWidth, captureHeight);
            Graphics.Blit(_webCam, rt);
            
            RenderTexture.active = rt;
            _tempTexture.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            _tempTexture.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            byte[] bytes = _tempTexture.EncodeToJPG(70); 
            
            // 현재 인덱스로 파일명 생성
            string fileName = $"img_{_globalFrameIndex:D5}.jpg"; 
            string path = Path.Combine(_saveFolderPath, fileName);
            
            // 파일 쓰기 예외 처리 및 인덱스 증가
            try
            {
                File.WriteAllBytes(path, bytes);
                _globalFrameIndex++; // 성공 시에만 증가 (파일명 연속성 보장)
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TimeLapseRecorder] 프레임 저장 실패: {e.Message}");
            }
        }
    }

    public void OpenFolder()
    {
        Application.OpenURL($"file://{_saveFolderPath}");
    }
    
    // FFmpeg 프로세스를 실행하고 모니터링 코루틴 시작
    public void ConvertToVideo()
    {
        string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe"); 
        
        if (!File.Exists(ffmpegPath))
        {
            Debug.LogError($"[FFmpeg] ffmpeg.exe를 찾을 수 없습니다: {ffmpegPath}");
            return;
        }
        
        string inputPattern = Path.Combine(_saveFolderPath, "img_%05d.jpg");
        string outputPath = Path.Combine(_saveFolderPath, "Final_Timelapse.mp4");
        
        try
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FFmpeg] 기존 파일 삭제 실패: {e.Message}");
        }

        Debug.Log($"[FFmpeg] 변환 시작: {inputPattern} -> {outputPath}");
        
        // 30fps로 영상을 만듦 (15fps로 찍었으니 2배속 영상이 됨)
        string args = $"-framerate 30 -i \"{inputPattern}\" -c:v libx264 -pix_fmt yuv420p \"{outputPath}\"";
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // ------------------------------------------------------------------
        // [수정] 프로세스 시작 방어 코드 추가
        // ------------------------------------------------------------------
        try 
        {
            _ffmpegProcess = Process.Start(startInfo);

            if (_ffmpegProcess != null)
            {
                // 프로세스가 정상적으로 시작된 경우에만 코루틴 시작
                StartCoroutine(WaitForFFmpegRoutine(_ffmpegProcess));
            }
            else
            {
                // 프로세스 시작 실패 (null 반환)
                Debug.LogError("[TimeLapseRecorder] FFmpeg 프로세스 시작 실패 (null 반환)");
                IsProcessing = false;
                IsConversionSuccessful = false;
            }
        }
        catch (System.Exception e)
        {
            // 프로세스 실행 중 예외 발생 (파일 없음, 권한 문제 등)
            Debug.LogError($"[TimeLapseRecorder] FFmpeg 실행 중 예외 발생: {e.Message}");
            IsProcessing = false;
            IsConversionSuccessful = false;
        }
    }

    // 프로세스 완료 대기 코루틴
    private IEnumerator WaitForFFmpegRoutine(Process process)
    {
        IsProcessing = true;
        IsConversionSuccessful = false;
        LastExitCode = -1;

        // 프로세스가 끝날 때까지 대기
        while (!process.HasExited)
        {
            yield return null;
        }

        LastExitCode = process.ExitCode;
        IsProcessing = false;
        _ffmpegProcess = null; // 참조 해제

        if (LastExitCode == 0)
        {
            IsConversionSuccessful = true;
            Debug.Log("[TimeLapseRecorder] FFmpeg 변환 성공");
        }
        else
        {
            IsConversionSuccessful = false;
            Debug.LogError($"[TimeLapseRecorder] FFmpeg 변환 실패. ExitCode: {LastExitCode}");
        }
    }
}