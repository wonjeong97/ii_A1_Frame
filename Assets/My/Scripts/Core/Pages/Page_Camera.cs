using System;
using System.Collections;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using My.Scripts.Hardware;
using My.Scripts.Timelapse;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
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
        private CancellationTokenSource _hueCts;

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

        public override void SetupData(object data)
        {
        }

        public void SetPhotoFilename(string fileName)
        {
            _photoFileName = fileName;
        }

        public void SetLevelID(string id)
        {
            _levelID = id;
        }

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
            SetRawImageAlpha(cameraDisplay, 0f);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            SetAlpha(1f);
            SetRawImageAlpha(cameraDisplay, 0f);

            if (countdownText)
            {
                countdownText.text = "";
                SetTextAlpha(0f);
            }

            if (flashImage)
            {
                flashImage.gameObject.SetActive(false);
                SetImageAlpha(flashImage, 0f);
            }

            if (contentCanvasGroup) contentCanvasGroup.alpha = 1f;

            CleanupPhotoUI();
            StartWebCam(); 

            _hueCts?.Cancel();
            _hueCts?.Dispose();
            _hueCts = new CancellationTokenSource();

            if (LevelManager.Instance && HueManager.Instance)
            {
                int qNum = LevelManager.Instance.CurrentQuestionNumber;
                RGBColor fallbackWhite =
                    (HueManager.Instance.Config != null && HueManager.Instance.Config.whiteColor != null)
                        ? HueManager.Instance.Config.whiteColor
                        : new RGBColor { r = 191, g = 239, b = 251 };

                if (qNum >= 6 && qNum <= 10)
                {
                    _isQ6To10 = true;
                    RGBColor randomColor = HueManager.Instance.PopRandomColor() ?? fallbackWhite;
                    
                    _currentTintColor = new Color(randomColor.r / 255f, randomColor.g / 255f, randomColor.b / 255f);

                    HueManager.Instance.SetLightColorRGBAsync(1, randomColor, -1, 4, _hueCts.Token).Forget();
                    HueManager.Instance.SetLightColorRGBAsync(2, randomColor, -1, 4, _hueCts.Token).Forget();
                }
                else
                {
                    _isQ6To10 = false;
                    _currentTintColor = Color.white;

                    HueManager.Instance.SetLightColorRGBAsync(1, fallbackWhite, -1, 4, _hueCts.Token).Forget();
                    HueManager.Instance.SetLightColorRGBAsync(2, fallbackWhite, -1, 4, _hueCts.Token).Forget();
                }
            }
            
            if (cameraDisplay)
            {
                Color initColor = _currentTintColor;
                initColor.a = 0f; 
                cameraDisplay.color = initColor;
            }

            if (TimeLapseRecorder.Instance)
            {
                TimeLapseRecorder.Instance.CurrentTint = _currentTintColor;
            }

            StartCoroutine(FadeInCameraRoutine());
            StartCoroutine(CountdownRoutine());
        }

        private void TurnOffHueLights()
        {
            _hueCts?.Cancel();
            _hueCts?.Dispose();
            _hueCts = new CancellationTokenSource();

            if (HueManager.Instance)
            {
                HueManager.Instance.SetLightStateAsync(1, false, _hueCts.Token).Forget();
                HueManager.Instance.SetLightStateAsync(2, false, _hueCts.Token).Forget();
            }
        }

        public override void OnExit()
        {
            StopAllCoroutines();
            base.OnExit();
            StopWebCam();
            CleanupPhotoUI();
            TurnOffHueLights();

            if (TimeLapseRecorder.Instance)
            {
                TimeLapseRecorder.Instance.CurrentTint = Color.white;
            }
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            StopWebCam();
            TurnOffHueLights();
            if (_capturedPhoto)
            {
                Destroy(_capturedPhoto);
                _capturedPhoto = null;
            }
        }

        private IEnumerator FadeInCameraRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(cameraFadeDelay);

            if (_webCamTexture)
            {
                float timeout = 2.0f;
                float waitTimer = 0f;
                while (_webCamTexture.width <= 16 && waitTimer < timeout)
                {
                    waitTimer += Time.deltaTime;
                    yield return null;
                }
            }

            float timer = 0f;
            while (timer < cameraFadeDuration)
            {
                timer += Time.deltaTime;
                SetRawImageAlpha(cameraDisplay, Mathf.Lerp(0f, 1f, timer / cameraFadeDuration));
                yield return null;
            }

            SetRawImageAlpha(cameraDisplay, 1f);
        }

        private IEnumerator CountdownRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(1.0f + cameraFadeDelay);

            if (_shouldSavePhoto && TimeLapseRecorder.Instance)
            {
                if (_webCamTexture && _webCamTexture.isPlaying)
                {
                    TimeLapseRecorder.Instance.SetCurrentLevel(_levelID);
                    
                    // 현재 문항 번호를 가져옴
                    int qNum = LevelManager.Instance ? LevelManager.Instance.CurrentQuestionNumber : 0;
                    
                    // 타임랩스는 사진 저장이 켜진 모든 문항(Q1~Q15)에서 활성화
                    TimeLapseRecorder.Instance.EnableTimelapseCapture = true;
                    
                    // 리얼타임은 11번 문항부터 15번 문항까지만 활성화
                    TimeLapseRecorder.Instance.EnableRealtimeCapture = (qNum >= 11 && qNum <= 15);
                    TimeLapseRecorder.Instance.StartCapture(_webCamTexture);
                }
                else
                {
                    Debug.LogError("[Page_Camera] 웹캠 오류로 타임랩스를 녹화할 수 없습니다.");
                }
            }

            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_10_3초");

            yield return StartCoroutine(ShowAndFadeNumber("3"));
            yield return StartCoroutine(ShowAndFadeNumber("2"));
            yield return StartCoroutine(ShowAndFadeNumber("1"));

            if (_shouldSavePhoto && TimeLapseRecorder.Instance)
            {
                // 종료 시점에는 둘 다 비활성화 후 캡처 완전 종료
                TimeLapseRecorder.Instance.EnableRealtimeCapture = false;
                TimeLapseRecorder.Instance.EnableTimelapseCapture = false;
                TimeLapseRecorder.Instance.StopCapture();
            }

            yield return StartCoroutine(FlashAndCaptureRoutine());
        }

        private IEnumerator FlashAndCaptureRoutine()
        {
            float maxAlpha = 0.8f;
            if (flashImage)
            {
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_11");
                flashImage.gameObject.SetActive(true);
                SetImageAlpha(flashImage, maxAlpha);
            }

            if (contentCanvasGroup) contentCanvasGroup.alpha = 0f;

            yield return CoroutineData.GetWaitForSeconds(0.05f);

            CapturePhoto();
            TurnOffHueLights();

            if (flashImage)
            {
                float t = 0f;
                while (t < 0.5f)
                {
                    t += Time.deltaTime;
                    SetImageAlpha(flashImage, Mathf.Lerp(maxAlpha, 0f, t / 0.5f));
                    yield return null;
                }

                flashImage.gameObject.SetActive(false);
            }

            yield return CoroutineData.GetWaitForSeconds(2.0f);

            CompleteStep();
        }

        private void CapturePhoto()
        {
            if (!_webCamTexture || !_webCamTexture.isPlaying)
            {
                Debug.LogError("[Page_Camera] 웹캠이 중지되어 캡처할 수 없습니다.");
                return;
            }

            RenderTexture rt = RenderTexture.GetTemporary(PhotoWidth, PhotoHeight, 0, RenderTextureFormat.ARGB32);
            Material maskToUse = _currentMaskingMaterial;
            if (maskToUse) Graphics.Blit(_webCamTexture, rt, maskToUse);
            else Graphics.Blit(_webCamTexture, rt);

            if (!_capturedPhoto || _capturedPhoto.width != PhotoWidth || _capturedPhoto.height != PhotoHeight)
            {
                if (_capturedPhoto) Destroy(_capturedPhoto);
                _capturedPhoto = new Texture2D(PhotoWidth, PhotoHeight, TextureFormat.RGBA32, false);
            }

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            _capturedPhoto.ReadPixels(new Rect(0, 0, PhotoWidth, PhotoHeight), 0, 0);
            
            if (_isQ6To10 && _currentTintColor != Color.white)
            {
                Color32[] pixels = _capturedPhoto.GetPixels32();
                
                float tr = _currentTintColor.r;
                float tg = _currentTintColor.g;
                float tb = _currentTintColor.b;

                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 p = pixels[i];
                    pixels[i] = new Color32(
                        (byte)(p.r * tr),
                        (byte)(p.g * tg),
                        (byte)(p.b * tb),
                        p.a
                    );
                }
                _capturedPhoto.SetPixels32(pixels);
            }
            
            _capturedPhoto.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            if (cameraDisplay) 
            {
                cameraDisplay.texture = _capturedPhoto;
                cameraDisplay.color = Color.white; 
            }

            if (_shouldSavePhoto)
            {
                SavePhotoToCustomFolderAsync(_capturedPhoto).Forget();
            }

            StopWebCam();
        }

        private async UniTaskVoid SavePhotoToCustomFolderAsync(Texture2D photo)
        {
            if (!photo) return;

            byte[] rawData = photo.GetRawTextureData();
            int width = photo.width;
            int height = photo.height;
            UnityEngine.Experimental.Rendering.GraphicsFormat format = photo.graphicsFormat;

            string dataPath = Application.dataPath;
            string photoName = _photoFileName;

            try
            {
                await UniTask.RunOnThreadPool(() =>
                {
                    byte[] bytes =
                        UnityEngine.ImageConversion.EncodeArrayToPNG(rawData, format, (uint)width, (uint)height);

                    DirectoryInfo parentDir = Directory.GetParent(dataPath);
                    string rootPath = parentDir != null ? parentDir.FullName : dataPath;

                    string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
                    string folder = Path.Combine(rootPath, "Pictures", dateFolder);

                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    string path = Path.Combine(folder, $"{photoName}.png");
                    File.WriteAllBytes(path, bytes);

                    Debug.Log($"[Page_Camera] 원본 사진 저장 완료: {path}");
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[Page_Camera] 사진 저장 실패: {e.Message}");
            }
        }

        private void CleanupPhotoUI()
        {
            if (cameraDisplay && cameraDisplay.texture == _capturedPhoto)
                cameraDisplay.texture = null;
        }

        private IEnumerator ShowAndFadeNumber(string n)
        {
            if (countdownText)
            {
                countdownText.text = n;
                SetTextAlpha(1f);
                float t = 0;
                while (t < 1)
                {
                    t += Time.deltaTime;
                    SetTextAlpha(Mathf.Lerp(1f, 0f, t));
                    yield return null;
                }
            }
        }

        private void SetTextAlpha(float a)
        {
            if (countdownText)
            {
                Color c = countdownText.color;
                c.a = a;
                countdownText.color = c;
            }
        }

        private void SetImageAlpha(Image i, float a)
        {
            if (i)
            {
                Color c = i.color;
                c.a = a;
                i.color = c;
            }
        }

        private void SetRawImageAlpha(RawImage ri, float a)
        {
            if (ri)
            {
                Color c = ri.color;
                c.a = a;
                ri.color = c;
            }
        }

        private void StartWebCam()
        {
            if (_webCamTexture && _webCamTexture.isPlaying) return;

            _selectedDevice = default;

            if (cameraDisplay)
            {
                WebCamDevice[] devices = WebCamTexture.devices;
                if (devices.Length == 0)
                {
                    Debug.LogError("[Page_Camera] 웹캠 장치가 없습니다.");
                    return;
                }

                string selectedDeviceName = "";
                for (int i = 0; i < devices.Length; i++)
                {
                    if (devices[i].name.IndexOf("USB Video", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        devices[i].name.IndexOf("Webcam", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        devices[i].name.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        selectedDeviceName = devices[i].name;
                        _selectedDevice = devices[i];
                        break;
                    }
                }

                if (string.IsNullOrEmpty(selectedDeviceName) && devices.Length > 0)
                {
                    selectedDeviceName = devices[0].name;
                    _selectedDevice = devices[0];
                }

                try
                {
                    _webCamTexture = new WebCamTexture(selectedDeviceName, PhotoWidth, PhotoHeight);
                    cameraDisplay.texture = _webCamTexture;
                    _webCamTexture.Play();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Page_Camera] 웹캠 예외 발생: {e.Message}");
                }
            }
        }

        private void StopWebCam()
        {
            if (_webCamTexture && _webCamTexture.isPlaying) _webCamTexture.Stop();
            _webCamTexture = null;
        }
    }
}