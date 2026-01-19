using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.UI;

namespace My.Scripts._02_Play_Tutorial.Pages
{
    [Serializable]
    public class PlayTutorialPage5Data
    {
        // 빈 데이터
    }

    public class PlayTutorialPage5Controller : PlayTutorialPageBase
    {
        [Header("Page 5 UI")]
        [SerializeField] private RawImage cameraDisplay; 
        [SerializeField] private Text countdownText;     
        
        [Header("Effects")]
        [SerializeField] private Image flashImage;       
        [SerializeField] private CanvasGroup contentCanvasGroup; 

        private WebCamTexture _webCamTexture;
        private Texture2D _capturedPhoto; 

        public override void SetupData(object data)
        {
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            SetAlpha(1f);
            
            if (countdownText != null)
            {
                countdownText.text = "";
                SetTextAlpha(0f);
            }
            if (flashImage != null)
            {
                flashImage.gameObject.SetActive(false);
                SetImageAlpha(flashImage, 0f);
            }
            if (contentCanvasGroup != null)
            {
                contentCanvasGroup.alpha = 1f;
            }
            
            CleanupPhoto();
            StartWebCam();
            StartCoroutine(CountdownRoutine());
        }

        public override void OnExit()
        {
            base.OnExit();
            StopWebCam();
            CleanupPhoto(); 
        }

        private void OnDestroy()
        {
            CleanupPhoto();
        }

        private IEnumerator CountdownRoutine()
        {
            yield return new WaitForSeconds(1.0f);

            yield return StartCoroutine(ShowAndFadeNumber("3"));
            yield return StartCoroutine(ShowAndFadeNumber("2"));
            yield return StartCoroutine(ShowAndFadeNumber("1"));

            yield return StartCoroutine(FlashAndCaptureRoutine());
        }

        private IEnumerator FlashAndCaptureRoutine()
        {
            // 1. [플래시 ON] 흰색 이미지를 켜서 화면을 가림
            if (flashImage != null)
            {
                flashImage.gameObject.SetActive(true);
                SetImageAlpha(flashImage, 1f);
            }

            // 2. 캔버스 그룹 숨김
            if (contentCanvasGroup != null)
            {
                contentCanvasGroup.alpha = 0f;
            }

            // 화면이 완전히 하얗게 된 상태를 확실히 보여주기 위해 잠시 대기
            // "반짝하는 사이"에 멈추게 하려면, 흰색이 된 후 -> 캡쳐(정지) -> 흰색 사라짐 순서여야 함
            yield return new WaitForSeconds(0.05f); 

            // 3. [캡쳐] 흰 화면 뒤에서 카메라 정지 및 텍스처 교체
            CapturePhoto();

            // 4. [플래시 OFF] 흰색 이미지가 서서히 사라짐 -> 찍힌 사진이 드러남
            if (flashImage != null)
            {
                float duration = 0.5f;
                float timer = 0f;
                while (timer < duration)
                {
                    timer += Time.deltaTime;
                    float alpha = Mathf.Lerp(1f, 0f, timer / duration);
                    SetImageAlpha(flashImage, alpha);
                    yield return null;
                }
                SetImageAlpha(flashImage, 0f);
                flashImage.gameObject.SetActive(false);
            }

            // 5. [대기] 사진 확인 (2초)
            yield return new WaitForSeconds(2.0f);

            // 6. [페이드 아웃]
            if (FadeManager.Instance != null)
            {
                bool fadeDone = false;
                FadeManager.Instance.FadeOut(1.0f, () => fadeDone = true);
                while (!fadeDone) yield return null;
            }
            else
            {
                yield return new WaitForSeconds(1.0f);
            }

            Debug.Log("모든 플레이 시퀀스 종료");
            CompleteStep(); 
        }

        private void CapturePhoto()
        {
            if (_webCamTexture != null && _webCamTexture.isPlaying)
            {
                _capturedPhoto = new Texture2D(_webCamTexture.width, _webCamTexture.height);
                _capturedPhoto.SetPixels(_webCamTexture.GetPixels());
                _capturedPhoto.Apply();

                if (cameraDisplay != null)
                {
                    cameraDisplay.texture = _capturedPhoto;
                }

                StopWebCam();
                Debug.Log($"[PlayPage5] 사진 캡쳐 완료: {_capturedPhoto.width}x{_capturedPhoto.height}");
            }
        }

        private void CleanupPhoto()
        {
            if (_capturedPhoto != null)
            {
                Destroy(_capturedPhoto);
                _capturedPhoto = null;
            }
        }

        private IEnumerator ShowAndFadeNumber(string number)
        {
            if (countdownText == null) yield break;

            countdownText.text = number;
            SetTextAlpha(1f);

            float duration = 1.0f;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, timer / duration);
                SetTextAlpha(alpha);
                yield return null;
            }
            SetTextAlpha(0f);
        }

        private void SetTextAlpha(float alpha)
        {
            if (countdownText == null) return;
            Color c = countdownText.color;
            c.a = alpha;
            countdownText.color = c;
        }

        private void SetImageAlpha(Image img, float alpha)
        {
            if (img == null) return;
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }

        private void StartWebCam()
        {
            if (cameraDisplay == null) return;

            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0) return;

            if (_webCamTexture != null && _webCamTexture.isPlaying) 
                return;

            _webCamTexture = new WebCamTexture();
            cameraDisplay.texture = _webCamTexture;
            _webCamTexture.Play();
        }

        private void StopWebCam()
        {
            if (_webCamTexture != null)
            {
                if (_webCamTexture.isPlaying) _webCamTexture.Stop();
                _webCamTexture = null;
            }
        }
    }
}