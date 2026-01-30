using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    public class Page_Camera : GamePage
    {
        [Header("UI References")] [SerializeField]
        private RawImage cameraDisplay;

        [SerializeField] private Text countdownText;

        [Header("Effects")] [SerializeField] private Image flashImage;
        [SerializeField] private CanvasGroup contentCanvasGroup;

        [Header("Default Settings")] [SerializeField]
        private Material defaultMaskingMaterial;

        [SerializeField] private bool defaultSavePhoto = true;

        [Header("Transition")] [SerializeField]
        private float cameraFadeDelay = 0.5f;

        [SerializeField] private float cameraFadeDuration = 0.5f;

        private Material _currentMaskingMaterial;
        private bool _shouldSavePhoto;
        private bool _triggerEncodingOnCapture;
        private bool _isConfigured;

        private WebCamTexture _webCamTexture;
        private Texture2D _capturedPhoto;
        private string _photoFileName = "Default_Photo";

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

        public override void SetupData(object data)
        {
        }

        public void SetPhotoFilename(string fileName)
        {
            _photoFileName = fileName;
        }

        public void Configure(bool shouldSave, Material maskMat = null, bool triggerEncoding = false)
        {
            _shouldSavePhoto = shouldSave;
            _currentMaskingMaterial = maskMat;
            _triggerEncodingOnCapture = triggerEncoding;
            _isConfigured = true;
        }

        // [추가] 외부에서 미리 카메라를 켜는 함수
        public void PreloadCamera()
        {
            Debug.Log("[Page_Camera] 카메라 미리 켜기 (Preload)...");
            StartWebCam();
            // 미리 켜더라도 화면에는 안 보이게 투명 처리 (OnEnter에서 페이드 인)
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

            CleanupPhoto();
            StartWebCam(); // 이미 Preload 되었다면 내부에서 무시됨

            StartCoroutine(FadeInCameraRoutine());
            StartCoroutine(CountdownRoutine());
        }

        public override void OnExit()
        {
            StopAllCoroutines();
            base.OnExit();
            StopWebCam();
            CleanupPhoto();
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            StopWebCam();
            CleanupPhoto();
        }

        private IEnumerator FadeInCameraRoutine()
        {
            // 미리 켜뒀으므로 대기 시간을 줄여도 되지만, 안정성을 위해 유지
            yield return CoroutineData.GetWaitForSeconds(cameraFadeDelay);

            if (_webCamTexture != null)
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

            if (TimeLapseRecorder.Instance != null && _webCamTexture != null)
            {
                TimeLapseRecorder.Instance.StartCapture(_webCamTexture);
            }

            yield return StartCoroutine(ShowAndFadeNumber("3"));
            yield return StartCoroutine(ShowAndFadeNumber("2"));
            yield return StartCoroutine(ShowAndFadeNumber("1"));

            if (TimeLapseRecorder.Instance != null)
            {
                TimeLapseRecorder.Instance.StopCapture();
            }

            yield return StartCoroutine(FlashAndCaptureRoutine());
        }

        private IEnumerator FlashAndCaptureRoutine()
        {
            float maxAlpha = 0.8f;

            if (flashImage)
            {
                flashImage.gameObject.SetActive(true);
                SetImageAlpha(flashImage, maxAlpha);
            }

            if (contentCanvasGroup) contentCanvasGroup.alpha = 0f;

            yield return CoroutineData.GetWaitForSeconds(0.05f);

            CapturePhoto();
            
            if (_triggerEncodingOnCapture && TimeLapseRecorder.Instance != null)
            {
                Debug.Log("[Page_Camera] Q15 감지: 촬영 즉시 인코딩 요청");
                TimeLapseRecorder.Instance.ConvertToVideo();
            }

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
            if (_webCamTexture != null && _webCamTexture.isPlaying)
            {
                RenderTexture rt = RenderTexture.GetTemporary(PhotoWidth, PhotoHeight, 0, RenderTextureFormat.ARGB32);

                Material maskToUse = _currentMaskingMaterial;
                if (maskToUse != null) Graphics.Blit(_webCamTexture, rt, maskToUse);
                else Graphics.Blit(_webCamTexture, rt);

                _capturedPhoto = new Texture2D(PhotoWidth, PhotoHeight, TextureFormat.RGBA32, false);

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                _capturedPhoto.ReadPixels(new Rect(0, 0, PhotoWidth, PhotoHeight), 0, 0);
                _capturedPhoto.Apply();

                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                if (cameraDisplay) cameraDisplay.texture = _capturedPhoto;

                if (_shouldSavePhoto)
                {
                    SavePhotoToCustomFolder(_capturedPhoto);
                }
                else
                {
                    Debug.Log($"[Page_Camera] 저장 건너뜀 (shouldSavePhoto: false)");
                }

                StopWebCam();
            }
        }
    
        /// <summary> 사진을 커스텀 폴더(Pictures/yyyy-MM-dd)에 저장 </summary>
        private void SavePhotoToCustomFolder(Texture2D photo)
        {
            if (!photo) return;
            try
            {
                byte[] bytes = photo.EncodeToPNG();
                
                // 루트 경로 계산
                string dataPath = Application.dataPath;
                DirectoryInfo parentDir = Directory.GetParent(dataPath);
                string rootPath = (parentDir != null) ? parentDir.FullName : dataPath;

                // 날짜별 폴더 분리 (Pictures/yyyy-MM-dd/)
                string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
                string folder = Path.Combine(rootPath, "Pictures", dateFolder);
                
                // 폴더가 없으면 생성
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string path = Path.Combine(folder, $"{_photoFileName}.png");
                File.WriteAllBytes(path, bytes);
                Debug.Log($"[Page_Camera] 저장됨: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Page_Camera] 저장 실패: {e.Message}");
            }
        }

        private void CleanupPhoto()
        {
            if (_capturedPhoto)
            {
                Destroy(_capturedPhoto);
                _capturedPhoto = null;
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
            // [수정] 이미 켜져 있으면 중복 실행 방지
            if (_webCamTexture != null && _webCamTexture.isPlaying)
            {
                Debug.Log("[Page_Camera] 카메라가 이미 실행 중입니다.");
                return;
            }

            if (cameraDisplay)
            {
                WebCamDevice[] devices = WebCamTexture.devices;
                string selectedDeviceName = "";

                for (int i = 0; i < devices.Length; i++)
                {
                    if (devices[i].name == "USB Video")
                    {
                        selectedDeviceName = devices[i].name;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(selectedDeviceName) && devices.Length > 0)
                {
                    selectedDeviceName = devices[0].name;
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