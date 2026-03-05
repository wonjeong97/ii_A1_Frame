using System;
using System.Collections;
using System.IO;
using My.Scripts.Timelapse;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
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
        private WebCamDevice _selectedDevice; // [추가] 현재 선택된 웹캠 디바이스 정보
        private Texture2D _capturedPhoto;
        private string _photoFileName = "Default_Photo";
        
        // 현재 레벨 ID (리얼타임 녹화 시 레벨별 분기 처리를 위해 필요)
        private string _levelID; 

        private const int PhotoWidth = 1920;
        private const int PhotoHeight = 1080;

        protected override void Awake()
        {
            base.Awake();
            
            // 외부 설정이 들어오지 않았을 경우를 대비한 기본값 초기화
            if (!_isConfigured)
            {
                _currentMaskingMaterial = defaultMaskingMaterial;
                _shouldSavePhoto = defaultSavePhoto;
            }
        }

        /// <summary> 매 프레임 웹캠의 최신 속성(상하 반전, 회전, 전면 카메라 여부)을 확인하여 프리뷰 UI를 보정합니다. </summary>
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

        public override void SetupData(object data)
        {
            // 이 페이지는 정적 데이터(텍스트 등)보다는 런타임 설정(Configure 함수)을 주로 사용함
        }

        public void SetPhotoFilename(string fileName)
        {
            _photoFileName = fileName;
        }

        /// <summary>
        /// 현재 플레이 중인 레벨 ID를 설정합니다. (LevelManager 연동)
        /// 타임랩스 레코더가 이 ID를 보고 리얼타임 녹화 여부를 결정합니다.
        /// </summary>
        public void SetLevelID(string id)
        {
            _levelID = id;
        }

        /// <summary>
        /// 카메라 페이지의 동작 모드를 설정합니다.
        /// </summary>
        /// <param name="shouldSave">사진 저장 여부</param>
        /// <param name="maskMat">적용할 마스킹 재질 (없으면 null)</param>
        /// <param name="triggerEncoding">촬영 후 즉시 영상 변환을 시도할지 여부</param>
        public void Configure(bool shouldSave, Material maskMat = null, bool triggerEncoding = false)
        {
            _shouldSavePhoto = shouldSave;
            _currentMaskingMaterial = maskMat;
            _triggerEncodingOnCapture = triggerEncoding;
            _isConfigured = true;
        }

        /// <summary>
        /// 페이지 진입 전 웹캠을 미리 초기화합니다.
        /// 웹캠 구동 시 발생하는 초기 딜레이(검은 화면)를 사용자에게 보여주지 않기 위함입니다.
        /// </summary>
        public void PreloadCamera()
        {
            StartWebCam();
            SetRawImageAlpha(cameraDisplay, 0f); // 로딩 중에는 화면을 숨김
        }

        public override void OnEnter()
        {
            base.OnEnter();
            SetAlpha(1f);

            // 초기 상태: 카메라는 투명하게 시작하여 서서히 페이드 인
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
            StartWebCam(); // Preload가 안 되었을 경우를 대비한 안전장치

            StartCoroutine(FadeInCameraRoutine());
            StartCoroutine(CountdownRoutine());
        }

        public override void OnExit()
        {
            StopAllCoroutines();
            base.OnExit();
            
            StopWebCam();
            CleanupPhoto();

            // 마지막 15번째 질문(Q15) 촬영이 끝나는 즉시 리얼타임 영상 변환 시작
            string currentScene = SceneManager.GetActiveScene().name;
            if (TimeLapseRecorder.Instance && string.Equals(_levelID, "Q15", StringComparison.OrdinalIgnoreCase))
            {
                if (!TimeLapseRecorder.Instance.IsRealtimeProcessing && string.IsNullOrEmpty(TimeLapseRecorder.Instance.LastRealtimeVideoPath))
                {
                    Debug.Log($"[Page_Camera] OnExit: 리얼타임 영상 변환 조기 시작 (Scene: {currentScene})");
                    TimeLapseRecorder.Instance.ConvertToRealtimeVideo();
                }
            }
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            StopWebCam();
            CleanupPhoto();
        }

        /// <summary>
        /// 카메라 화면을 부드럽게 페이드 인 합니다.
        /// 웹캠 텍스처가 실제로 준비될 때까지 기다린 후 연출을 시작합니다.
        /// </summary>
        private IEnumerator FadeInCameraRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(cameraFadeDelay);

            // 웹캠 초기화 대기 (너비가 16 이하인 경우 초기화 덜 된 것으로 간주)
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

        /// <summary> 3, 2, 1 카운트다운을 진행하고 촬영 시점을 잡습니다. </summary>
        private IEnumerator CountdownRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(1.0f + cameraFadeDelay);

            // 카운트다운 시작과 동시에 타임랩스/리얼타임 녹화 시작
            if (_shouldSavePhoto && TimeLapseRecorder.Instance && _webCamTexture)
            {
                TimeLapseRecorder.Instance.SetCurrentLevel(_levelID);
                TimeLapseRecorder.Instance.StartCapture(_webCamTexture);
            }
            
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_10_3초");
            yield return StartCoroutine(ShowAndFadeNumber("3"));
            yield return StartCoroutine(ShowAndFadeNumber("2"));
            yield return StartCoroutine(ShowAndFadeNumber("1"));

            // 촬영 직전 녹화 종료 (촬영 찰나의 멈춤 방지 및 프레임 확보 완료)
            if (_shouldSavePhoto && TimeLapseRecorder.Instance)
            {
                TimeLapseRecorder.Instance.StopCapture();
            }

            yield return StartCoroutine(FlashAndCaptureRoutine());
        }

        /// <summary> 플래시(하얀 화면) 연출과 함께 실제 사진을 캡처합니다. </summary>
        private IEnumerator FlashAndCaptureRoutine()
        {
            float maxAlpha = 0.8f;

            // 카메라 촬영 효과
            if (flashImage)
            {   
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_11");
                flashImage.gameObject.SetActive(true);
                SetImageAlpha(flashImage, maxAlpha);
            }

            // 콘텐츠 숨김 (사진만 남기고 UI 등은 가림)
            if (contentCanvasGroup) contentCanvasGroup.alpha = 0f;

            yield return CoroutineData.GetWaitForSeconds(0.05f); // 플래시 타이밍 미세 조정

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

            // 결과 확인 시간
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            CompleteStep();
        }

        /// <summary>
        /// 웹캠의 현재 프레임을 캡처하여 Texture2D로 변환합니다.
        /// 마스킹 재질이 있다면 쉐이더를 적용하여 저장합니다.
        /// </summary>
        private void CapturePhoto()
        {
            if (_webCamTexture && _webCamTexture.isPlaying)
            {
                RenderTexture rt = RenderTexture.GetTemporary(PhotoWidth, PhotoHeight, 0, RenderTextureFormat.ARGB32);

                // GPU 상에서 텍스처 복사 (마스킹 적용)
                Material maskToUse = _currentMaskingMaterial;
                if (maskToUse) Graphics.Blit(_webCamTexture, rt, maskToUse);
                else Graphics.Blit(_webCamTexture, rt);

                // 매번 텍스처를 새로 생성
                _capturedPhoto = new Texture2D(PhotoWidth, PhotoHeight, TextureFormat.RGBA32, false);

                // RenderTexture -> Texture2D 데이터 전송
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                _capturedPhoto.ReadPixels(new Rect(0, 0, PhotoWidth, PhotoHeight), 0, 0);
                _capturedPhoto.Apply();

                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                // 촬영된 결과물을 화면에 고정 표시
                if (cameraDisplay) cameraDisplay.texture = _capturedPhoto;

                if (_shouldSavePhoto)
                {
                    SavePhotoToCustomFolder(_capturedPhoto);
                }
                else
                {
                    Debug.Log($"[Page_Camera] 저장 건너뜀 (shouldSavePhoto: false)");
                }

                // 촬영 후 카메라는 정지 (정지 이미지를 보여주기 위해)
                StopWebCam();
            }
        }
    
        /// <summary> 캡처된 텍스처를 PNG 파일로 로컬 경로에 저장합니다. </summary>
        private void SavePhotoToCustomFolder(Texture2D photo)
        {
            if (!photo) return;
            try
            {
                byte[] bytes = photo.EncodeToPNG();
                
                string dataPath = Application.dataPath;
                DirectoryInfo parentDir = Directory.GetParent(dataPath);
                string rootPath = (parentDir != null) ? parentDir.FullName : dataPath;

                // 날짜별 폴더 관리
                string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
                string folder = Path.Combine(rootPath, "Pictures", dateFolder);
                
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

        /// <summary> 숫자 텍스트를 페이드 아웃 효과와 함께 표시합니다. </summary>
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

        /// <summary>
        /// 사용 가능한 웹캠 디바이스를 찾아 카메라를 실행합니다.
        /// "USB Video" 이름을 가진 장치를 우선적으로 선택합니다.
        /// </summary>
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

                // 특정 외부 카메라("USB Video") 우선 검색
                for (int i = 0; i < devices.Length; i++)
                {
                    if (devices[i].name == "USB Video")
                    {
                        selectedDeviceName = devices[i].name;
                        _selectedDevice = devices[i]; // 디바이스 정보 캐싱
                        break;
                    }
                }

                // 없으면 첫 번째 장치 사용
                if (string.IsNullOrEmpty(selectedDeviceName) && devices.Length > 0)
                {
                    selectedDeviceName = devices[0].name;
                    _selectedDevice = devices[0]; // 디바이스 정보 캐싱
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