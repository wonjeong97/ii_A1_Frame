using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.UI;

namespace My.Scripts._03_Play_Q1.Pages
{
    [Serializable]
    public class PlayQ1Page6Data
    {
        // 빈 데이터
    }

    public class PlayQ1Page6Controller : PlayQ1PageBase
    {
        [Header("Page 6 UI")]
        [SerializeField] private RawImage cameraDisplay; 
        [SerializeField] private Text countdownText;     
        
        [Header("Effects")]
        [SerializeField] private Image flashImage;       
        [SerializeField] private CanvasGroup contentCanvasGroup;
        
        [Header("Masking Settings")]
        [SerializeField] private Material maskingMaterial;

        private WebCamTexture _webCamTexture;
        private Texture2D _capturedPhoto; 
        
        // [추가] 파일명 저장을 위한 변수 (기본값 설정)
        private string _photoFileName = "Default_Q1";

        public override void SetupData(object data)
        {
        }

        // [추가] 외부(Manager)에서 파일명을 설정하는 함수
        public void SetPhotoFilename(string name)
        {
            _photoFileName = name;
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
            if (flashImage != null)
            {
                flashImage.gameObject.SetActive(true);
                SetImageAlpha(flashImage, 1f);
            }

            if (contentCanvasGroup != null)
            {
                contentCanvasGroup.alpha = 0f;
            }

            yield return new WaitForSeconds(0.05f); 

            CapturePhoto();

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

            yield return new WaitForSeconds(2.0f);

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

            Debug.Log("Q1 촬영 시퀀스 종료");
            CompleteStep(); 
        }

        private void CapturePhoto()
        {
            if (_webCamTexture != null && _webCamTexture.isPlaying)
            {
                // [수정 1] ARGB32 포맷의 렌더 텍스처 생성 (투명도 지원)
                RenderTexture rt = RenderTexture.GetTemporary(
                    _webCamTexture.width, 
                    _webCamTexture.height, 
                    0, 
                    RenderTextureFormat.ARGB32
                );

                if (maskingMaterial != null)
                {
                    Graphics.Blit(_webCamTexture, rt, maskingMaterial);
                }
                else
                {
                    Graphics.Blit(_webCamTexture, rt);
                }

                // [수정 2] RGB24 -> RGBA32 (알파 채널 포함)
                _capturedPhoto = new Texture2D(_webCamTexture.width, _webCamTexture.height, TextureFormat.RGBA32, false);

                RenderTexture currentRT = RenderTexture.active; 
                RenderTexture.active = rt;                      
                
                _capturedPhoto.ReadPixels(new Rect(0, 0, _webCamTexture.width, _webCamTexture.height), 0, 0);
                _capturedPhoto.Apply();

                RenderTexture.active = currentRT;
                RenderTexture.ReleaseTemporary(rt);

                if (cameraDisplay != null)
                {
                    cameraDisplay.texture = _capturedPhoto;
                }

                SavePhotoToTempFolder(_capturedPhoto);

                StopWebCam();
                Debug.Log($"[PlayQ1Page6] 투명 배경 사진 캡쳐 완료: {_capturedPhoto.width}x{_capturedPhoto.height}");
            }
        }

        private void SavePhotoToTempFolder(Texture2D photo)
        {
            if (photo == null) return;

            try
            {
                byte[] bytes = photo.EncodeToPNG();

                // [수정] 설정된 파일명 사용
                string filename = $"{_photoFileName}.png";
                string path = Path.Combine(Application.temporaryCachePath, filename);

                File.WriteAllBytes(path, bytes);

                Debug.Log($"[PlayQ1Page6] 사진 파일 저장됨: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayQ1Page6] 사진 저장 실패: {e.Message}");
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