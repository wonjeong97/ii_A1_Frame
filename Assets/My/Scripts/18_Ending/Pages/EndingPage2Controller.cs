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
            if (descriptionText) descriptionText.text = "00:00";
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

            // 파일이 생성되지 않았을 경우 에러 처리 및 스킵
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[EndingPage2] 영상 파일을 찾을 수 없습니다: {filePath}");
                CompleteStep();
                yield break;
            }

            Debug.Log($"[EndingPage2] 재생 시작: {filePath}");

            // 2. 재생 준비
            // URL 소스로 설정하여 로컬 파일을 스트리밍 방식으로 재생
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

            // 3. 재생 시작 및 화면/텍스트 페이드 인
            videoDisplay.texture = videoPlayer.texture;
            videoPlayer.Play();
            
            StartCoroutine(FadeRawImage(videoDisplay, 0f, 1f, 1.0f));
            if (descriptionText) StartCoroutine(FadeText(descriptionText, 0f, 1f, 1.0f));

            // 4. 타이머 진행 (30초 고정)
            // 영상의 실제 길이가 프레임 드랍 등으로 인해 30초보다 짧거나 길더라도, 
            // UI는 정확히 30초(00:30)에 맞춰 진행되도록 별도의 타이머를 사용합니다.
            float currentTimer = 0f;
            while (currentTimer < FixedDuration)
            {
                currentTimer += Time.deltaTime;
                float displayTime = Mathf.Min(currentTimer, FixedDuration);

                if (descriptionText)
                {
                    // [수정] mm:ss 포맷으로 변경 (예: 00:00 ~ 00:30)
                    int minutes = Mathf.FloorToInt(displayTime / 60f);
                    int seconds = Mathf.FloorToInt(displayTime % 60f);
                    descriptionText.text = $"{minutes:00}:{seconds:00}";
                }
                yield return null;
            }
            
            // 30초 종료 확정 (mm:ss 포맷 유지)
            if (descriptionText) 
            {
                int finalMinutes = Mathf.FloorToInt(FixedDuration / 60f);
                int finalSeconds = Mathf.FloorToInt(FixedDuration % 60f);
                descriptionText.text = $"{finalMinutes:00}:{finalSeconds:00}";
            }
            
            // [중요] Stop()을 호출하면 렌더 텍스처가 검은색이나 투명으로 초기화될 수 있으므로,
            // 마지막 프레임을 화면에 남겨두기 위해 Pause()를 사용합니다.
            if (videoPlayer.isPlaying) videoPlayer.Pause();

            // 여운을 주기 위해 잠시 대기
            yield return CoroutineData.GetWaitForSeconds(1.5f);

            // 부드럽게 페이드 아웃 후 종료 (1초 동안 사라짐)
            // 다음 페이지 전환 시 화면이 뚝 끊기는 느낌을 방지하기 위함입니다.
            StartCoroutine(FadeRawImage(videoDisplay, 1f, 0f, 1.0f));
            if (descriptionText) StartCoroutine(FadeText(descriptionText, 1f, 0f, 1.0f));
            
            yield return CoroutineData.GetWaitForSeconds(1.0f); // 페이드 아웃 시간만큼 대기

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