using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using My.Scripts.Core;
using My.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

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
    /// 유저의 추가 조작 없이 안내 텍스트를 보여준 뒤, 지정된 시간(5초) 후 자동으로 다음 페이지로 전환합니다.
    /// </summary>
    public class TutorialPage5Controller : GamePage<TutorialPage5Data>
    {
        [Header("Page 5 UI")]
        [SerializeField] private Text descriptionText;

        private Coroutine autoNextStepRoutine;
        private TutorialPage5Data _data;

        /// <summary> JSON에서 로드한 안내 텍스트 데이터 주입 </summary>
        protected override void SetupData(TutorialPage5Data data)
        {
            _data = data;
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
        }

        public override object ExtractCurrentData()
        {
            return new TutorialPage5Data
            {
                descriptionText = TutorialPageUtils.BuildTextSetting(descriptionText, _data?.descriptionText),
            };
        }

        /// <summary>
        /// 페이지 진입 시 안내 텍스트를 표시하고 자동 전환 타이머를 시작함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            if (descriptionText)
            {
                UIFadeUtility.SetAlpha(descriptionText, 1f);
            }

            // 별도의 사운드 재생 로직이 없었으므로 타이머만 가동
            WaitAndNextAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// 지정된 시간(4초) 동안 대기한 후 다음 단계로 진행함.
        /// </summary>
        /// <param name="token">객체 파괴 시 작업을 취소하기 위한 토큰</param>
        private async UniTaskVoid WaitAndNextAsync(CancellationToken token)
        {
            // 원본 로직의 4초 대기 적용
            await UniTask.Delay(TimeSpan.FromSeconds(4.0), cancellationToken: token);
            CompleteStep();
        }
    }
}