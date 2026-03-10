using System;
using System.Collections;
using System.IO;
using Cysharp.Threading.Tasks; // UniTask 사용을 위해 추가
using My.Scripts.Timelapse;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 웹캠 화면 출력, 사진 촬영, 카운트다운 및 플래시 연출을 전담하는 페이지 컨트롤러입니다.
    /// 로컬 파일 저장과 타임랩스/리얼타임 녹화 모듈과의 동기화를 관리합니다.
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

        /// <summary> 초기화 시 외부 매니저의 설정(Configure) 개입이 없었을 경우에만 기본값을 할당합니다. </summary>
        protected override void Awake()
        {
            base.Awake();
            
            if (!_isConfigured)
            {
                _currentMaskingMaterial = defaultMaskingMaterial;
                _shouldSavePhoto = defaultSavePhoto;
            }
        }

        /// <summary> 물리적 카메라의 설치 방향 및 전/후면 여부에 맞춰 UI 상의 영상을 올바르게 반전 및 회전시킵니다. </summary>
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

        /// <summary> 상속 구조 유지를 위한 빈 구현부 </summary>
        public override void SetupData(object data) { }

        /// <summary> 로컬에 저장될 사진 파일명 지정 </summary>
        public void SetPhotoFilename(string fileName)
        {
            _photoFileName = fileName;
        }

        /// <summary> 타임랩스 기록 및 관리에 사용될 레벨 ID 지정 </summary>
        public void SetLevelID(string id)
        {
            _levelID = id;
        }

        /// <summary> 외부 매니저에서 카메라 옵션(마스킹 재질, 저장 여부 등)을 동적으로 주입하기 위해 사용합니다. </summary>
        public void Configure(bool shouldSave, Material maskMat = null, bool triggerEncoding = false)
        {
            _shouldSavePhoto = shouldSave;
            _currentMaskingMaterial = maskMat;
            _triggerEncodingOnCapture = triggerEncoding;
            _isConfigured = true; 
        }

        /// <summary> 화면 등장 전 카메라 하드웨어 로딩 지연을 숨기기 위해 미리 가동합니다. </summary>
        public void PreloadCamera()
        {
            StartWebCam();
            SetRawImageAlpha(cameraDisplay, 0f); 
        }

        /// <summary> 페이지 진입 시 이전 잔상을 제거하고 연출 시퀀스를 가동합니다. </summary>
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

            StartCoroutine(FadeInCameraRoutine());
            StartCoroutine(CountdownRoutine());
        }

        /// <summary> 페이지 퇴장 시 리소스 누수 방지를 위해 카메라를 즉시 종료합니다. </summary>
        public override void OnExit()
        {
            StopAllCoroutines();
            base.OnExit();
            
            StopWebCam();
            CleanupPhotoUI();
        }

        /// <summary> 객체 파괴 시 카메라 하드웨어 점유를 강제로 해제하고 재사용 버퍼 메모리를 반환합니다. </summary>
        private void OnDestroy()
        {
            StopAllCoroutines();
            StopWebCam();
            
            if (_capturedPhoto)
            {
                Destroy(_capturedPhoto);
                _capturedPhoto = null;
            }
        }

        /// <summary> 웹캠 초기화 시 발생하는 프레임 드랍 및 끊김 현상을 숨기기 위해 부드럽게 페이드인합니다. </summary>
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

        /// <summary> 5초 카운트다운을 진행하며, 타임랩스와 리얼타임 녹화의 생명주기를 타이밍에 맞춰 동기화합니다. </summary>
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

        /// <summary> 사진 촬영 순간의 시각적 피드백(플래시)을 연출하고 실제 촬영 로직을 호출합니다. </summary>
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

        /// <summary> 
        /// WebCamTexture의 픽셀 데이터를 읽어와 마스킹 재질을 적용한 뒤 메모리에 고정시킵니다.
        /// 가비지 컬렉션 스파이크를 방지하기 위해 단일 텍스처 인스턴스를 재사용합니다.
        /// </summary>
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
                    // 비동기(UniTask) 사진 저장 호출 (Fire and Forget)
                    SavePhotoToCustomFolderAsync(_capturedPhoto).Forget();
                }

                StopWebCam();
            }
        }
    
        /// <summary> 
        /// 메인 스레드 멈춤(프리징) 현상을 방지하기 위해 UniTask를 활용하여 
        /// PNG 인코딩 및 디스크 I/O를 스레드 풀에서 비동기로 처리합니다.
        /// </summary>
        private async UniTaskVoid SavePhotoToCustomFolderAsync(Texture2D photo)
        {
            if (!photo)
            {
                Debug.LogError("[Page_Camera] 캡처된 텍스처가 존재하지 않아 저장을 취소합니다.");
                return;
            }
            
            // 메인 스레드 종속적인 Raw 데이터와 포맷 정보만 미리 추출
            byte[] rawData = photo.GetRawTextureData();
            int width = photo.width;
            int height = photo.height;
            UnityEngine.Experimental.Rendering.GraphicsFormat format = photo.graphicsFormat;
            
            string dataPath = Application.dataPath;
            string photoName = _photoFileName;

            try
            {
                // UniTask를 통해 유니티 메인 스레드 부하 없이 백그라운드 스레드에서 무거운 작업 실행
                await UniTask.RunOnThreadPool(() =>
                {
                    byte[] bytes = UnityEngine.ImageConversion.EncodeArrayToPNG(rawData, format, (uint)width, (uint)height);
                    
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

        /// <summary> 이전 촬영 결과물이 UI에 번쩍이며 나타나는 현상을 방지하기 위해 텍스처 참조만 해제합니다. </summary>
        private void CleanupPhotoUI()
        {
            if (cameraDisplay && cameraDisplay.texture == _capturedPhoto)
            {
                cameraDisplay.texture = null;
            }
        }

        /// <summary> 카운트다운 숫자를 표시하고 서서히 투명해지는 연출을 수행합니다. </summary>
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

        /// <summary> 연결된 카메라 장치를 검색하고 하드웨어를 가동합니다. </summary>
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

        /// <summary> 카메라 하드웨어 작동 중지 </summary>
        private void StopWebCam()
        {
            if (_webCamTexture && _webCamTexture.isPlaying) _webCamTexture.Stop();
            _webCamTexture = null;
        }
    }
}