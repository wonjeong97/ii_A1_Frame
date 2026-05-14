using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using My.Scripts.Utils;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._18_Ending.Pages
{
    /// <summary> 엔딩 1페이지용 데이터 구조체 </summary>
    [Serializable]
    public class EndingPage1Data
    {
        public TextSetting firstText; // "모든 질문을 찾았습니다."
        public TextSetting secondText; // "STEP.2\n기록된 우리 모습 확인하기."
    }

    /// <summary> 
    /// 엔딩 씬의 진입 안내를 담당하는 컨트롤러입니다.
    /// 두 개의 안내 문구를 순차적인 페이드 효과(Cross-fade)로 연출하여 몰입감을 높입니다.
    /// </summary>
    public class EndingPage1Controller : GamePage<EndingPage1Data>
    {
        [Header("UI References")]
        [SerializeField] private Text descriptionText; 

        private EndingPage1Data _data;

        /// <summary> JSON 설정 로드 및 첫 번째 문구 즉시 활성화 </summary>
        protected override void SetupData(EndingPage1Data data)
        {
            _data = data;
            
            if (descriptionText && _data.firstText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, _data.firstText);
                UIFadeUtility.SetAlpha(descriptionText, 1f);
            }
            else if (!descriptionText)
            {
                Debug.LogWarning("EndingPage1Controller: descriptionText가 할당되지 않았습니다.");
            }
        }

        /// <summary> 페이지 활성화 시 연출 시퀀스 가동 </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            SequenceAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// 텍스트의 가독성과 연출 흐름을 위한 순차 제어.
        /// IEnumerator를 제거하고 UniTask를 도입하여 코루틴 할당 오버헤드를 제거함.
        /// </summary>
        private async UniTaskVoid SequenceAsync(CancellationToken token)
        {
            if (descriptionText && _data?.firstText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, _data.firstText);
                UIFadeUtility.SetAlpha(descriptionText, 1f);
            }
            
            // yield return WaitForSeconds 대신 UniTask.Delay 사용
            await UniTask.Delay(TimeSpan.FromSeconds(2.0), cancellationToken: token);
            
            // await를 사용하여 비동기 페이드 완료까지 대기
            await UIFadeUtility.FadeGraphicAsync(descriptionText, 1f, 0f, 1f, token);

            if (descriptionText && _data?.secondText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, _data.secondText);
                if (SoundManager.Instance)
                {
                    SoundManager.Instance.PlaySFX("공통_13"); 
                }
            }

            await UIFadeUtility.FadeGraphicAsync(descriptionText, 0f, 1f, 1f, token);
            
            await UniTask.Delay(TimeSpan.FromSeconds(2.0), cancellationToken: token);
            CompleteStep();
        }
    }
}