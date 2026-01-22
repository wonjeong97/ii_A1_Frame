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
    public bool IsConverting => _ffmpegProcess != null && !_ffmpegProcess.HasExited;

    // 외부에서 FFmpeg 상태를 확인할 수 있는 프로퍼티
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

            if (!Directory.Exists(_saveFolderPath))
            {
                Directory.CreateDirectory(_saveFolderPath);
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
            string[] files = Directory.GetFiles(_saveFolderPath);
            foreach (string file in files)
            {
                try { File.Delete(file); } catch { }
            }
        }
        UnityEngine.Debug.Log("[TimeLapseRecorder] 이전 데이터 초기화 완료");
    }

    public void StartCapture(WebCamTexture cam)
    {
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
            int frameIndex = _globalFrameIndex;
            _globalFrameIndex++;
            StartCoroutine(CaptureFrameRoutine(frameIndex));
        }
    }

    private IEnumerator CaptureFrameRoutine(int frameIndex)
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
            
            string fileName = $"img_{frameIndex:D5}.jpg"; 
            string path = Path.Combine(_saveFolderPath, fileName);
            
            File.WriteAllBytes(path, bytes);
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
            UnityEngine.Debug.LogError($"[FFmpeg] ffmpeg.exe를 찾을 수 없습니다: {ffmpegPath}");
            return;
        }
        
        string inputPattern = Path.Combine(_saveFolderPath, "img_%05d.jpg");
        string outputPath = Path.Combine(_saveFolderPath, "Final_Timelapse.mp4");
        if (File.Exists(outputPath)) File.Delete(outputPath);
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
        _ffmpegProcess = Process.Start(startInfo);
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