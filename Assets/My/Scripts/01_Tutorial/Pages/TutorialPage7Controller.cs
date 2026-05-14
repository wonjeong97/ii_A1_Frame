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
    /// <summary> 튜토리얼 7페이지용 데이터 구조체 </summary>
    [Serializable]
    public class TutorialPage7Data
    {
        public TextSetting descriptionText1;
        public TextSetting descriptionText2;
    }

    /// <summary> 
    /// 튜토리얼 7페이지 컨트롤러.
    /// 유저 조작 없이 두 개의 안내 텍스트를 보여준 뒤, 지정된 시간 후 자동으로 다음 페이지로 전환합니다.
    /// </summary>
    public class TutorialPage7Controller : GamePage<TutorialPage7Data>
    {
        [Header("Page 7 UI")]
        [SerializeField] private Text text1;
        [SerializeField] private Text text2;

        private Coroutine _endSequenceRoutine;
        private TutorialPage7Data _data;

        /// <summary> JSON에서 로드한 두 개의 안내 텍스트 데이터 주입 </summary>
        protected override void SetupData(TutorialPage7Data data)
        {
            _data = data;
            if (text1) UIManager.Instance.SetText(text1.gameObject, data.descriptionText1);
            if (text2) UIManager.Instance.SetText(text2.gameObject, data.descriptionText2);
        }

        public override object ExtractCurrentData()
        {
            return new TutorialPage7Data
            {
                descriptionText1 = TutorialPageUtils.BuildTextSetting(text1, _data?.descriptionText1),
                descriptionText2 = TutorialPageUtils.BuildTextSetting(text2, _data?.descriptionText2),
            };
        }

        /// <summary>
        /// 페이지 진입 시 안내 문구들을 활성화하고 종료 시퀀스를 시작함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            if (text1)
            {
                UIFadeUtility.SetAlpha(text1, 1f);
            }

            if (text2)
            {
                UIFadeUtility.SetAlpha(text2, 1f);
            }

            WaitAndNextAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }
        
        /// <summary>
        /// 유저가 안내를 충분히 읽을 수 있도록 대기 후 튜토리얼을 종료함.
        /// </summary>
        /// <param name="token">비동기 작업 취소 토큰</param>
        private async UniTaskVoid WaitAndNextAsync(CancellationToken token)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(3.0), cancellationToken: token);
            CompleteStep();
        }
    }
}