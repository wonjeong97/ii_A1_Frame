using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage5Data
    {
        public TextSetting descriptionText; // 설명 텍스트 데이터
    }

    /// <summary> 튜토리얼 5페이지 컨트롤러 </summary>
    public class TutorialPage5Controller : GamePage<TutorialPage5Data>
    {
        [Header("Page 5 UI")]
        [SerializeField] private Text descriptionText; // 설명 텍스트
        
        private Coroutine autoNextStepRoutine;

        /// <summary> 데이터 설정 (텍스트 적용) </summary>
        protected override void SetupData(TutorialPage5Data data)
        {
            if (descriptionText) 
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
        }

        /// <summary> 페이지 진입 (자동 넘김 시작) </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            if (autoNextStepRoutine != null) StopCoroutine(autoNextStepRoutine);
            autoNextStepRoutine = StartCoroutine(AutoNextStep());
        }

        /// <summary> 5초 대기 후 완료 처리 </summary>
        private IEnumerator AutoNextStep()
        {
            yield return CoroutineData.GetWaitForSeconds(5.0f);
            CompleteStep();
        }

        public override void OnExit()
        {
            if (autoNextStepRoutine != null)
            {
                StopCoroutine(autoNextStepRoutine);
                autoNextStepRoutine = null;
            }
            base.OnExit();
        }
    }
}