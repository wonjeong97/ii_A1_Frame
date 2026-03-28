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
    /// <summary>
    /// 웹캠 제어, UI 연출 및 촬영된 사진의 로컬 저장을 담당하는 페이지.
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
        private CancellationTokenSource _hueCts;

        private const int PhotoWidth = 1920;
        private const int PhotoHeight = 1080;

        private bool _isQ6To10;
        private Color _currentTintColor = Color.white;

        /// <summary>
        /// 컴포넌트 초기화 및 기본 마스킹 매터리얼을 설정함.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            if (!_isConfigured)
            {
                _currentMaskingMaterial = defaultMaskingMaterial;
                _shouldSavePhoto = defaultSavePhoto;
            }
        }

        /// <summary>
        /// 기기별 웹캠 회전 및 반전 상태를 실시간으로 UI Transform에 동기화함.
        /// </summary>
        private void Update()
        {
            if (_webCamTexture && _webCamTexture.isPlaying && cameraDisplay)
            {
                float sy = _webCamTexture.videoVerticallyMirrored ? -1f : 1f;
                float sx = (!string.IsNullOrEmpty(_selectedDevice.name) && _selectedDevice.isFrontFacing) ? -1f : 1f;

                // ex: videoRotationAngle=90 -> localEulerAngles=(0, 0, -90)
                cameraDisplay.rectTransform.localEulerAngles = new Vector3(0f, 0f, -_webCamTexture.videoRotationAngle);
                
                // ex: sx=-1(전면 카메라), sy=1 -> localScale=(-1, 1, 1)
                cameraDisplay.rectTransform.localScale = new Vector3(sx, sy, 1f);
            }
        }

        /// <summary>
        /// 전달받은 데이터를 페이지에 적용함.
        /// </summary>
        /// <param name="data">적용할 데이터 객체</param>
        public override void SetupData(object data)
        {
        }

        /// <summary>
        /// 캡처된 사진이 로컬에 저장될 파일명을 지정함.
        /// </summary>
        /// <param name="fileName">저장할 파일명</param>
        public void SetPhotoFilename(string fileName)
        {
            _photoFileName = fileName;
        }

        /// <summary>
        /// 현재 진행 중인 레벨의 ID를 설정함.
        /// </summary>
        /// <param name="id">레벨 ID</param>
        public void SetLevelID(string id)
        {
            _levelID = id;
        }

        /// <summary>
        /// 외부 설정값으로 카메라 동작 방식을 덮어씌움.
        /// </summary>
        /// <param name="shouldSave">저장 여부</param>
        /// <param name="maskMat">적용할 마스킹 매터리얼</param>
        /// <param name="triggerEncoding">캡처 후 인코딩 강제 여부</param>
        public void Configure(bool shouldSave, Material maskMat = null, bool triggerEncoding = false)
        {
            _shouldSavePhoto = shouldSave;
            _currentMaskingMaterial = maskMat;
            _triggerEncodingOnCapture = triggerEncoding;
            _isConfigured = true;
        }

        /// <summary>
        /// 씬 전환 전 웹캠 장치를 미리 로드하여 딜레이를 줄임.
        /// </summary>
        public void PreloadCamera()
        {
            StartWebCam();
            SetRawImageAlpha(cameraDisplay, 0f);
        }

        /// <summary>
        /// 페이지 활성화 시 웹캠을 시작하고, 조명 색상을 동기화하며, UI를 초기화함.
        /// </summary>
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
                
                RGBColor fallbackWhite = new RGBColor();
                if (HueManager.Instance.Config == null || HueManager.Instance.Config.whiteColor == null)
                {
                    Debug.LogWarning("HueManager.Config 또는 whiteColor 설정값이 누락됨.");
                }
                else
                {
                    fallbackWhite = HueManager.Instance.Config.whiteColor;
                }

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

        /// <summary>
        /// 진행 중인 Hue 조명 명령을 취소하고 조명을 소등함.
        /// </summary>
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

        /// <summary>
        /// 페이지 비활성화 시 사용 중인 리소스를 해제함.
        /// </summary>
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

        /// <summary>
        /// 객체 파괴 시 메모리 누수를 방지하기 위해 텍스처를 할당 해제함.
        /// </summary>
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

        /// <summary>
        /// 웹캠 초기화 지연 시간을 고려하여 카메라 피드를 서서히 밝게 연출함.
        /// </summary>
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
                // ex: timer=0.25, duration=0.5 -> 알파 0.5 (50%)
                SetRawImageAlpha(cameraDisplay, Mathf.Lerp(0f, 1f, timer / cameraFadeDuration));
                yield return null;
            }

            SetRawImageAlpha(cameraDisplay, 1f);
        }

        /// <summary>
        /// 사진 촬영 전 카운트다운을 진행하고 타임랩스 녹화 상태를 제어함.
        /// </summary>
        private IEnumerator CountdownRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(1.0f + cameraFadeDelay);

            if (_shouldSavePhoto && TimeLapseRecorder.Instance)
            {
                if (_webCamTexture && _webCamTexture.isPlaying)
                {
                    TimeLapseRecorder.Instance.SetCurrentLevel(_levelID);
                    
                    int qNum = LevelManager.Instance ? LevelManager.Instance.CurrentQuestionNumber : 0;
                    
                    TimeLapseRecorder.Instance.EnableTimelapseCapture = true;
                    TimeLapseRecorder.Instance.EnableRealtimeCapture = (qNum >= 11 && qNum <= 15);
                    TimeLapseRecorder.Instance.StartCapture(_webCamTexture);
                }
                else
                {
                    Debug.LogError("웹캠 오류로 타임랩스를 녹화할 수 없음.");
                }
            }

            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_10_3초");

            yield return StartCoroutine(ShowAndFadeNumber("3"));
            yield return StartCoroutine(ShowAndFadeNumber("2"));
            yield return StartCoroutine(ShowAndFadeNumber("1"));

            if (_shouldSavePhoto && TimeLapseRecorder.Instance)
            {
                TimeLapseRecorder.Instance.EnableRealtimeCapture = false;
                TimeLapseRecorder.Instance.EnableTimelapseCapture = false;
                TimeLapseRecorder.Instance.StopCapture();
            }

            yield return StartCoroutine(FlashAndCaptureRoutine());
        }

        /// <summary>
        /// 카메라 플래시 연출을 실행하고 실제 사진 데이터를 캡처함.
        /// </summary>
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
                    // ex: maxAlpha=0.8, t=0.25 -> 알파 0.4 (절반 감소)
                    SetImageAlpha(flashImage, Mathf.Lerp(maxAlpha, 0f, t / 0.5f));
                    yield return null;
                }

                flashImage.gameObject.SetActive(false);
            }

            yield return CoroutineData.GetWaitForSeconds(2.0f);

            CompleteStep();
        }

        /// <summary>
        /// 웹캠 텍스처를 RenderTexture로 변환하여 파일로 저장할 이미지를 생성함.
        /// </summary>
        private void CapturePhoto()
        {
            if (!_webCamTexture || !_webCamTexture.isPlaying)
            {
                Debug.LogError("웹캠이 중지되어 캡처할 수 없음.");
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
                    // ex: p.r=200, tr=0.5 -> 신규 Red값 100
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

        /// <summary>
        /// 생성된 텍스처 데이터를 백그라운드 스레드에서 PNG 파일로 인코딩하여 로컬에 저장함.
        /// </summary>
        /// <param name="photo">저장할 원본 텍스처 데이터</param>
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
                    byte[] bytes = UnityEngine.ImageConversion.EncodeArrayToPNG(rawData, format, (uint)width, (uint)height);

                    DirectoryInfo parentDir = Directory.GetParent(dataPath);
                    string rootPath = parentDir != null ? parentDir.FullName : dataPath;
                    
                    string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
                    string folder = Path.Combine(rootPath, "Pictures", dateFolder);

                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    string path = Path.Combine(folder, $"{photoName}.png");
                    File.WriteAllBytes(path, bytes);

                    Debug.Log($"원본 사진 저장 완료: {path}");
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"사진 저장 실패: {e.Message}");
            }
        }

        /// <summary>
        /// UI 상에 남아있는 이전 사진의 잔상을 지움.
        /// </summary>
        private void CleanupPhotoUI()
        {
            if (cameraDisplay && cameraDisplay.texture == _capturedPhoto)
            {
                cameraDisplay.texture = null;
            }
        }

        /// <summary>
        /// 지정된 숫자를 텍스트 UI에 출력하고 페이드아웃 효과를 적용함.
        /// </summary>
        /// <param name="n">표시할 문자열</param>
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

        /// <summary>
        /// 텍스트 컴포넌트의 투명도를 설정함.
        /// </summary>
        /// <param name="a">목표 알파값 (0~1)</param>
        private void SetTextAlpha(float a)
        {
            if (countdownText)
            {
                Color c = countdownText.color;
                c.a = a;
                countdownText.color = c;
            }
        }

        /// <summary>
        /// Image UI 컴포넌트의 투명도를 설정함.
        /// </summary>
        /// <param name="i">대상 Image</param>
        /// <param name="a">목표 알파값 (0~1)</param>
        private void SetImageAlpha(Image i, float a)
        {
            if (i)
            {
                Color c = i.color;
                c.a = a;
                i.color = c;
            }
        }

        /// <summary>
        /// RawImage UI 컴포넌트의 투명도를 설정함.
        /// </summary>
        /// <param name="ri">대상 RawImage</param>
        /// <param name="a">목표 알파값 (0~1)</param>
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
        /// 연결된 웹캠 하드웨어를 검색하여 피드 재생을 시작함.
        /// </summary>
        private void StartWebCam()
        {
            if (_webCamTexture && _webCamTexture.isPlaying) return;

            _selectedDevice = default;

            if (cameraDisplay)
            {
                WebCamDevice[] devices = WebCamTexture.devices;
                if (devices.Length == 0)
                {
                    Debug.LogError("웹캠 장치가 없음.");
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
                    Debug.LogError($"웹캠 예외 발생: {e.Message}");
                }
            }
        }

        /// <summary>
        /// 재생 중인 웹캠 피드를 중지함.
        /// </summary>
        private void StopWebCam()
        {
            if (_webCamTexture && _webCamTexture.isPlaying)
            {
                _webCamTexture.Stop();
            }
            _webCamTexture = null;
        }
    }
}