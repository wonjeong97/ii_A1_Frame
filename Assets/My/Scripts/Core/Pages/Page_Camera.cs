using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("Default Settings (Inspector)")] 
        [SerializeField] private Material defaultMaskingMaterial;
        [SerializeField] private bool defaultSavePhoto = true;

        // 내부 변수
        private Material _currentMaskingMaterial;
        private bool _shouldSavePhoto;
        
        // [핵심 수정 1] 설정 여부를 확인하는 변수 추가
        private bool _isConfigured = false; 

        private WebCamTexture _webCamTexture;
        private Texture2D _capturedPhoto;
        private string _photoFileName = "Default_Photo";
        
        private const int PhotoWidth = 1920;
        private const int PhotoHeight = 1080;

        protected override void Awake()
        {
            base.Awake();
            
            // [핵심 수정 2] 외부(LevelManager)에서 Configure가 먼저 호출되었다면 기본값으로 덮어쓰지 않음
            if (!_isConfigured)
            {
                _currentMaskingMaterial = defaultMaskingMaterial;
                _shouldSavePhoto = defaultSavePhoto;
            }
        }

        public override void SetupData(object data) { }

        public void SetPhotoFilename(string fileName)
        {
            _photoFileName = fileName;
        }

        public void Configure(bool shouldSave, Material maskMat = null)
        {
            _shouldSavePhoto = shouldSave;
            _currentMaskingMaterial = maskMat;
            
            // [핵심 수정 3] 설정이 완료되었음을 표시
            _isConfigured = true; 
            
            Debug.Log($"[Page_Camera] Configured: Save={_shouldSavePhoto}, Mask={(maskMat != null ? maskMat.name : "None")}");
        }

        public override void OnEnter()
        {
            base.OnEnter();
            SetAlpha(1f);

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
            StartWebCam();
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

        private IEnumerator CountdownRoutine()
        {   
            yield return new WaitForSeconds(1.0f);
            
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

            yield return new WaitForSeconds(0.05f);

            CapturePhoto();

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

            yield return new WaitForSeconds(2.0f);
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

                // [핵심 수정 5] 저장 여부(_shouldSavePhoto) 확실히 체크
                if (_shouldSavePhoto)
                {
                    SavePhotoToCustomFolder(_capturedPhoto);
                }
                else
                {
                    Debug.Log($"[Page_Camera] Photo save skipped. (shouldSavePhoto is false)");
                }

                StopWebCam();
            }
        }

        private void SavePhotoToCustomFolder(Texture2D photo)
        {
            if (!photo) return;
            try
            {
                byte[] bytes = photo.EncodeToPNG();
                string dataPath = Application.dataPath;
                DirectoryInfo parentDir = Directory.GetParent(dataPath);
                string rootPath = (parentDir != null) ? parentDir.FullName : dataPath;
                
                string folder = Path.Combine(rootPath, "Pictures");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string path = Path.Combine(folder, $"{_photoFileName}.png");
                File.WriteAllBytes(path, bytes);
                Debug.Log($"[Page_Camera] Saved: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Page_Camera] Save Failed: {e.Message}");
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

        private void StartWebCam()
        {
            if (cameraDisplay && _webCamTexture == null)
            {
                _webCamTexture = new WebCamTexture(PhotoWidth, PhotoHeight);
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