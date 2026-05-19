using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;
using ZLogger;

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary> 튜토리얼 2페이지용 데이터 구조체 </summary>
    [Serializable]
    public class TutorialPage2Data
    {
        public TextSetting descriptionText;
    }

    /// <summary> 
    /// 튜토리얼 2페이지 컨트롤러.
    /// 본격적인 시작 전 메인 BGM으로 교체하고 지정된 시간 동안 대기한 후 자동 전환합니다.
    /// </summary>
    public class TutorialPage2Controller : GamePage<TutorialPage2Data>
    {
        [Header("Page 2 UI")]
        [SerializeField] private Text descriptionText;

        private TutorialPage2Data _data;
        private CancellationTokenSource _sequenceCts;

        // --- 의존성 주입 (DI) 변수 ---
        private ILogger<TutorialPage2Controller> _logger;
        private SoundManager _soundManager;

        [Inject]
        public void Construct(SoundManager soundManager, ILogger<TutorialPage2Controller> logger)
        {
            _soundManager = soundManager;
            _logger = logger;
        }

        protected override void SetupData(TutorialPage2Data data)
        {
            _data = data;
        }

        public override object ExtractCurrentData()
        {
            return new TutorialPage2Data
            {
                descriptionText = TutorialPageUtils.BuildTextSetting(descriptionText, _data?.descriptionText),
            };
        }

        public override void OnEnter()
        {
            base.OnEnter(); 
    
            if (descriptionText)
            {
                if (_data?.descriptionText != null && _uiManager)
                {
                    _uiManager.SetText(descriptionText.gameObject, _data.descriptionText);

                    if (Mathf.Abs(descriptionText.color.a - 1f) > Mathf.Epsilon)
                    {
                        descriptionText.SetAlpha(1f);
                    }
                }
                else
                {
                    descriptionText.SetAlpha(1f);
                }
            }

            if (_soundManager)
            {
                _soundManager.StopBGM();
                _soundManager.PlayBGM("MainBGM");
                _soundManager.PlaySFX("공통_6");
            }
    
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = new CancellationTokenSource();

            WaitAndNextAsync(_sequenceCts.Token).Forget();
        }
        
        public override void OnExit()
        {
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = null;

            base.OnExit();
        }

        private async UniTaskVoid WaitAndNextAsync(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(4000, ignoreTimeScale: true, cancellationToken: token);
                CompleteStep();
            }
            catch (OperationCanceledException)
            {
                // 씬 파괴 및 페이지 전환 시 강제 취소 예외 무음 처리
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