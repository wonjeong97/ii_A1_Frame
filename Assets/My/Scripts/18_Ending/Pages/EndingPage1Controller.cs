using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts.Core;
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
    public class EndingPage1Data
    {
        public TextSetting firstText; 
        public TextSetting secondText; 
    }

    /// <summary> 
    /// 엔딩 씬의 진입 안내를 담당하는 컨트롤러입니다.
    /// View와 Data의 책임이 완벽히 분리되었으며, 씬 파괴 시의 비동기 예외가 안전하게 억제되었습니다.
    /// </summary>
    public class EndingPage1Controller : GamePage<EndingPage1Data>
    {
        [Header("UI References")]
        [SerializeField] private Text descriptionText; 

        private EndingPage1Data _data;
        private CancellationTokenSource _sequenceCts;

        // --- 의존성 주입 (DI) 변수 ---
        private SoundManager _soundManager;
        private ILogger<EndingPage1Controller> _logger;

        [Inject]
        public void Construct(SoundManager soundManager, ILogger<EndingPage1Controller> logger)
        {
            _soundManager = soundManager;
            _logger = logger;
        }

        protected override void SetupData(EndingPage1Data data)
        {
            // [최적화 완료] SetupData는 순수하게 데이터 캐싱 역할만 수행합니다. (중복 UI 세팅 제거)
            _data = data;
            
            if (!descriptionText)
            {
                _logger?.ZLogWarning($"[EndingPage1Controller] descriptionText가 할당되지 않았습니다.");
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = new CancellationTokenSource();
            
            SequenceAsync(_sequenceCts.Token).Forget();
        }

        public override void OnExit()
        {
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = null;
            
            base.OnExit();
        }

        private async UniTaskVoid SequenceAsync(CancellationToken token)
        {
            try
            {
                // 1. 첫 번째 텍스트 세팅
                if (descriptionText && _data?.firstText != null && _uiManager)
                {
                    _uiManager.SetText(descriptionText.gameObject, _data.firstText);
                    descriptionText.SetAlpha(1f); // 씬 전체가 페이드인 되므로 여기선 1로 두는 것이 맞습니다.
                }
                
                await UniTask.Delay(4000, ignoreTimeScale: true, cancellationToken: token);
                
                // 2. 첫 번째 텍스트 페이드 아웃
                if (descriptionText)
                {
                    await descriptionText.FadeAsync(1f, 0f, 1f, token);
                }

                // 3. 두 번째 텍스트 세팅
                if (descriptionText && _data?.secondText != null && _uiManager)
                {
                    _uiManager.SetText(descriptionText.gameObject, _data.secondText);
                    
                    descriptionText.SetAlpha(0f);
                    
                    if (_soundManager)
                    {
                        _soundManager.PlaySFX("공통_13"); 
                    }
                }

                // 4. 두 번째 텍스트 페이드 인
                if (descriptionText)
                {
                    await descriptionText.FadeAsync(0f, 1f, 1f, token);
                }
                
                await UniTask.Delay(2000, ignoreTimeScale: true, cancellationToken: token);
                
                CompleteStep();
            }
            catch (OperationCanceledException)
            {
                // 토큰 취소 시 자연스럽게 루틴 종료 (에디터 콘솔 에러 방어)
            }
        }

        protected override void OnDestroy()
        {
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = null;

            base.OnDestroy();
        }
    }
}