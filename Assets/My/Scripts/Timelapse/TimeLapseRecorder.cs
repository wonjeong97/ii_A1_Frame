using System.Collections;
using System.IO;
using UnityEngine;
using System.Diagnostics;

public class TimeLapseRecorder : MonoBehaviour
{
    public static TimeLapseRecorder Instance;

    [Header("Settings")]
    // [수정] 프레임 간격(int) 대신 초당 촬영 횟수(float)로 변경
    // 15로 설정 시: 1초에 15장 저장 -> 45초 촬영 시 675장 -> 30fps 영상으로 만들면 약 22.5초 분량
    [Tooltip("초당 몇 장을 찍을지 설정 (15 권장)")]
    public float captureFPS = 15f; 
    
    public int captureWidth = 1280; 
    public int captureHeight = 720;

    private WebCamTexture _webCam;
    private bool _isRecording;
    private string _saveFolderPath;
    private int _globalFrameIndex = 0; 
    
    // [추가] 시간 누적용 변수
    private float _timer = 0f;
    private float _captureInterval; // 1 / captureFPS

    private Texture2D _tempTexture;

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
            
            // 인터벌 계산 (예: 1/15 = 0.066초마다 촬영)
            if (captureFPS > 0) _captureInterval = 1f / captureFPS;
        }
        else Destroy(gameObject);
    }

    public void ClearRecordingData()
    {
        _globalFrameIndex = 0;
        
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
        _timer = 0f; // 타이머 초기화

        // 설정값 갱신
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

        // [핵심 수정] 프레임 카운트가 아닌 실제 시간(Time.deltaTime)을 누적하여 체크
        _timer += Time.deltaTime;

        if (_timer >= _captureInterval)
        {
            // 타이머에서 인터벌만큼 뺌 (0으로 초기화하면 오차가 누적될 수 있음)
            _timer -= _captureInterval;
            
            StartCoroutine(CaptureFrameRoutine());
        }
    }

    private IEnumerator CaptureFrameRoutine()
    {
        // 이미 프레임 끝을 기다렸다가 캡처하는 방식이므로, 
        // Update 타이밍과 렌더링 타이밍을 맞추기 위해 WaitForEndOfFrame 유지
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

            // 화질 70 유지
            byte[] bytes = _tempTexture.EncodeToJPG(70); 
            
            string fileName = $"img_{_globalFrameIndex:D5}.jpg"; 
            string path = Path.Combine(_saveFolderPath, fileName);
            
            // 비동기 파일 쓰기 (File.WriteAllBytesAsync)가 있다면 좋겠지만, 
            // 유니티 버전에 따라 호환성이 다르므로 동기 방식 유지하되
            // 1초에 15번 정도면 요즘 SSD에서는 부하가 거의 없음
            File.WriteAllBytes(path, bytes);
            _globalFrameIndex++;
        }
    }

    public void OpenFolder()
    {
        Application.OpenURL($"file://{_saveFolderPath}");
    }
    
    public void ConvertToVideo()
    {
        string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe"); 
        string inputPattern = Path.Combine(_saveFolderPath, "img_%05d.jpg");
        string outputPath = Path.Combine(_saveFolderPath, "Final_Timelapse.mp4");

        if (File.Exists(outputPath)) File.Delete(outputPath);

        UnityEngine.Debug.Log($"[FFmpeg] 변환 시작: {inputPattern} -> {outputPath}");

        // 30fps로 영상을 만듦 (15fps로 찍었으니 2배속 영상이 됨)
        string args = $"-framerate 30 -i \"{inputPattern}\" -c:v libx264 -pix_fmt yuv420p \"{outputPath}\"";

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process.Start(startInfo);
    }
}