using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils; // CoroutineData 사용을 위해 추가

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage2Data
    {
        public TextSetting descriptionText;
    }

    /// <summary> 튜토리얼 2페이지 컨트롤러 </summary>
    public class TutorialPage2Controller : GamePage<TutorialPage2Data>
    {
        [Header("Page 2 UI")]
        [SerializeField] private Text descriptionText; // 설명 텍스트

        /// <summary> 데이터 설정 </summary>
        protected override void SetupData(TutorialPage2Data data)
        {
            if (descriptionText) 
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            }
        }

        /// <summary> 페이지 진입 (바로 보여주고 대기 시작) </summary>
        public override void OnEnter()
        {
            base.OnEnter(); // 기본 활성화

            // 텍스트가 바로 보이도록 확실하게 알파값 1 설정
            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = 1f;
                descriptionText.color = c;
            }

            // 3초 대기 코루틴 시작
            StartCoroutine(WaitAndNextRoutine());
        }

        /// <summary> 3초 대기 후 다음 단계로 </summary>
        private IEnumerator WaitAndNextRoutine()
        {
            // 3초 대기
            yield return CoroutineData.GetWaitForSeconds(3.0f);

            // 단계 완료
            CompleteStep();
        }
    }
}