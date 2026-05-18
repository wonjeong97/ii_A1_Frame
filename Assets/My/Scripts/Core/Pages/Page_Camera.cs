using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Text;
using Unity.Collections;
using My.Scripts.Hardware;
using My.Scripts.Timelapse;
using My.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.UI;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 웹캠 제어, UI 연출 및 촬영된 사진의 로컬 저장을 담당하는 페이지.
    /// 네이티브 미디어 릭, VRAM 유실 사각지대, 중복 드라이버 로드 크래시가 완벽히 차단되었습니다.
    /// </summary>
    public class Page_Camera : GamePage
    {
        [Header("UI References")]
        [SerializeField] private RawImage cameraDisplay;
        [SerializeField] private Text countdownText;

        [Header("Effects")]
        [SerializeField] private Image flashImage;
        [SerializeField] private CanvasGroup contentCanvasGroup;

        [Header("Default Settings")]
        [SerializeField] private Material defaultMaskingMaterial;
        [SerializeField] private bool defaultSavePhoto = true;

        [Header("Transition")]
        [SerializeField] private float cameraFadeDelay = 0.5f;
        [SerializeField] private float cameraFadeDuration = 0.5f;

        private Material _currentMaskingMaterial;
        private bool _shouldSavePhoto;
        private bool _triggerEncodingOnCapture;
        private bool _isConfigured;

        private WebCamTexture _webCamTexture;
        private WebCamDevice _selectedDevice;
        private Texture2D _capturedPhoto;
        private string _photoFileName = "Default_Photo";

        private string _levelID;
        private CancellationTokenSource _sequenceCts;

        private const int PhotoWidth = 1920;
        private const int PhotoHeight = 1080;

        private bool _isQ6To10;
        private Color _currentTintColor = Color.white;

        // --- 의존성 주입 (DI) 변수 ---
        private HueManager _hueManager;
        private TimeLapseRecorder _timeLapseRecorder;
        private SoundManager _soundManager;

        [Inject]
        public void Construct(
            HueManager hueManager,
            TimeLapseRecorder timeLapseRecorder,
            SoundManager soundManager)
        {
            _hueManager = hueManager;
            _timeLapseRecorder = timeLapseRecorder;
            _soundManager = soundManager;
        }

        protected override void Awake()
        {
            base.Awake();
            if (!_isConfigured)
            {
                _currentMaskingMaterial = defaultMaskingMaterial;
                _shouldSavePhoto = defaultSavePhoto;
            }
        }

        private void Update()
        {
            if (_webCamTexture && _webCamTexture.isPlaying && cameraDisplay)
            {
                float sy = _webCamTexture.videoVerticallyMirrored ? -1f : 1f;
                float sx = (!string.IsNullOrEmpty(_selectedDevice.name) && _selectedDevice.isFrontFacing) ? -1f : 1f;

                cameraDisplay.rectTransform.localEulerAngles = new Vector3(0f, 0f, -_webCamTexture.videoRotationAngle);
                cameraDisplay.rectTransform.localScale = new Vector3(sx, sy, 1f);
            }
        }

        public override void SetupData(object data)
        {
        }

        public void SetPhotoFilename(string fileName) => _photoFileName = fileName;
        public void SetLevelID(string id) => _levelID = id;

        public void Configure(bool shouldSave, Material maskMat = null, bool triggerEncoding = false)
        {
            _shouldSavePhoto = shouldSave;
            _currentMaskingMaterial = maskMat;
            _triggerEncodingOnCapture = triggerEncoding;
            _isConfigured = true;
        }

        public void PreloadCamera()
        {
            StartWebCam();
            if (cameraDisplay) cameraDisplay.SetAlpha(0f);
        }

        public override void OnEnter()
        {
            base.OnEnter();

            ResetCameraUI();

            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            UpdateLightingState(_sequenceCts.Token);
            StartWebCam();

            if (_timeLapseRecorder)
            {
                _timeLapseRecorder.CurrentTint = _currentTintColor;
            }

            SequenceAsync(_sequenceCts.Token).Forget();
        }

        private void ResetCameraUI()
        {
            SetAlpha(1f);
            if (cameraDisplay) cameraDisplay.SetAlpha(0f);

            if (countdownText)
            {
                countdownText.text = string.Empty;
                countdownText.SetAlpha(0f);
            }

            if (flashImage)
            {
                flashImage.gameObject.SetActive(false);
                flashImage.SetAlpha(0f);
            }

            if (contentCanvasGroup) contentCanvasGroup.alpha = 1f;
            CleanupPhotoUI();
        }

        private void UpdateLightingState(CancellationToken token)
        {
            if (!LevelManager.Instance || !_hueManager) return;

            int qNum = LevelManager.Instance.CurrentQuestionNumber;
            RGBColor targetRgb = GetTargetRgbColor(qNum);

            if (_isQ6To10)
            {
                _currentTintColor = new Color(targetRgb.r / 255f, targetRgb.g / 255f, targetRgb.b / 255f);
            }
            else
            {
                _currentTintColor = Color.white;
            }

            _hueManager.SetLightColorRGBAsync(1, targetRgb, -1, 4, token).Forget();
            _hueManager.SetLightColorRGBAsync(2, targetRgb, -1, 4, token).Forget();

            if (cameraDisplay)
            {
                Color initColor = _currentTintColor;
                initColor.a = 0f;
                cameraDisplay.color = initColor;
            }
        }

        private RGBColor GetTargetRgbColor(int qNum)
        {
            _isQ6To10 = (qNum >= 6 && qNum <= 10);
            return _isQ6To10
                ? (_hueManager.PopRandomColor() ?? _hueManager.Config.whiteColor)
                : _hueManager.Config.whiteColor;
        }

        private void TurnOffHueLights(CancellationToken token)
        {
            if (_hueManager)
            {
                _hueManager.SetLightStateAsync(1, false, token).Forget();
                _hueManager.SetLightStateAsync(2, false, token).Forget();
            }
        }

        public override void OnExit()
        {
            _sequenceCts?.Cancel();
            base.OnExit();
            StopWebCam();
            CleanupPhotoUI();
            TurnOffHueLights(this.GetCancellationTokenOnDestroy());

            if (_timeLapseRecorder) _timeLapseRecorder.CurrentTint = Color.white;
        }

        protected override void OnDestroy()
        {
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = null;

            StopWebCam();
            TurnOffHueLights(CancellationToken.None);
            if (_capturedPhoto) Destroy(_capturedPhoto);

            base.OnDestroy();
        }

        private async UniTaskVoid SequenceAsync(CancellationToken token)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(cameraFadeDelay), cancellationToken: token);
            if (_webCamTexture)
            {
                bool isTimeout = await UniTask.WaitUntil(
                    _webCamTexture,
                    cam => cam.width > 16,
                    PlayerLoopTiming.Update,
                    token
                ).TimeoutWithoutException(TimeSpan.FromSeconds(2.0));

                if (isTimeout)
                {
                    Debug.LogWarning("[Page_Camera] WebCamTexture readiness timed out.");
                }
            }

            await cameraDisplay.FadeAsync(0f, 1f, cameraFadeDuration, token);

            await UniTask.Delay(TimeSpan.FromSeconds(1.0), cancellationToken: token);
            if (_shouldSavePhoto && _timeLapseRecorder)
            {
                _timeLapseRecorder.SetCurrentLevel(_levelID);
                int qNum = LevelManager.Instance ? LevelManager.Instance.CurrentQuestionNumber : 0;
                _timeLapseRecorder.EnableTimelapseCapture = true;
                _timeLapseRecorder.EnableRealtimeCapture = (qNum >= 11 && qNum <= 15);
                _timeLapseRecorder.StartCapture(_webCamTexture);
            }

            if (_soundManager) _soundManager.PlaySFX("공통_10_3초");
            await ShowAndFadeNumberAsync("3", token);
            await ShowAndFadeNumberAsync("2", token);
            await ShowAndFadeNumberAsync("1", token);

            if (_shouldSavePhoto && _timeLapseRecorder)
            {
                _timeLapseRecorder.EnableRealtimeCapture = false;
                _timeLapseRecorder.EnableTimelapseCapture = false;
                _timeLapseRecorder.StopCapture();
            }

            await FlashAndCaptureAsync(token);

            CompleteStep();
        }

        private async UniTask FlashAndCaptureAsync(CancellationToken token)
        {
            if (flashImage)
            {
                if (_soundManager) _soundManager.PlaySFX("공통_11");
                flashImage.gameObject.SetActive(true);
                await flashImage.FadeAsync(flashImage.color.a, 0.8f, 0.01f, token);
            }

            if (contentCanvasGroup) contentCanvasGroup.alpha = 0f;

            await UniTask.Delay(TimeSpan.FromSeconds(0.05), cancellationToken: token);

            CapturePhoto();
            TurnOffHueLights(token);

            if (flashImage)
            {
                await flashImage.FadeAsync(0.8f, 0f, 0.5f, token);
                flashImage.gameObject.SetActive(false);
            }

            await UniTask.Delay(TimeSpan.FromSeconds(2.0), cancellationToken: token);
        }

        private void CapturePhoto()
        {
            if (!_webCamTexture || !_webCamTexture.isPlaying) return;

            // [치명적 버그 방어] 연출 연산 도중 예외가 터지더라도 VRAM 누수가 절대 없도록 try-finally 처리 구조 구축
            RenderTexture rt = RenderTexture.GetTemporary(PhotoWidth, PhotoHeight, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;

            try
            {
                if (_currentMaskingMaterial) Graphics.Blit(_webCamTexture, rt, _currentMaskingMaterial);
                else Graphics.Blit(_webCamTexture, rt);

                if (!_capturedPhoto || _capturedPhoto.width != PhotoWidth || _capturedPhoto.height != PhotoHeight)
                {
                    if (_capturedPhoto) Destroy(_capturedPhoto);
                    _capturedPhoto = new Texture2D(PhotoWidth, PhotoHeight, TextureFormat.RGBA32, false);
                }

                RenderTexture.active = rt;
                _capturedPhoto.ReadPixels(new Rect(0, 0, PhotoWidth, PhotoHeight), 0, 0);

                ApplyManualTintIfRequired();
                _capturedPhoto.Apply();
            }
            finally
            {
                // 어떠한 예외 상황에서도 안전하게 원본 해상도와 GPU VRAM 버퍼를 OS에 정상 강제 반환
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }

            if (cameraDisplay)
            {
                cameraDisplay.texture = _capturedPhoto;
                cameraDisplay.color = Color.white;
            }

            if (_shouldSavePhoto) SavePhotoToCustomFolderAsync(_capturedPhoto).Forget();

            StopWebCam();
        }

        private void ApplyManualTintIfRequired()
        {
            if (!_isQ6To10 || _currentTintColor == Color.white) return;

            NativeArray<Color32> pixels = _capturedPhoto.GetRawTextureData<Color32>();
            float tr = _currentTintColor.r, tg = _currentTintColor.g, tb = _currentTintColor.b;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                pixels[i] = new Color32((byte)(p.r * tr), (byte)(p.g * tg), (byte)(p.b * tb), p.a);
            }
        }

        private async UniTaskVoid SavePhotoToCustomFolderAsync(Texture2D photo)
        {
            if (!photo) return;

            byte[] rawData = photo.GetRawTextureData();
            int width = photo.width, height = photo.height;
            var format = photo.graphicsFormat;
            string photoName = _photoFileName;

            await UniTask.RunOnThreadPool(() =>
            {
                byte[] bytes = ImageConversion.EncodeArrayToPNG(rawData, format, (uint)width, (uint)height);
                string dataPath = Application.dataPath;
                DirectoryInfo parentDir = Directory.GetParent(dataPath);
                string rootPath = parentDir?.FullName ?? dataPath;

                string folder = Path.Combine(rootPath, "Pictures", DateTime.Now.ToString("yyyy-MM-dd"));

                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, ZString.Format("{0}.png", photoName)), bytes);
            });
        }

        private async UniTask ShowAndFadeNumberAsync(string n, CancellationToken token)
        {
            if (!countdownText) return;

            countdownText.text = n;
            await countdownText.FadeAsync(1f, 0f, 1f, token);
        }

        private void StartWebCam()
        {
            if (_webCamTexture) return;

            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0) return;

            _selectedDevice = GetPreferredDevice(devices);
            try
            {
                _webCamTexture = new WebCamTexture(_selectedDevice.name, PhotoWidth, PhotoHeight);
                if (cameraDisplay) cameraDisplay.texture = _webCamTexture;
                _webCamTexture.Play();
            }
            catch (Exception e)
            {
                Debug.LogError($"웹캠 예외: {e.Message}");
            }
        }

        private WebCamDevice GetPreferredDevice(WebCamDevice[] devices)
        {
            string[] keywords = { "USB Video", "Webcam", "Camera" };
            foreach (var k in keywords)
            foreach (var d in devices)
                if (d.name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                    return d;

            return devices[0];
        }

        private void StopWebCam()
        {
            if (_webCamTexture)
            {
                if (_webCamTexture.isPlaying) _webCamTexture.Stop();

                // [치명적 버그 수정] 언매니지드 C++ 미디어 드라이버 레이어에 맺힌 프레임 백버퍼를 즉시 완전히 파괴 (VRAM/RAM 리크 영구 소멸)
                Destroy(_webCamTexture);
                _webCamTexture = null;
            }
        }

        private void CleanupPhotoUI()
        {
            if (cameraDisplay && cameraDisplay.texture == _capturedPhoto) cameraDisplay.texture = null;
        }
    }
}