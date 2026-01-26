using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using My.Scripts.Core;    
using My.Scripts.Global;  
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._18_Ending.Pages
{
    [Serializable]
    public class EndingPage1Data
    {
        public TextSetting descriptionText;
    }

    public class EndingPage1Controller : GamePage
    {
        [Header("UI References")]
        [SerializeField] private RawImage videoDisplay; 
        [SerializeField] private VideoPlayer videoPlayer; 
        [SerializeField] private Text descriptionText;

        [Header("Settings")]
        [SerializeField] private string videoFolderName = "Timelapse"; 
        [SerializeField] private string videoFileName = "Final_Timelapse.mp4";

        public override void SetupData(object data) 
        {
            var pageData = data as EndingPage1Data;
            if (pageData == null) return;

            if (descriptionText) 
            {
                UIManager.Instance.SetText(descriptionText.gameObject, pageData.descriptionText);
            }
        }

        public override void OnEnter()
        {
            base.OnEnter(); // Page Alpha = 1
            
            // UI 요소들을 투명하게 설정
            SetTextAlpha(descriptionText, 0f);
            SetImageAlpha(videoDisplay, 0f);

            // 시퀀스 시작
            StartCoroutine(PresentationRoutine());
        }

        public override void OnExit()
        {
            base.OnExit();
            StopAllCoroutines();
        }

        private IEnumerator PresentationRoutine()
        {   
            if (videoPlayer == null || videoDisplay == null)
            {
                Debug.LogError("[EndingPage1] VideoPlayer 또는 VideoDisplay가 할당되지 않았습니다.");
                CompleteStep();
                yield break;
            }
            
            // ------------------------------------------------------------------
            // 1. Description Text 페이드 인 (1초)
            // ------------------------------------------------------------------
            yield return StartCoroutine(FadeText(descriptionText, 0f, 1f, 1.0f));
            
            // ------------------------------------------------------------------
            // 2. 영상 파일 준비 (로딩 대기)
            // ------------------------------------------------------------------
            string filePath = GetVideoPath();
            
            // 2-1. FFmpeg 변환 대기
            if (TimeLapseRecorder.Instance != null)
            {
                float processingTimeout = 60f;
                float processingWait = 0f;
                while (TimeLapseRecorder.Instance.IsProcessing && processingWait < processingTimeout)
                {
                    Debug.Log("[EndingPage1] FFmpeg 변환 중...");
                    yield return new WaitForSeconds(0.5f);
                    processingWait += 0.5f;
                }
                
                if (!TimeLapseRecorder.Instance.IsConversionSuccessful)
                {
                    Debug.LogError($"[EndingPage1] 변환 실패. 종료합니다.");
                    CompleteStep();
                    yield break; 
                }
            }

            // 2-2. 파일 시스템 동기화 대기
            float waitTime = 0f;
            while (!File.Exists(filePath) && waitTime < 5.0f)
            {
                yield return new WaitForSeconds(0.2f);
                waitTime += 0.2f;
            }

            if (!File.Exists(filePath))
            {
                Debug.LogError($"[EndingPage1] 파일을 찾을 수 없음: {filePath}");
                CompleteStep();
                yield break;
            }

            // 2-3. VideoPlayer 준비
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = "file://" + filePath; 
            videoPlayer.renderMode = VideoRenderMode.APIOnly; 
            videoPlayer.isLooping = false; // 반복 재생 끔
            videoPlayer.Prepare();

            float prepareWait = 0f;
            while (!videoPlayer.isPrepared && prepareWait < 10f)
            {
                yield return null;
                prepareWait += Time.deltaTime;
            }

            if (!videoPlayer.isPrepared)
            {
                Debug.LogError("[EndingPage1] VideoPlayer 준비 실패.");
                CompleteStep();
                yield break;
            }

            // 텍스처 연결
            videoDisplay.texture = videoPlayer.texture;

            // ------------------------------------------------------------------
            // 3. RawImage 페이드 인 (1초) & 영상 재생
            // ------------------------------------------------------------------
            videoPlayer.Play();
            yield return StartCoroutine(FadeRawImage(videoDisplay, 0f, 1f, 1.0f));

            // ------------------------------------------------------------------
            // 4. 영상 끝날 때까지 대기
            // ------------------------------------------------------------------
            // 영상 길이(초)를 가져옴
            double duration = videoPlayer.length;
            if (duration > 0)
            {
                yield return new WaitForSeconds((float)duration);
            }
            
            // 아직 재생 중이라면 멈출 때까지 대기 (최대 2초 추가)
            float safetyWait = 0f;
            while (videoPlayer.isPlaying && safetyWait < 2.0f)
            {
                yield return null;
                safetyWait += Time.deltaTime;
            }

            // ------------------------------------------------------------------
            // 5. 페이지 페이드 아웃 (1초)
            // ------------------------------------------------------------------
            yield return StartCoroutine(FadePageAlpha(1f, 0f, 1.0f));
            
            CompleteStep();
        }

        // 파일 경로 가져오기
        private string GetVideoPath()
        {
            string dataPath = Application.dataPath;
            DirectoryInfo parentDir = Directory.GetParent(dataPath);
            string rootPath = (parentDir != null) ? parentDir.FullName : dataPath;
            return Path.Combine(rootPath, videoFolderName, videoFileName);
        }

        // 텍스트 페이드
        private IEnumerator FadeText(Text target, float start, float end, float duration)
        {
            if (!target) yield break;
            float t = 0f;
            SetTextAlpha(target, start);
            while (t < duration)
            {
                t += Time.deltaTime;
                SetTextAlpha(target, Mathf.Lerp(start, end, t / duration));
                yield return null;
            }
            SetTextAlpha(target, end);
        }

        // 이미지 페이드
        private IEnumerator FadeRawImage(RawImage target, float start, float end, float duration)
        {
            if (!target) yield break;
            float t = 0f;
            SetImageAlpha(target, start);
            while (t < duration)
            {
                t += Time.deltaTime;
                SetImageAlpha(target, Mathf.Lerp(start, end, t / duration));
                yield return null;
            }
            SetImageAlpha(target, end);
        }

        // 페이지 전체(CanvasGroup) 페이드
        private IEnumerator FadePageAlpha(float start, float end, float duration)
        {
            float t = 0f;
            SetAlpha(start);
            while (t < duration)
            {
                t += Time.deltaTime;
                SetAlpha(Mathf.Lerp(start, end, t / duration));
                yield return null;
            }
            SetAlpha(end);
        }

        private void SetTextAlpha(Text t, float a)
        {
            if (t)
            {
                Color c = t.color;
                c.a = a;
                t.color = c;
            }
        }

        private void SetImageAlpha(RawImage i, float a)
        {
            if (i)
            {
                Color c = i.color;
                c.a = a;
                i.color = c;
            }
        }
    }
}