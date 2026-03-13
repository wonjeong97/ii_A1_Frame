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

        // 비동기 통신 취소용 토큰 제어기
        private CancellationTokenSource _hueCts;

        private const int PhotoWidth = 1920;
        private const int PhotoHeight = 1080;

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
                // 이전 DeviceName 데이터가 없는 경우를 대비한 가드 추가
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

            // 진입 시 이전 진행 토큰 해제 및 신규 생성
            _hueCts?.Cancel();
            _hueCts?.Dispose();
            _hueCts = new CancellationTokenSource();

            if (LevelManager.Instance && HueManager.Instance)
            {
                int qNum = LevelManager.Instance.CurrentQuestionNumber;

                // Q6 ~ Q10 구간: 섞어둔 5가지 색상 중 랜덤으로 뽑아 점등
                if (qNum >= 6 && qNum <= 10)
                {
                    RGBColor randomColor = HueManager.Instance.PopRandomColor();
                    if (randomColor == null)
                    {
                        randomColor = new RGBColor { r = 255, g = 255, b = 255 };
                    }
                    HueManager.Instance.SetLightColorRGBAsync(1, randomColor, -1, 4, _hueCts.Token).Forget();
                    HueManager.Instance.SetLightColorRGBAsync(2, randomColor, -1, 4, _hueCts.Token).Forget();
                }
                else
                {
                    // 그 외의 모든 촬영 구간: 백색등(White) 점등
                    RGBColor whiteColor = new RGBColor { r = 255, g = 255, b = 255 };
                    HueManager.Instance.SetLightColorRGBAsync(1, whiteColor, -1, 4, _hueCts.Token).Forget();
                    HueManager.Instance.SetLightColorRGBAsync(2, whiteColor, -1, 4, _hueCts.Token).Forget();
                }
            }

            StartCoroutine(FadeInCameraRoutine());
            StartCoroutine(CountdownRoutine());
        }

        /// <summary> 공통 휴 조명 소등 헬퍼 메서드 </summary>
        private void TurnOffHueLights()
        {
            // 진행 중이던 통신을 강제 취소하고 신규 토큰을 생성하여 끄기 명령 전송 보장
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

            // 카메라 페이지를 벗어날 때는 문항 번호와 무관하게 무조건 소등
            TurnOffHueLights();
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            StopWebCam();

            // 안전 가드: 오브젝트가 파괴될 때도 무조건 소등
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
                while (_webCamTexture.width <= 16) yield return null;
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

            if (_shouldSavePhoto && TimeLapseRecorder.Instance && _webCamTexture)
            {
                TimeLapseRecorder.Instance.SetCurrentLevel(_levelID);
                TimeLapseRecorder.Instance.EnableTimelapseCapture = true;
                TimeLapseRecorder.Instance.EnableRealtimeCapture = true;
                TimeLapseRecorder.Instance.StartCapture(_webCamTexture);
            }

            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_10_5초");

            yield return StartCoroutine(ShowAndFadeNumber("5"));
            yield return StartCoroutine(ShowAndFadeNumber("4"));
            yield return StartCoroutine(ShowAndFadeNumber("3"));

            if (_shouldSavePhoto && TimeLapseRecorder.Instance)
            {
                TimeLapseRecorder.Instance.EnableRealtimeCapture = false;
            }

            yield return StartCoroutine(ShowAndFadeNumber("2"));
            yield return StartCoroutine(ShowAndFadeNumber("1"));

            if (_shouldSavePhoto && TimeLapseRecorder.Instance)
            {
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

            // 사진 촬영 직후 문항 번호와 무관하게 무조건 소등
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
            if (_webCamTexture && _webCamTexture.isPlaying)
            {
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
                _capturedPhoto.Apply();

                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                if (cameraDisplay) cameraDisplay.texture = _capturedPhoto;

                if (_shouldSavePhoto)
                {
                    SavePhotoToCustomFolderAsync(_capturedPhoto).Forget();
                }

                StopWebCam();
            }
        }

        private async UniTaskVoid SavePhotoToCustomFolderAsync(Texture2D photo)
        {
            if (!photo)
            {
                Debug.LogError("[Page_Camera] 캡처된 텍스처가 존재하지 않아 저장을 취소합니다.");
                return;
            }

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

                    Debug.Log($"[Page_Camera] 비동기 사진 저장 완료: {path}");
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[Page_Camera] 비동기 사진 저장 실패: {e.Message}");
            }
        }

        private void CleanupPhotoUI()
        {
            if (cameraDisplay && cameraDisplay.texture == _capturedPhoto)
            {
                cameraDisplay.texture = null;
            }
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
            if (_webCamTexture && _webCamTexture.isPlaying)
            {
                return;
            }

            _selectedDevice = default; // 이전 프론트카메라 설정값 잔존을 방지하기 위한 리셋

            if (cameraDisplay)
            {
                WebCamDevice[] devices = WebCamTexture.devices;
                string selectedDeviceName = "";

                for (int i = 0; i < devices.Length; i++)
                {
                    if (devices[i].name == "USB Video")
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

                if (!string.IsNullOrEmpty(selectedDeviceName))
                {
                    _webCamTexture = new WebCamTexture(selectedDeviceName, PhotoWidth, PhotoHeight);
                }
                else
                {
                    _webCamTexture = new WebCamTexture(PhotoWidth, PhotoHeight);
                }

                cameraDisplay.texture = _webCamTexture;
                _webCamTexture.Play();
            }
        }

        private void StopWebCam()
        {
            if (_webCamTexture && _webCamTexture.isPlaying) _webCamTexture.Stop();
            _webCamTexture = null;
        }
    }
}