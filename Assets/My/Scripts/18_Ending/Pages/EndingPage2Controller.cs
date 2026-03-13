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
    /// 엔딩 씬의 영상 변환 대기 페이지.
    /// 리얼타임 영상과 타임랩스 영상의 순차적 인코딩을 관리하며, 모든 작업이 완료될 때까지 대기합니다.
    /// </summary>
    public class EndingPage2Controller : GamePage<EndingPage2Data>
    {
        [Header("UI References")]
        [SerializeField] private Text descriptionText;
        [SerializeField] private Image loadingFillImage;

        [Header("Sound Settings")]
        [SerializeField] private float loadingSoundInterval = 7.0f;
        
        [Header("Timeout Settings")]
        [SerializeField] private float conversionTimeout = 40.0f;

        private Coroutine _loadingSoundRoutine;
        private bool _isTimelapseTriggered; // 중복 인코딩 방지용 플래그

        protected override void SetupData(EndingPage2Data data)
        {
            if (descriptionText)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
                SetTextAlpha(0f);
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isTimelapseTriggered = false; // 진입 시 초기화

            if (loadingFillImage) loadingFillImage.fillAmount = 0f;
            StartCoroutine(FadeText(0f, 1f, 1.0f));
            StartCoroutine(ProcessRealtimeVideoRoutine());
        }

        /// <summary> 
        /// 영상 변환 시퀀스 제어. 리얼타임 변환 완료 -> 타임랩스 변환 개시 -> 최종 완료 순으로 진행됩니다. 
        /// </summary>
        private IEnumerator ProcessRealtimeVideoRoutine()
        {
            if (!TimeLapseRecorder.Instance)
            {
                if (loadingFillImage) loadingFillImage.fillAmount = 1f;
                yield return CoroutineData.GetWaitForSeconds(2.0f);

                CompleteStep();
                yield break;
            }

            yield return CoroutineData.GetWaitForSeconds(0.5f);

            // 1. 리얼타임 영상 변환 시작
            if (!TimeLapseRecorder.Instance.IsRealtimeProcessing &&
                string.IsNullOrEmpty(TimeLapseRecorder.Instance.LastRealtimeVideoPath))
                TimeLapseRecorder.Instance.ConvertToRealtimeVideo();

            if (_loadingSoundRoutine != null) StopCoroutine(_loadingSoundRoutine);
            _loadingSoundRoutine = StartCoroutine(LoadingSoundLoopRoutine());

            float startWaitTime = Time.time;
            // UI 업데이트 루프
            while (TimeLapseRecorder.Instance.IsRealtimeProcessing)
            {
                if (Time.time - startWaitTime > conversionTimeout) break;

                if (loadingFillImage)
                    loadingFillImage.fillAmount = Mathf.Lerp(loadingFillImage.fillAmount,
                        TimeLapseRecorder.Instance.RealtimeProgress, Time.deltaTime * 5f);
                yield return null;
            }

            if (TimeLapseRecorder.Instance.IsRealtimeProcessing)
            {
                yield return new WaitUntil(() => !TimeLapseRecorder.Instance.IsRealtimeProcessing);
            }

            // 리얼타임 종료 후 즉시 타임랩스 변환 큐잉 및 대기
            if (TimeLapseRecorder.Instance && !TimeLapseRecorder.Instance.IsTimelapseProcessing)
            {
                _isTimelapseTriggered = true; // 정상적으로 트리거 되었음을 마킹
                TimeLapseRecorder.Instance.ConvertToVideo();

                yield return new WaitUntil(() => !TimeLapseRecorder.Instance.IsTimelapseProcessing);
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

        private IEnumerator LoadingSoundLoopRoutine()
        {
            while (true)
            {
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("키오스크_3");
                yield return CoroutineData.GetWaitForSeconds(loadingSoundInterval);
            }
        }

        public override void OnExit()
        {
            if (_loadingSoundRoutine != null)
            {
                StopCoroutine(_loadingSoundRoutine);
                _loadingSoundRoutine = null;
            }

            base.OnExit();

            // 루틴이 아닌 다른 경로로 종료될 때를 대비한 최종 안전 가드
            // 이미 정상적으로 타임랩스를 켰다면 중복 실행하지 않음
            if (!_isTimelapseTriggered && TimeLapseRecorder.Instance &&
                !TimeLapseRecorder.Instance.IsRealtimeProcessing && !TimeLapseRecorder.Instance.IsTimelapseProcessing)
            {
                TimeLapseRecorder.Instance.ConvertToVideo();
            }

            SoundManager.Instance?.StopSFX();
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