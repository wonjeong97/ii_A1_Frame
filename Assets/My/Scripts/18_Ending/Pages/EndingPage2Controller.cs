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
    /// 엔딩 2페이지 컨트롤러입니다.
    /// 플레이 중 녹화된 '리얼타임' 영상을 재생하며, 영상의 실제 길이와 무관하게 30초 카운트다운 연출을 동기화합니다.
    /// </summary>
    public class EndingPage2Controller : GamePage<EndingPage2Data>
    {
        [Header("UI References")]
        [SerializeField] private RawImage videoDisplay; 
        [SerializeField] private VideoPlayer videoPlayer; 
        [SerializeField] private Text descriptionText; 

        // [확인] 파일명 대소문자 주의 (Test_Realtime.mp4 권장)
        // # TODO: 파일명을 상수로 박아두기보다 GameManager나 DataManager에서 관리하는 것이 유연함.
        private const string FixedVideoFileName = "Test_Realtime.mp4"; 
        private const float FixedDuration = 30f; 
        
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
        /// (준비 -> 재생/페이드인 -> 30초 타이머 -> 정지/페이드아웃 -> 완료)
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

            // isPrepared 상태여도 Texture가 아직 생성되지 않았을 수 있으므로 확인 대기
            // videoPlayer.texture가 null이 아닐 때까지 잠시 대기 (최대 5초)
            float textureWait = 0f;
            while (videoPlayer.texture == null && textureWait < 5f)
            {
                yield return null;
                textureWait += Time.deltaTime;
            }

            // 텍스처 생성 실패 시 안전하게 종료
            if (videoPlayer.texture == null)
            {
                Debug.LogError("[EndingPage2] Video prepared but texture is null.");
                CompleteStep();
                yield break;
            }

            // 3. 재생 시작 및 화면/텍스트 페이드 인
            // 안전하게 확인된 텍스처를 할당하고 재생 시작
            videoDisplay.texture = videoPlayer.texture;
            videoPlayer.Play();
            
            StartCoroutine(FadeRawImage(videoDisplay, 0f, 1f, 0.1f));
            if (descriptionText) StartCoroutine(FadeText(descriptionText, 0f, 1f, 0.1f));

            // 4. 타이머 진행 (30초 고정)
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
            
            // 30초 종료 확정
            if (descriptionText) 
            {
                int finalMinutes = Mathf.FloorToInt(FixedDuration / 60f);
                int finalSeconds = Mathf.FloorToInt(FixedDuration % 60f);
                descriptionText.text = $"{finalMinutes:00}:{finalSeconds:00}";
            }
            
            if (videoPlayer.isPlaying) videoPlayer.Pause();

            yield return CoroutineData.GetWaitForSeconds(1.5f);

            StartCoroutine(FadeRawImage(videoDisplay, 1f, 0f, 0.1f));
            if (descriptionText) StartCoroutine(FadeText(descriptionText, 1f, 0f, 0.1f));
            
            yield return CoroutineData.GetWaitForSeconds(1.0f); 

            CompleteStep();
        }

        /// <summary>
        /// 날짜별 폴더 구조에서 리얼타임 영상 경로를 생성하여 반환합니다.
        /// </summary>
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