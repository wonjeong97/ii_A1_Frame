using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts.Core;
using My.Scripts.Timelapse;
using My.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;
using ZLogger;

namespace My.Scripts._18_Ending.Pages
{
    [Serializable]
    public class EndingPage2Data
    {
        public TextSetting descriptionText;
    }

    /// <summary> 
    /// 엔딩 씬의 영상 변환 대기 페이지.
    /// 중복 대기 논리 결함이 수정되고, 람다 가비지가 완벽히 소멸된 진(眞) 무결점 버전입니다.
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

        // --- 의존성 주입 (DI) 변수 ---
        private TimeLapseRecorder _timeLapseRecorder;
        private SoundManager _soundManager;
        private ILogger<EndingPage2Controller> _logger;

        [Inject]
        public void Construct(TimeLapseRecorder timeLapseRecorder, SoundManager soundManager,
            ILogger<EndingPage2Controller> logger)
        {
            _timeLapseRecorder = timeLapseRecorder;
            _soundManager = soundManager;
            _logger = logger;
        }

        protected override void SetupData(EndingPage2Data data)
        {
            if (descriptionText && data?.descriptionText != null && _uiManager)
            {
                _uiManager.SetText(descriptionText.gameObject, data.descriptionText);
                descriptionText.SetAlpha(0f);
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isTimelapseTriggered = false;

            if (loadingFillImage) loadingFillImage.fillAmount = 0f;

            _pageCts?.Cancel();
            _pageCts?.Dispose();
            _pageCts = new CancellationTokenSource();

            ProcessSequenceAsync(_pageCts.Token).Forget();
        }

        public override void OnExit()
        {
            _pageCts?.Cancel();
            _pageCts?.Dispose();
            _pageCts = null;

            base.OnExit();

            EnsureTimelapseTriggered();

            if (_soundManager) _soundManager.StopSFX();
        }

        private async UniTaskVoid ProcessSequenceAsync(CancellationToken token)
        {
            try
            {
                if (descriptionText) descriptionText.FadeAsync(0f, 1f, 1.0f, token).Forget();

                LoadingSoundLoopAsync(token).Forget();

                if (!_timeLapseRecorder)
                {
                    await HandleRecorderMissingAsync(token);
                    return;
                }

                await UniTask.Delay(500, ignoreTimeScale: true, cancellationToken: token);

                await HandleRealtimeConversionAsync(token);
                await HandleTimelapseConversionAsync(token);

                if (loadingFillImage) loadingFillImage.fillAmount = 1f;
                await UniTask.Delay(1500, ignoreTimeScale: true, cancellationToken: token);

                CompleteStep();
            }
            catch (OperationCanceledException)
            {
                // 취소 시 안전하게 루틴 종료
            }
        }

        private async UniTask HandleRealtimeConversionAsync(CancellationToken token)
        {
            if (!_timeLapseRecorder.IsRealtimeProcessing &&
                string.IsNullOrEmpty(_timeLapseRecorder.LastRealtimeVideoPath))
            {
                _timeLapseRecorder.ConvertToRealtimeVideo();
            }

            float startTime = Time.unscaledTime;
            
            while (_timeLapseRecorder.IsRealtimeProcessing)
            {
                if (Time.unscaledTime - startTime > conversionTimeout)
                {
                    _logger?.ZLogWarning($"[EndingPage2Controller] 리얼타임 변환 타임아웃({0}s) - 다음 단계 진행", conversionTimeout);
                    break;
                }

                if (loadingFillImage)
                {
                    loadingFillImage.fillAmount = Mathf.Lerp(
                        loadingFillImage.fillAmount,
                        _timeLapseRecorder.RealtimeProgress,
                        Time.unscaledDeltaTime * 5f
                    );
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        private async UniTask HandleTimelapseConversionAsync(CancellationToken token)
        {
            if (!_timeLapseRecorder) return;

            if (!_timeLapseRecorder.IsTimelapseProcessing)
            {
                _isTimelapseTriggered = true;
                _timeLapseRecorder.ConvertToVideo();
            }

            while (_timeLapseRecorder.IsTimelapseProcessing)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        private async UniTaskVoid LoadingSoundLoopAsync(CancellationToken token)
        {
            try
            {
                int intervalMs = Mathf.RoundToInt(loadingSoundInterval * 1000);

                while (!token.IsCancellationRequested)
                {
                    if (_soundManager) _soundManager.PlaySFX("키오스크_3");
                    await UniTask.Delay(intervalMs, ignoreTimeScale: true, cancellationToken: token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTask HandleRecorderMissingAsync(CancellationToken token)
        {
            if (loadingFillImage) loadingFillImage.fillAmount = 1f;
            await UniTask.Delay(2000, ignoreTimeScale: true, cancellationToken: token);
            CompleteStep();
        }

        private void EnsureTimelapseTriggered()
        {
            if (!_isTimelapseTriggered && _timeLapseRecorder)
            {
                if (!_timeLapseRecorder.IsRealtimeProcessing && !_timeLapseRecorder.IsTimelapseProcessing)
                {
                    _timeLapseRecorder.ConvertToVideo();
                }
            }
        }

        protected override void OnDestroy()
        {
            _pageCts?.Cancel();
            _pageCts?.Dispose();
            _pageCts = null;

            base.OnDestroy();
        }
    }
}