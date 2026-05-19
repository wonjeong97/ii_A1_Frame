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

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary> 튜토리얼 5페이지용 데이터 구조체 </summary>
    [Serializable]
    public class TutorialPage5Data
    {
        public TextSetting descriptionText;
    }

    /// <summary> 
    /// 튜토리얼 5페이지 컨트롤러.
    /// 유저의 추가 조작 없이 안내 텍스트를 보여준 뒤, 지정된 시간(4초) 후 자동으로 다음 페이지로 전환합니다.
    /// 극단적인 연속 진입 상황에서도 좀비 태스크가 발생하지 않도록 자원 관리가 완전 캡슐화되었습니다.
    /// </summary>
    public class TutorialPage5Controller : GamePage<TutorialPage5Data>
    {
        [Header("Page 5 UI")]
        [SerializeField] private Text descriptionText;

        private TutorialPage5Data _data;
        private CancellationTokenSource _sequenceCts;

        // --- 의존성 주입 (DI) 변수 ---
        private ILogger<TutorialPage5Controller> _logger;

        [Inject]
        public void Construct(ILogger<TutorialPage5Controller> logger)
        {
            _logger = logger;
        }

        protected override void SetupData(TutorialPage5Data data)
        {
            _data = data;
        }

        public override object ExtractCurrentData()
        {
            return new TutorialPage5Data
            {
                descriptionText = TutorialPageUtils.BuildTextSetting(descriptionText, _data?.descriptionText),
            };
        }

        public override void OnEnter()
        {
            base.OnEnter();

            if (_sequenceCts != null)
            {
                _sequenceCts.Cancel();
                _sequenceCts.Dispose();
                _sequenceCts = null;
            }
            _sequenceCts = new CancellationTokenSource();

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

            WaitAndNextAsync(_sequenceCts.Token).Forget();
        }

        public override void OnExit()
        {
            if (_sequenceCts != null)
            {
                _sequenceCts.Cancel();
                _sequenceCts.Dispose();
                _sequenceCts = null;
            }

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
                // 페이지 종료 및 씬 스킵 시 발생하는 정상적인 비동기 취소 예외 소화
            }
        }

        protected override void OnDestroy()
        {
            if (_sequenceCts != null)
            {
                _sequenceCts.Cancel();
                _sequenceCts.Dispose();
                _sequenceCts = null;
            }

            base.OnDestroy();
        }
    }
}