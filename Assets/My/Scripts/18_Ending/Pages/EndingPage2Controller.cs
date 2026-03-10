using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using My.Scripts.Timelapse;
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
    /// 엔딩 2페이지 컨트롤러.
    /// 리얼타임 영상 합성을 수행하며 로딩바를 표시하고, 완료 후 타임랩스 합성을 백그라운드에서 시작합니다.
    /// </summary>
    public class EndingPage2Controller : GamePage<EndingPage2Data>
    {
        [Header("UI References")]
        [SerializeField] private Text descriptionText; 
        [SerializeField] private Image loadingBgImage; // 로딩바 배경
        [SerializeField] private Image loadingFillImage; // 로딩바 채우기(Fill) 이미지

        [Header("Sound Settings")]
        [Tooltip("로딩 효과음 반복 재생 간격(초). 사운드 파일의 실제 길이에 맞춰 자연스럽게 조절해 주세요.")]
        [SerializeField] private float loadingSoundInterval = 7.0f;
        
        private Coroutine _loadingSoundRoutine;

        /// <summary>
        /// 데이터 설정 및 초기 투명도 세팅을 수행합니다.
        /// </summary>
        protected override void SetupData(EndingPage2Data data)
        {
            if (descriptionText && data.descriptionText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
                SetTextAlpha(0f); 
            }
            else Debug.LogWarning("[EndingPage2] UI 텍스트 컴포넌트나 데이터가 없습니다.");
        }

        /// <summary>
        /// 페이지 진입 시 리얼타임 영상 합성을 시작합니다.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            if (loadingFillImage) loadingFillImage.fillAmount = 0f;

            StartCoroutine(FadeText(0f, 1f, 1.0f));
            StartCoroutine(ProcessRealtimeVideoRoutine());
        }

        /// <summary>
        /// 리얼타임 영상 변환을 지시하고, 완료될 때까지 진행도를 모니터링합니다.
        /// </summary>
        private IEnumerator ProcessRealtimeVideoRoutine()
        {
            if (!TimeLapseRecorder.Instance)
            {
                Debug.LogWarning("[EndingPage2] TimeLapseRecorder가 존재하지 않습니다. 합성을 건너뜁니다.");
                if (loadingFillImage) loadingFillImage.fillAmount = 1f;
                yield return CoroutineData.GetWaitForSeconds(2.0f);
                CompleteStep();
                yield break;
            }

            yield return CoroutineData.GetWaitForSeconds(0.5f); // 텍스트가 나타날 시간 부여

            // 이미 변환 중이 아니며, 영상이 없는 경우에만 시작
            if (!TimeLapseRecorder.Instance.IsRealtimeProcessing && string.IsNullOrEmpty(TimeLapseRecorder.Instance.LastRealtimeVideoPath))
            {
                Debug.Log("[EndingPage2] 리얼타임 영상 변환 시작");
                TimeLapseRecorder.Instance.ConvertToRealtimeVideo();
            }

            // 로딩 사운드 루프 시작
            if (_loadingSoundRoutine != null) StopCoroutine(_loadingSoundRoutine);
            _loadingSoundRoutine = StartCoroutine(LoadingSoundLoopRoutine());

            // 변환 중일 때 진행도를 로딩바에 반영
            while (TimeLapseRecorder.Instance.IsRealtimeProcessing)
            {
                if (loadingFillImage)
                {
                    // 부드러운 UI 갱신을 위해 Lerp 사용
                    loadingFillImage.fillAmount = Mathf.Lerp(loadingFillImage.fillAmount, TimeLapseRecorder.Instance.RealtimeProgress, Time.deltaTime * 5f);
                }
                yield return null;
            }

            // 로딩 사운드 루프 종료 (변환 완료 시)
            if (_loadingSoundRoutine != null)
            {
                StopCoroutine(_loadingSoundRoutine);
                _loadingSoundRoutine = null;
            }

            // 완료 보장
            if (loadingFillImage) loadingFillImage.fillAmount = 1f;

            // 로딩 완료 후 유저가 인지할 수 있는 여운 시간 부여
            yield return CoroutineData.GetWaitForSeconds(1.5f);

            CompleteStep();
        }
        
        /// <summary>
        /// 로딩이 진행되는 동안 일정 간격으로 사운드를 무한 재생합니다.
        /// </summary>
        private IEnumerator LoadingSoundLoopRoutine()
        {
            while (true)
            {
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("키오스크_3");
                yield return CoroutineData.GetWaitForSeconds(loadingSoundInterval);
            }
        }

        /// <summary>
        /// 페이지 퇴장 시, 다음 페이지들을 보는 동안 백그라운드에서 타임랩스 영상을 변환하도록 지시합니다.
        /// </summary>
        public override void OnExit()
        {
            if (_loadingSoundRoutine != null)
            {
                StopCoroutine(_loadingSoundRoutine);
                _loadingSoundRoutine = null;
            }
            
            base.OnExit();
            
            if (TimeLapseRecorder.Instance)
            {
                Debug.Log("[EndingPage2] OnExit: 타임랩스 영상 백그라운드 변환 시작");
                TimeLapseRecorder.Instance.ConvertToVideo();
            }
        }

        private IEnumerator FadeText(float start, float end, float duration)
        {
            if (!descriptionText) yield break;
            
            float t = 0f;
            SetTextAlpha(start);
            
            while (t < duration)
            {
                t += Time.deltaTime;
                SetTextAlpha(Mathf.Lerp(start, end, t / duration));
                yield return null;
            }
            SetTextAlpha(end);
        }

        private void SetTextAlpha(float alpha)
        {
            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = alpha;
                descriptionText.color = c;
            }
        }
    }
}