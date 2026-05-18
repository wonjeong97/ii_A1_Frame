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
    [Serializable]
    public class TutorialPage7Data
    {
        public TextSetting descriptionText1;
        public TextSetting descriptionText2;
    }

    /// <summary> 
    /// 튜토리얼 7페이지 컨트롤러.
    /// 유저 조작 없이 두 개의 안내 텍스트를 보여준 뒤, 지정된 시간 후 자동으로 다음 페이지로 전환합니다.
    /// OnEnter의 내부 복잡도를 서브 메서드로 위임하여 최상의 가독성을 확보한 마스터 버전입니다.
    /// </summary>
    public class TutorialPage7Controller : GamePage<TutorialPage7Data>
    {
        [Header("Page 7 UI")]
        [SerializeField] private Text text1;
        [SerializeField] private Text text2;

        private TutorialPage7Data _data;
        private CancellationTokenSource _sequenceCts;

        // --- 의존성 주입 (DI) 변수 ---
        private ILogger<TutorialPage7Controller> _logger;

        [Inject]
        public void Construct(ILogger<TutorialPage7Controller> logger)
        {
            _logger = logger;
        }

        protected override void SetupData(TutorialPage7Data data)
        {
            _data = data;
        }

        public override object ExtractCurrentData()
        {
            return new TutorialPage7Data
            {
                descriptionText1 = TutorialPageUtils.BuildTextSetting(text1, _data?.descriptionText1),
                descriptionText2 = TutorialPageUtils.BuildTextSetting(text2, _data?.descriptionText2),
            };
        }
        
        public override void OnEnter()
        {
            base.OnEnter();

            ResetSequenceToken();    // 1. 비동기 토큰 안전 해제 및 갱신
            RenderUI();              // 2. 데이터 기반 UI 텍스트 출력 및 알파 가드 적용
            StartAutoTransition();   // 3. 자동 씬 전환 비동기 타이머 기동
        }

        public override void OnExit()
        {
            ClearSequenceToken();
            base.OnExit();
        }

        protected override void OnDestroy()
        {
            ClearSequenceToken();
            base.OnDestroy();
        }

        #region Private Sub-Pipelines (분할된 서브 로직)

        /// <summary> 기존 가동 중인 비동기 소스를 안전하게 제거하고 새 토큰을 발행함 </summary>
        private void ResetSequenceToken()
        {
            if (_sequenceCts != null)
            {
                _sequenceCts.Cancel();
                _sequenceCts.Dispose();
            }
            _sequenceCts = new CancellationTokenSource();
        }

        /// <summary> 자원 해제 전용 헬퍼 </summary>
        private void ClearSequenceToken()
        {
            if (_sequenceCts != null)
            {
                _sequenceCts.Cancel();
                _sequenceCts.Dispose();
                _sequenceCts = null;
            }
        }

        /// <summary> 데이터 검증 및 UGUI Canvas Dirty 오버헤드를 고려한 전용 렌더링 로직 </summary>
        private void RenderUI()
        {
            if (!_uiManager || _data == null)
            {
                if (text1) text1.SetAlpha(1f);
                if (text2) text2.SetAlpha(1f);
                return;
            }

            // 첫 번째 안내문 바인딩 및 중복 Dirty 방어
            if (text1 && _data.descriptionText1 != null)
            {
                _uiManager.SetText(text1.gameObject, _data.descriptionText1);
                if (Mathf.Abs(text1.color.a - 1f) > Mathf.Epsilon) text1.SetAlpha(1f);
            }

            // 두 번째 안내문 바인딩 및 중복 Dirty 방어
            if (text2 && _data.descriptionText2 != null)
            {
                _uiManager.SetText(text2.gameObject, _data.descriptionText2);
                if (Mathf.Abs(text2.color.a - 1f) > Mathf.Epsilon) text2.SetAlpha(1f);
            }
        }

        /// <summary> 자동 페이지 전환 시퀀스 가동 </summary>
        private void StartAutoTransition()
        {
            if (_sequenceCts != null)
            {
                WaitAndNextAsync(_sequenceCts.Token).Forget();
            }
        }

        #endregion

        #region Async Logic

        private async UniTaskVoid WaitAndNextAsync(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(3000, ignoreTimeScale: true, cancellationToken: token);
                
                // 씬 로드 전 메모리 링크 해제
                if (text1) text1.text = string.Empty;
                if (text2) text2.text = string.Empty;

                CompleteStep();
            }
            catch (OperationCanceledException)
            {
                // 정상 취소 무음 처리
            }
        }

        #endregion
    }
}