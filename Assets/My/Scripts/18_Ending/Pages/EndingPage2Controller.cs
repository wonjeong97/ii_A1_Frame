using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using My.Scripts.Timelapse;
using My.Scripts.Utils;
using Wonjeong.Data;
using Wonjeong.UI;

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

        private CancellationTokenSource _pageCts;
        private bool _isTimelapseTriggered;

        protected override void SetupData(EndingPage2Data data)
        {
            if (descriptionText && data.descriptionText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
                UIFadeUtility.SetAlpha(descriptionText, 0f);
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isTimelapseTriggered = false;

            if (loadingFillImage) loadingFillImage.fillAmount = 0f;

            // 기존 작업 취소 및 새로운 토큰 발행
            _pageCts?.Cancel();
            _pageCts?.Dispose();
            _pageCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            ProcessSequenceAsync(_pageCts.Token).Forget();
        }

        public override void OnExit()
        {
            _pageCts?.Cancel();
            _pageCts?.Dispose();
            _pageCts = null;

            base.OnExit();

            // 루틴 외부에서 종료될 경우를 대비한 최종 안전 가드
            EnsureTimelapseTriggered();
            SoundManager.Instance?.StopSFX();
        }

        /// <summary> 페이드, 사운드 루프, 영상 변환 감시를 통합 수행하는 시퀀스 </summary>
        private async UniTaskVoid ProcessSequenceAsync(CancellationToken token)
        {
            // 1. 초기 UI 연출
            UIFadeUtility.FadeGraphicAsync(descriptionText, 0f, 1f, 1.0f, token).Forget();
            LoadingSoundLoopAsync(token).Forget();

            if (!TimeLapseRecorder.Instance)
            {
                await HandleRecorderMissingAsync(token);
                return;
            }

            await UniTask.Delay(TimeSpan.FromSeconds(0.5), cancellationToken: token);

            // 2. 리얼타임 영상 변환 대기
            await HandleRealtimeConversionAsync(token);

            // 3. 타임랩스 영상 변환 대기
            await HandleTimelapseConversionAsync(token);

            // 4. 완료 연출
            if (loadingFillImage) loadingFillImage.fillAmount = 1f;
            await UniTask.Delay(TimeSpan.FromSeconds(1.5), cancellationToken: token);

            CompleteStep();
        }

        private async UniTask HandleRealtimeConversionAsync(CancellationToken token)
        {
            TimeLapseRecorder recorder = TimeLapseRecorder.Instance;
            
            if (!recorder.IsRealtimeProcessing && string.IsNullOrEmpty(recorder.LastRealtimeVideoPath))
            {
                recorder.ConvertToRealtimeVideo();
            }

            float startTime = Time.time;
            
            // 타임아웃을 고려한 UI 업데이트 루프 (GC Zero)
            while (recorder.IsRealtimeProcessing)
            {
                if (Time.time - startTime > conversionTimeout) break;

                if (loadingFillImage)
                {
                    loadingFillImage.fillAmount = Mathf.Lerp(
                        loadingFillImage.fillAmount, 
                        recorder.RealtimeProgress, 
                        Time.deltaTime * 5f
                    );
                }
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // 프로세스가 남았다면 완료될 때까지 비동기 대기 (Closure 방지 위해 recorder 전달)
            if (recorder.IsRealtimeProcessing)
            {
                await UniTask.WaitUntil(recorder,rec => !rec.IsRealtimeProcessing, PlayerLoopTiming.Update, token);
            }
        }

        private async UniTask HandleTimelapseConversionAsync(CancellationToken token)
        {
            TimeLapseRecorder recorder = TimeLapseRecorder.Instance;
            if (!recorder || recorder.IsTimelapseProcessing) return;

            _isTimelapseTriggered = true;
            recorder.ConvertToVideo();
            
            await UniTask.WaitUntil(recorder, rec => !rec.IsTimelapseProcessing, PlayerLoopTiming.Update, token);
        }

        private async UniTaskVoid LoadingSoundLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                SoundManager.Instance?.PlaySFX("키오스크_3");
                await UniTask.Delay(TimeSpan.FromSeconds(loadingSoundInterval), cancellationToken: token);
            }
        }

        private async UniTask HandleRecorderMissingAsync(CancellationToken token)
        {
            if (loadingFillImage) loadingFillImage.fillAmount = 1f;
            await UniTask.Delay(TimeSpan.FromSeconds(2.0), cancellationToken: token);
            CompleteStep();
        }

        private void EnsureTimelapseTriggered()
        {
            if (!_isTimelapseTriggered && TimeLapseRecorder.Instance)
            {
                var rec = TimeLapseRecorder.Instance;
                if (!rec.IsRealtimeProcessing && !rec.IsTimelapseProcessing)
                {
                    rec.ConvertToVideo();
                }
            }
        }

        // TODO: 기존의 FadeText, SetTextAlpha, ProcessRealtimeVideoRoutine 및 모든 IEnumerator 메서드 삭제됨
    }
}