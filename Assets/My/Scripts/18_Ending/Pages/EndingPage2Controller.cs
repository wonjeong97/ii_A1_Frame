using System;
using System.Collections;
using System.IO;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._18_Ending.Pages
{
    [Serializable]
    public class EndingPage2Data
    {
        public TextSetting descriptionText; 
    }

    /// <summary> 
    /// 엔딩 2페이지 컨트롤러
    /// 플레이 중 녹화된 '리얼타임' 영상을 재생하며, 영상의 실제 길이와 무관하게 15초 카운트다운 연출을 동기화.
    /// </summary>
    public class EndingPage2Controller : GamePage<EndingPage2Data>
    {
        [Header("UI References")]
        [SerializeField] private RawImage videoDisplay; 
        [SerializeField] private VideoPlayer videoPlayer; 
        [SerializeField] private Text descriptionText; 

        private const string FixedVideoFileName = "Test_Realtime.mp4"; 
        
        // 영상 길이에 맞춰 타이머를 30초 -> 15초로 변경
        private const float FixedDuration = 15f; 
        
        protected override void SetupData(EndingPage2Data data)
        {
            if (descriptionText && data.descriptionText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
                descriptionText.text = "00:00";
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            SetImageAlpha(videoDisplay, 0f);
            
            if (descriptionText) 
            {
                Color c = descriptionText.color;
                c.a = 0f;
                descriptionText.color = c;
            }
            
            StartCoroutine(PresentationRoutine());
        }

        public override void OnExit()
        {
            base.OnExit();
            StopAllCoroutines();
        }
        
        /// <summary>
        /// 영상 재생 및 타이머 연출의 전체 시퀀스를 제어합니다.
        /// (준비 -> 재생/페이드인 -> 15초 타이머 -> 정지/페이드아웃 -> 완료)
        /// </summary>
        private IEnumerator PresentationRoutine()
        {   
            if (!videoPlayer || !videoDisplay)
            {
                CompleteStep();
                yield break;
            }
            
            string filePath = GetVideoPath();

            if (!File.Exists(filePath))
            {
                Debug.LogError($"[EndingPage2] 영상 파일을 찾을 수 없습니다: {filePath}");
                CompleteStep();
                yield break;
            }

            Debug.Log($"[EndingPage2] 재생 시작: {filePath}");

            // 2. 재생 준비
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = new Uri(filePath).AbsoluteUri; 
            videoPlayer.Prepare();

            // 준비 완료 대기 (최대 10초 타임아웃)
            float prepareWait = 0f;
            while (!videoPlayer.isPrepared && prepareWait < 10f)
            {
                yield return null;
                prepareWait += Time.deltaTime;
            }

            if (!videoPlayer.isPrepared)
            {
                CompleteStep();
                yield break;
            }

            // 텍스처 생성 대기
            float textureWait = 0f;
            while (!videoPlayer.texture && textureWait < 5f)
            {
                yield return null;
                textureWait += Time.deltaTime;
            }

            if (!videoPlayer.texture)
            {
                Debug.LogError("[EndingPage2] Video prepared but texture is null.");
                CompleteStep();
                yield break;
            }

            // 3. 재생 시작 및 화면/텍스트 페이드 인
            videoDisplay.texture = videoPlayer.texture;
            videoPlayer.Play();
            
            StartCoroutine(FadeRawImage(videoDisplay, 0f, 1f, 1f));
            if (descriptionText) StartCoroutine(FadeText(descriptionText, 0f, 1f, 1f));

            // 4. 타이머 진행 (15초 고정)
            float currentTimer = 0f;
            while (currentTimer < FixedDuration)
            {
                currentTimer += Time.deltaTime;
                float displayTime = Mathf.Min(currentTimer, FixedDuration);

                if (descriptionText)
                {
                    int minutes = Mathf.FloorToInt(displayTime / 60f);
                    int seconds = Mathf.FloorToInt(displayTime % 60f);
                    descriptionText.text = $"{minutes:00}:{seconds:00}";
                }
                yield return null;
            }
            
            // 타이머 종료 확정 표시
            if (descriptionText) 
            {
                int finalMinutes = Mathf.FloorToInt(FixedDuration / 60f);
                int finalSeconds = Mathf.FloorToInt(FixedDuration % 60f);
                descriptionText.text = $"{finalMinutes:00}:{finalSeconds:00}";
            }
            
            if (videoPlayer.isPlaying) videoPlayer.Pause();

            yield return CoroutineData.GetWaitForSeconds(1.5f);

            StartCoroutine(FadeRawImage(videoDisplay, 1f, 0f, 1f));
            if (descriptionText) StartCoroutine(FadeText(descriptionText, 1f, 0f, 1f));
            
            CompleteStep();
        }

        private string GetVideoPath()
        {   
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
            return Path.Combine(root, "Timelapse", "Realtime_Video", dateFolder, FixedVideoFileName);
        }

        private IEnumerator FadeRawImage(RawImage t, float s, float e, float d)
        {
            if (!t) yield break;
            float time = 0f;
            SetImageAlpha(t, s);
            while(time < d) 
            { 
                time += Time.deltaTime; 
                SetImageAlpha(t, Mathf.Lerp(s, e, time/d)); 
                yield return null; 
            }
            SetImageAlpha(t, e);
        }
        
        private IEnumerator FadeText(Text t, float s, float e, float d)
        {
            if (!t) yield break;
            float time = 0f;
            Color c = t.color;
            c.a = s;
            t.color = c;
            
            while(time < d) 
            { 
                time += Time.deltaTime; 
                c.a = Mathf.Lerp(s, e, time/d);
                t.color = c;
                yield return null; 
            }
            c.a = e;
            t.color = c;
        }

        private void SetImageAlpha(RawImage i, float a) 
        { 
            if(i) 
            { 
                Color c = i.color; 
                c.a = a; 
                i.color = c; 
            } 
        }
    }
}