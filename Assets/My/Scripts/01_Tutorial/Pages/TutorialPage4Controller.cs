using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage4Data
    {
        public TextSetting descriptionText; // 설명 텍스트 데이터
    }

    /// <summary> 튜토리얼 4페이지 컨트롤러 (자동 넘김) </summary>
    public class TutorialPage4Controller : GamePage<TutorialPage4Data>
    {
        [Header("Page 4 UI")]
        [SerializeField] private Text descriptionText; // 설명 텍스트

        /// <summary> 데이터 설정 (텍스트 적용) </summary>
        protected override void SetupData(TutorialPage4Data data)
        {
            if (descriptionText) 
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
        }

        /// <summary> 페이지 진입 (자동 넘김 시작) </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            StartCoroutine(AutoNextStep());
        }

        /// <summary> 5초 대기 후 완료 처리 </summary>
        private IEnumerator AutoNextStep()
        {
            yield return new WaitForSeconds(5.0f);
            CompleteStep();
        }
    }
}