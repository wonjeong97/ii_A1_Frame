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
    /// <summary> 엔딩 2페이지용 데이터 구조체 </summary>
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
        [SerializeField] private Image loadingBgImage; 
        [SerializeField] private Image loadingFillImage; 

        [Header("Sound Settings")]
        [Tooltip("로딩 효과음 반복 재생 간격(초).")]
        [SerializeField] private float loadingSoundInterval = 7.0f;

        [Header("Timeout Settings")]
        [Tooltip("영상 변환 최대 대기 시간(초). 엔진 오류 시 무한 대기를 방지합니다.")]
        [SerializeField] private float conversionTimeout = 40.0f;
        
        private Coroutine _loadingSoundRoutine;

        /// <summary> JSON 설정 데이터 로드 및 텍스트 초기 투명도 설정 </summary>
        protected override void SetupData(EndingPage2Data data)
        {
            if (descriptionText && data.descriptionText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
                SetTextAlpha(0f); 
            }
            else Debug.LogWarning("[EndingPage2] UI 텍스트 컴포넌트나 데이터가 없습니다.");
        }

        /// <summary> 페이지 진입 시 로딩 UI 초기화 및 리얼타임 영상 합성 시작 </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            if (loadingFillImage) loadingFillImage.fillAmount = 0f;

            StartCoroutine(FadeText(0f, 1f, 1.0f));
            StartCoroutine(ProcessRealtimeVideoRoutine());
        }

        /// <summary> 
        /// 리얼타임 영상 변환을 실행하고 진행도를 UI에 반영합니다.
        /// 타임아웃 로직을 통해 엔진 오류로 인한 키오스크 멈춤 현상을 방지합니다.
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

            yield return CoroutineData.GetWaitForSeconds(0.5f); 

            if (!TimeLapseRecorder.Instance.IsRealtimeProcessing && string.IsNullOrEmpty(TimeLapseRecorder.Instance.LastRealtimeVideoPath))
            {
                Debug.Log("[EndingPage2] 리얼타임 영상 변환 시작");
                TimeLapseRecorder.Instance.ConvertToRealtimeVideo();
            }

            if (_loadingSoundRoutine != null) StopCoroutine(_loadingSoundRoutine);
            _loadingSoundRoutine = StartCoroutine(LoadingSoundLoopRoutine());

            // 변환 대기 및 타임아웃 체크 시작
            float startWaitTime = Time.time;
            while (TimeLapseRecorder.Instance.IsRealtimeProcessing)
            {
                // 설정된 시간을 초과하면 변환이 실패한 것으로 간주하고 강제 진행 (사용자 경험 보호)
                if (Time.time - startWaitTime > conversionTimeout)
                {
                    Debug.LogWarning($"[EndingPage2] 영상 변환 타임아웃({conversionTimeout}초) 발생. 엔진 오류 가능성으로 인해 강제 전환합니다.");
                    break;
                }

                if (loadingFillImage)
                {
                    loadingFillImage.fillAmount = Mathf.Lerp(loadingFillImage.fillAmount, TimeLapseRecorder.Instance.RealtimeProgress, Time.deltaTime * 5f);
                }
                yield return null;
            }

            if (_loadingSoundRoutine != null)
            {
                StopCoroutine(_loadingSoundRoutine);
                _loadingSoundRoutine = null;
            }

            if (loadingFillImage) loadingFillImage.fillAmount = 1f;

            yield return CoroutineData.GetWaitForSeconds(1.5f);

            CompleteStep();
        }
        
        /// <summary> 로딩 시간 동안 사운드 루프 재생 </summary>
        private IEnumerator LoadingSoundLoopRoutine()
        {
            while (true)
            {
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("키오스크_3");
                yield return CoroutineData.GetWaitForSeconds(loadingSoundInterval);
            }
        }

        /// <summary> 
        /// 페이지 퇴장 시 사운드 중단 및 타임랩스 백그라운드 합성 개시.
        /// 리소스 효율을 위해 리얼타임 영상 처리 후 즉시 연계 실행합니다.
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

        /// <summary> 텍스트 투명도 페이드 연출 </summary>
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

        /// <summary> UI 텍스트 컬러 알파값 갱신 </summary>
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