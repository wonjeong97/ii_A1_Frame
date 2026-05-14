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

        /// <summary> JSON에서 로드한 텍스트 데이터 주입 </summary>
        protected override void SetupData(TutorialPage2Data data)
        {
            _data = data;
            if (descriptionText)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            }
        }

        public override object ExtractCurrentData()
        {
            return new TutorialPage2Data
            {
                descriptionText = TutorialPageUtils.BuildTextSetting(descriptionText, _data?.descriptionText),
            };
        }

        /// <summary> 페이지 진입 시 UI 표시 및 BGM 전환, 타이머 시작 </summary>
        public override void OnEnter()
        {
            base.OnEnter(); 
    
            if (descriptionText) UIFadeUtility.SetAlpha(descriptionText, 1f);

            if (SoundManager.Instance)
            {
                SoundManager.Instance.StopBGM();
                SoundManager.Instance.PlayBGM("MainBGM");
                SoundManager.Instance.PlaySFX("공통_6");
            }
    
            WaitAndNextAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }
        
        private async UniTaskVoid WaitAndNextAsync(CancellationToken token)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(3.0), cancellationToken: token);
            CompleteStep();
        }
    }
}