using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using My.Scripts.Core;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._18_Ending.Pages
{
    /// <summary> 엔딩 3페이지용 데이터 구조체 </summary>
    [Serializable]
    public class EndingPage3Data
    {
        public TextSetting descriptionText; 
    }

    /// <summary> 
    /// 엔딩 3페이지 컨트롤러.
    /// 녹화된 '리얼타임' 영상을 재생하며 15초 카운트다운을 시각적으로 동기화합니다.
    /// 영상이 15초보다 짧을 경우 반복 재생하여 연출 시간을 유지합니다.
    /// </summary>
    public class EndingPage3Controller : GamePage<EndingPage3Data>
    {
        [Header("UI References")]
        [SerializeField] private RawImage videoDisplay; 
        [SerializeField] private VideoPlayer videoPlayer; 
        [SerializeField] private Text descriptionText; 
        
        private const float FixedDuration = 15f; 
        
        /// <summary> 텍스트 설정 로드 및 타이머 초기값 할당 </summary>
        protected override void SetupData(EndingPage3Data data)
        {
            if (descriptionText && data.descriptionText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
                descriptionText.text = "00:00";
            }
        }

        /// <summary> 페이지 진입 시 UI 초기화 및 연출 시퀀스 시작 </summary>
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

        /// <summary> 퇴장 시 모든 연출 코루틴 중단 </summary>
        public override void OnExit()
        {
            base.OnExit();
            StopAllCoroutines();
        }
        
        /// <summary>
        /// 영상 준비, 재생, 타이머 진행 및 페이드 효과를 포함한 전체 연출 흐름을 제어합니다.
        /// 영상 길이가 짧을 경우를 대비해 반복 재생(Loop) 모드를 활성화합니다.
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
                Debug.LogError($"[EndingPage3] 영상 파일을 찾을 수 없습니다: {filePath}");
                CompleteStep();
                yield break;
            }

            // 부드러운 재생 시작을 위해 비디오 준비 과정 수행
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = new Uri(filePath).AbsoluteUri; 
            videoPlayer.Prepare();

            // 파일 손상 시 무한 대기를 방지하기 위해 10초 타임아웃 설정
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

            // 첫 프레임 렌더링 지연을 방지하기 위해 텍스처 생성 대기
            float textureWait = 0f;
            while (!videoPlayer.texture && textureWait < 5f)
            {
                yield return null;
                textureWait += Time.deltaTime;
            }

            if (!videoPlayer.texture)
            {
                Debug.LogError("[EndingPage3] Video prepared but texture is null.");
                CompleteStep();
                yield break;
            }

            // 영상 길이가 15초보다 짧을 경우 끊기지 않도록 반복 재생 활성화
            videoPlayer.isLooping = true;
            videoDisplay.texture = videoPlayer.texture;
            videoPlayer.Play();
            
            StartCoroutine(FadeRawImage(videoDisplay, 0f, 1f, 1f));
            if (descriptionText) StartCoroutine(FadeText(descriptionText, 0f, 1f, 1f));

            // 영상 실제 길이와 상관없이 기획된 15초 연출 시간 준수
            float currentTimer = 0f;
            while (currentTimer < FixedDuration)
            {
                currentTimer += Time.deltaTime;
                float displayTime = Mathf.Min(currentTimer, FixedDuration);

                if (descriptionText)
                {
                    int seconds = Mathf.FloorToInt(displayTime);
                    int milliseconds = Mathf.FloorToInt((displayTime * 100) % 100);
                    descriptionText.text = $"{seconds:00}:{milliseconds:00}";
                }
                yield return null;
            }
            
            // 타이머 종료 확정 표시
            if (descriptionText) 
            {
                int finalSeconds = Mathf.FloorToInt(FixedDuration);
                int finalMilliseconds = Mathf.FloorToInt((FixedDuration * 100) % 100);
                descriptionText.text = $"{finalSeconds:00}:{finalMilliseconds:00}";
            }
            
            // 15초 시점에 영상의 반복 상태와 관계없이 강제 정지
            if (videoPlayer.isPlaying) videoPlayer.Pause();

            yield return CoroutineData.GetWaitForSeconds(1.5f);

            StartCoroutine(FadeRawImage(videoDisplay, 1f, 0f, 1f));
            if (descriptionText) StartCoroutine(FadeText(descriptionText, 1f, 0f, 1f));
            
            CompleteStep();
        }

        /// <summary> 현재 유저 ID와 오늘 날짜에 기반한 영상 파일 경로를 반환합니다. </summary>
        private string GetVideoPath()
        {   
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
            
            string userIdStr = "0";
            if (GameManager.Instance && SessionManager.Instance)
            {
                userIdStr = SessionManager.Instance.CurrentUserId.ToString();
            }
            
            string dynamicVideoFileName = $"{userIdStr}_Realtime.mp4";
            
            return Path.Combine(root, "Timelapse", "Realtime_Video", dateFolder, dynamicVideoFileName);
        }

        /// <summary> RawImage의 알파값을 선형 보간하여 시각적 전환 수행 </summary>
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
        
        /// <summary> Text 컴포넌트의 알파값을 선형 보간하여 등장/퇴장 연출 </summary>
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

        /// <summary> 지정된 이미지의 투명도 즉시 변경 </summary>
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