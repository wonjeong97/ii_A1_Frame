using System;
using System.Collections;
using System.IO;
using My.Scripts.Timelapse;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 웹캠 화면을 표시하고 사진 촬영, 카운트다운, 플래시 연출을 담당하는 페이지 컨트롤러입니다.
    /// 촬영된 사진은 로컬 스토리지에 저장되며, 타임랩스 레코더와 연동하여 리얼타임 녹화도 제어합니다.
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
                float sx = _selectedDevice.isFrontFacing ? -1f : 1f;

                cameraDisplay.rectTransform.localEulerAngles = new Vector3(0f, 0f, -_webCamTexture.videoRotationAngle);
                cameraDisplay.rectTransform.localScale = new Vector3(sx, sy, 1f);
            }
        }

        public override void SetupData(object data) { }

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

            CleanupPhoto();
            StartWebCam(); 

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

        /// <summary> 5초 카운트다운을 진행하며 구간별로 녹화를 제어합니다. </summary>
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
            
            // 5초
            yield return StartCoroutine(ShowAndFadeNumber("5"));
            // 4초
            yield return StartCoroutine(ShowAndFadeNumber("4"));
            // 3초
            yield return StartCoroutine(ShowAndFadeNumber("3"));

            // 5~3초 구간 종료. 리얼타임 녹화만 먼저 중단합니다.
            if (_shouldSavePhoto && TimeLapseRecorder.Instance)
            {
                TimeLapseRecorder.Instance.EnableRealtimeCapture = false;
            }

            // 2초
            yield return StartCoroutine(ShowAndFadeNumber("2"));
            // 1초
            yield return StartCoroutine(ShowAndFadeNumber("1"));

            // 5~1초 구간 종료. 타임랩스 녹화를 완전히 종료합니다.
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
                string rootPath = parentDir != null ? parentDir.FullName : dataPath;

                string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
                string folder = Path.Combine(rootPath, "Pictures", dateFolder);
                
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string path = Path.Combine(folder, $"{_photoFileName}.png");
                File.WriteAllBytes(path, bytes);
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
            if (_webCamTexture && _webCamTexture.isPlaying)
            {
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