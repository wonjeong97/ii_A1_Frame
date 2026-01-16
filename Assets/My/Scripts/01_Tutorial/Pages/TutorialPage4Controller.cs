using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    // ---------------------------------------------------------
    // 데이터 클래스
    // ---------------------------------------------------------
    [Serializable]
    public class TutorialPage4Data
    {
        public TextSetting descriptionText;
    }

    // ---------------------------------------------------------
    // 컨트롤러 클래스
    // ---------------------------------------------------------
    public class TutorialPage4Controller : TutorialPageBase
    {
        [Header("Page 4 UI")]
        [SerializeField] private Text descriptionText;

        public override void SetupData(object data)
        {
            var pageData = data as TutorialPage4Data;
            if (pageData == null) return;
            
            if (descriptionText) 
                UIManager.Instance.SetText(descriptionText.gameObject, pageData.descriptionText);
        }

        // 페이지 진입 시 자동으로 타이머 시작
        public override void OnEnter()
        {
            base.OnEnter();
            StartCoroutine(AutoNextStep());
        }

        private IEnumerator AutoNextStep()
        {
            // 대기 후 다음 페이지로
            // 텍스트 컷인 1초 + 화면 유지 4초
            yield return new WaitForSeconds(5.0f);
            CompleteStep();
        }
    }
}