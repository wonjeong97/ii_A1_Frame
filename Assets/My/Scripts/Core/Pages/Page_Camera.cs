using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace My.Scripts.Core.Pages
{
    /// <summary> 카메라 촬영 및 사진 저장 페이지 컨트롤러 </summary>
    public class Page_Camera : GamePage
    {
        [Header("UI References")] 
        [SerializeField] private RawImage cameraDisplay; // 카메라 화면 출력용 RawImage
        [SerializeField] private Text countdownText; // 카운트다운 텍스트

        [Header("Effects")] 
        [SerializeField] private Image flashImage; // 플래시 효과 이미지
        [SerializeField] private CanvasGroup contentCanvasGroup; // UI 콘텐츠 그룹

        [Header("Default Settings (Inspector)")] 
        [SerializeField] private Material defaultMaskingMaterial; // 기본 마스킹 재질
        [SerializeField] private bool defaultSavePhoto = true; // 기본 사진 저장 여부

        // 내부 변수
        private Material _currentMaskingMaterial; // 현재 적용된 마스킹 재질
        private bool _shouldSavePhoto; // 사진 저장 여부
        private bool _isConfigured = false; // 외부 설정 완료 여부

        private WebCamTexture _webCamTexture; // 웹캠 텍스처
        private Texture2D _capturedPhoto; // 캡처된 사진 텍스처
        private string _photoFileName = "Default_Photo"; // 저장될 파일명
        
        private const int PhotoWidth = 1920; // 촬영 해상도 너비
        private const int PhotoHeight = 1080; // 촬영 해상도 높이

        protected override void Awake()
        {
            base.Awake();
            
            // 외부 설정이 없으면 기본값 적용
            if (!_isConfigured)
            {
                _currentMaskingMaterial = defaultMaskingMaterial;
                _shouldSavePhoto = defaultSavePhoto;
            }
        }

        /// <summary> 데이터 설정 (사용 안 함) </summary>
        public override void SetupData(object data) { }

        /// <summary> 사진 파일명 설정 </summary>
        public void SetPhotoFilename(string fileName)
        {
            _photoFileName = fileName;
        }

        /// <summary> 외부 설정 적용 (저장 여부, 마스크) </summary>
        public void Configure(bool shouldSave, Material maskMat = null)
        {
            _shouldSavePhoto = shouldSave;
            _currentMaskingMaterial = maskMat;
            
            _isConfigured = true; // 설정 완료 플래그
            
            Debug.Log($"[Page_Camera] Configured: Save={_shouldSavePhoto}, Mask={(maskMat != null ? maskMat.name : "None")}");
        }

        /// <summary> 페이지 진입 (카메라 시작 및 카운트다운) </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            SetAlpha(1f);

            // UI 초기화
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

            // 카메라 시작
            CleanupPhoto();
            StartWebCam();
            StartCoroutine(CountdownRoutine());
        }

        /// <summary> 페이지 퇴장 (정리) </summary>
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

        /// <summary> 카운트다운 및 촬영 시퀀스 </summary>
        private IEnumerator CountdownRoutine()
        {   
            yield return new WaitForSeconds(1.0f);
            
            // 타임랩스 캡처 시작 (레코더 연동)
            if (TimeLapseRecorder.Instance != null && _webCamTexture != null)
            {
                TimeLapseRecorder.Instance.StartCapture(_webCamTexture);
            }

            // 3, 2, 1 카운트다운
            yield return StartCoroutine(ShowAndFadeNumber("3"));
            yield return StartCoroutine(ShowAndFadeNumber("2"));
            yield return StartCoroutine(ShowAndFadeNumber("1"));
            
            // 타임랩스 캡처 종료
            if (TimeLapseRecorder.Instance != null)
            {
                TimeLapseRecorder.Instance.StopCapture();
            }
            
            // 촬영 실행
            yield return StartCoroutine(FlashAndCaptureRoutine());
        }

        /// <summary> 플래시 효과 및 캡처 실행 </summary>
        private IEnumerator FlashAndCaptureRoutine()
        {
            float maxAlpha = 0.8f;

            // 플래시 켜기
            if (flashImage)
            {
                flashImage.gameObject.SetActive(true);
                SetImageAlpha(flashImage, maxAlpha);
            }

            // UI 숨기기
            if (contentCanvasGroup) contentCanvasGroup.alpha = 0f;

            yield return new WaitForSeconds(0.05f);

            // 실제 캡처 수행
            CapturePhoto();

            // 플래시 페이드 아웃
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
            CompleteStep(); // 단계 완료
        }

        /// <summary> 웹캠 화면 캡처 및 저장 처리 </summary>
        private void CapturePhoto()
        {
            if (_webCamTexture != null && _webCamTexture.isPlaying)
            {
                // 1. 렌더 텍스처 준비
                RenderTexture rt = RenderTexture.GetTemporary(PhotoWidth, PhotoHeight, 0, RenderTextureFormat.ARGB32);

                // 2. 마스킹 적용 (있으면)
                Material maskToUse = _currentMaskingMaterial;
                if (maskToUse != null) Graphics.Blit(_webCamTexture, rt, maskToUse);
                else Graphics.Blit(_webCamTexture, rt);

                // 3. 텍스처로 변환
                _capturedPhoto = new Texture2D(PhotoWidth, PhotoHeight, TextureFormat.RGBA32, false);

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                _capturedPhoto.ReadPixels(new Rect(0, 0, PhotoWidth, PhotoHeight), 0, 0);
                _capturedPhoto.Apply();

                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                // 4. 화면에 결과 표시
                if (cameraDisplay) cameraDisplay.texture = _capturedPhoto;

                // 5. 파일 저장 (설정된 경우)
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

        /// <summary> 캡처된 사진 파일 저장 </summary>
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
                Debug.Log($"[Page_Camera] 저장됨: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Page_Camera] 저장 실패: {e.Message}");
            }
        }

        /// <summary> 리소스 정리 </summary>
        private void CleanupPhoto()
        {
            if (_capturedPhoto)
            {
                Destroy(_capturedPhoto);
                _capturedPhoto = null;
            }
        }

        /// <summary> 숫자 표시 및 페이드 효과 </summary>
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

        /// <summary> 웹캠 구동 시작 </summary>
        private void StartWebCam()
        {
            if (cameraDisplay && _webCamTexture == null)
            {
                _webCamTexture = new WebCamTexture(PhotoWidth, PhotoHeight);
                cameraDisplay.texture = _webCamTexture;
                _webCamTexture.Play();
            }
        }

        /// <summary> 웹캠 구동 정지 </summary>
        private void StopWebCam()
        {
            if (_webCamTexture && _webCamTexture.isPlaying) _webCamTexture.Stop();
            _webCamTexture = null;
        }
    }
}