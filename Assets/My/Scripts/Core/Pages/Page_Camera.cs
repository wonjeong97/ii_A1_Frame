using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using My.Scripts.Hardware;
using My.Scripts.Timelapse;
using My.Scripts.Utils; // 유틸리티 네임스페이스 추가
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.UI;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 웹캠 제어, UI 연출 및 촬영된 사진의 로컬 저장을 담당하는 페이지.
    /// UniTask와 UIFadeUtility를 사용하여 GC 할당 없이 연출 시퀀스를 제어함.
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

        public override void SetupData(object data) { }

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
            if (cameraDisplay) UIFadeUtility.SetAlpha(cameraDisplay, 0f);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            ResetCameraUI();
            UpdateLightingState();
            StartWebCam(); 
            
            if (TimeLapseRecorder.Instance)
            {
                TimeLapseRecorder.Instance.CurrentTint = _currentTintColor;
            }

            // 모든 연출 시퀀스를 UniTask로 통합 실행
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            
            SequenceAsync(_sequenceCts.Token).Forget();
        }

        private void ResetCameraUI()
        {
            SetAlpha(1f);
            if (cameraDisplay) UIFadeUtility.SetAlpha(cameraDisplay, 0f);

            if (countdownText)
            {
                countdownText.text = string.Empty;
                UIFadeUtility.SetAlpha(countdownText, 0f);
            }

            if (flashImage)
            {
                flashImage.gameObject.SetActive(false);
                UIFadeUtility.SetAlpha(flashImage, 0f);
            }

            if (contentCanvasGroup) contentCanvasGroup.alpha = 1f;
            CleanupPhotoUI();
        }

        private void UpdateLightingState()
        {
            if (!LevelManager.Instance || !HueManager.Instance) return;

            int qNum = LevelManager.Instance.CurrentQuestionNumber;
            RGBColor targetRgb = GetTargetRgbColor(qNum);
            
            _currentTintColor = new Color(targetRgb.r / 255f, targetRgb.g / 255f, targetRgb.b / 255f);

            HueManager.Instance.SetLightColorRGBAsync(1, targetRgb, -1, 4, this.GetCancellationTokenOnDestroy()).Forget();
            HueManager.Instance.SetLightColorRGBAsync(2, targetRgb, -1, 4, this.GetCancellationTokenOnDestroy()).Forget();
            
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
            return _isQ6To10 ? (HueManager.Instance.PopRandomColor() ?? HueManager.Instance.Config.whiteColor) : HueManager.Instance.Config.whiteColor;
        }

        private void TurnOffHueLights()
        {
            if (HueManager.Instance)
            {
                HueManager.Instance.SetLightStateAsync(1, false, this.GetCancellationTokenOnDestroy()).Forget();
                HueManager.Instance.SetLightStateAsync(2, false, this.GetCancellationTokenOnDestroy()).Forget();
            }
        }

        public override void OnExit()
        {
            _sequenceCts?.Cancel();
            base.OnExit();
            StopWebCam();
            CleanupPhotoUI();
            TurnOffHueLights();

            if (TimeLapseRecorder.Instance) TimeLapseRecorder.Instance.CurrentTint = Color.white;
        }

        private void OnDestroy()
        {
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            StopWebCam();
            TurnOffHueLights();
            if (_capturedPhoto) Destroy(_capturedPhoto);
        }

        /// <summary> 카메라 준비 -> 카운트다운 -> 촬영으로 이어지는 전체 비동기 시퀀스 </summary>
        private async UniTaskVoid SequenceAsync(CancellationToken token)
        {
            // 1. 카메라 피드 페이드인
            await UniTask.Delay(TimeSpan.FromSeconds(cameraFadeDelay), cancellationToken: token);
            if (_webCamTexture)
            {
                await UniTask.WaitUntil(
                    _webCamTexture, 
                    cam => cam.width > 16, 
                    PlayerLoopTiming.Update, 
                    token
                ).Timeout(TimeSpan.FromSeconds(2.0));
            }
            await UIFadeUtility.FadeGraphicAsync(cameraDisplay, 0f, 1f, cameraFadeDuration, token);

            // 2. 녹화 시작
            await UniTask.Delay(TimeSpan.FromSeconds(1.0), cancellationToken: token);
            if (_shouldSavePhoto && TimeLapseRecorder.Instance)
            {
                TimeLapseRecorder.Instance.SetCurrentLevel(_levelID);
                int qNum = LevelManager.Instance ? LevelManager.Instance.CurrentQuestionNumber : 0;
                TimeLapseRecorder.Instance.EnableTimelapseCapture = true;
                TimeLapseRecorder.Instance.EnableRealtimeCapture = (qNum >= 11 && qNum <= 15);
                TimeLapseRecorder.Instance.StartCapture(_webCamTexture);
            }

            // 3. 카운트다운
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_10_3초");
            await ShowAndFadeNumberAsync("3", token);
            await ShowAndFadeNumberAsync("2", token);
            await ShowAndFadeNumberAsync("1", token);

            // 4. 플래시 및 캡처
            if (_shouldSavePhoto && TimeLapseRecorder.Instance)
            {
                TimeLapseRecorder.Instance.EnableRealtimeCapture = false;
                TimeLapseRecorder.Instance.EnableTimelapseCapture = false;
                TimeLapseRecorder.Instance.StopCapture();
            }
            await FlashAndCaptureAsync(token);
            
            CompleteStep();
        }

        private async UniTask FlashAndCaptureAsync(CancellationToken token)
        {
            if (flashImage)
            {
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_11");
                flashImage.gameObject.SetActive(true);
                UIFadeUtility.SetAlpha(flashImage, 0.8f);
            }

            if (contentCanvasGroup) contentCanvasGroup.alpha = 0f;

            await UniTask.Delay(TimeSpan.FromSeconds(0.05), cancellationToken: token);

            CapturePhoto(); // 실제 텍스트 캡처 및 저장 명령
            TurnOffHueLights();

            if (flashImage)
            {
                await UIFadeUtility.FadeGraphicAsync(flashImage, 0.8f, 0f, 0.5f, token);
                flashImage.gameObject.SetActive(false);
            }

            await UniTask.Delay(TimeSpan.FromSeconds(2.0), cancellationToken: token);
        }

        private void CapturePhoto()
        {
            if (!_webCamTexture || !_webCamTexture.isPlaying) return;

            RenderTexture rt = RenderTexture.GetTemporary(PhotoWidth, PhotoHeight, 0, RenderTextureFormat.ARGB32);
            if (_currentMaskingMaterial) Graphics.Blit(_webCamTexture, rt, _currentMaskingMaterial);
            else Graphics.Blit(_webCamTexture, rt);

            if (!_capturedPhoto || _capturedPhoto.width != PhotoWidth || _capturedPhoto.height != PhotoHeight)
            {
                if (_capturedPhoto) Destroy(_capturedPhoto);
                _capturedPhoto = new Texture2D(PhotoWidth, PhotoHeight, TextureFormat.RGBA32, false);
            }

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            _capturedPhoto.ReadPixels(new Rect(0, 0, PhotoWidth, PhotoHeight), 0, 0);
            
            ApplyManualTintIfRequired();
            _capturedPhoto.Apply();
            
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

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

            Color32[] pixels = _capturedPhoto.GetPixels32();
            float tr = _currentTintColor.r, tg = _currentTintColor.g, tb = _currentTintColor.b;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                pixels[i] = new Color32((byte)(p.r * tr), (byte)(p.g * tg), (byte)(p.b * tb), p.a);
            }
            _capturedPhoto.SetPixels32(pixels);
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
                File.WriteAllBytes(Path.Combine(folder, $"{photoName}.png"), bytes);
            });
        }

        private async UniTask ShowAndFadeNumberAsync(string n, CancellationToken token)
        {
            if (!countdownText) return;
            countdownText.text = n;
            await UIFadeUtility.FadeGraphicAsync(countdownText, 1f, 0f, 1f, token);
        }

        private void StartWebCam()
        {
            if (_webCamTexture && _webCamTexture.isPlaying) return;
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0) return;

            _selectedDevice = GetPreferredDevice(devices);
            try
            {
                _webCamTexture = new WebCamTexture(_selectedDevice.name, PhotoWidth, PhotoHeight);
                if (cameraDisplay) cameraDisplay.texture = _webCamTexture;
                _webCamTexture.Play();
            }
            catch (Exception e) { Debug.LogError($"웹캠 예외: {e.Message}"); }
        }

        private WebCamDevice GetPreferredDevice(WebCamDevice[] devices)
        {
            string[] keywords = { "USB Video", "Webcam", "Camera" };
            foreach (var k in keywords)
                foreach (var d in devices)
                    if (d.name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return d;
            return devices[0];
        }

        private void StopWebCam()
        {
            if (_webCamTexture && _webCamTexture.isPlaying) _webCamTexture.Stop();
            _webCamTexture = null;
        }

        private void CleanupPhotoUI()
        {
            if (cameraDisplay && cameraDisplay.texture == _capturedPhoto) cameraDisplay.texture = null;
        }
    }
}