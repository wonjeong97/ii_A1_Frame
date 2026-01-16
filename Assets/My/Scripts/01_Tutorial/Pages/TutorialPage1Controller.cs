using System;
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
    public class TutorialPage1Data
    {
        public TextSetting descriptionText; // "두 사람 모두 태그하면..."
    }

    // ---------------------------------------------------------
    // 컨트롤러 클래스
    // ---------------------------------------------------------
    public class TutorialPage1Controller : TutorialPageBase
    {
        [Header("Page 1 UI")]
        [SerializeField] private Text descriptionText;

        // 데이터 적용 구현
        public override void SetupData(object data)
        {
            // object로 들어온 데이터를 TutorialPage1Data으로 형변환
            var pageData = data as TutorialPage1Data;
            if (pageData == null) return;

            // UI 매니저를 통해 텍스트 세팅
            if (descriptionText) 
            {
                UIManager.Instance.SetText(descriptionText.gameObject, pageData.descriptionText);
            }
        }

        private void Update()
        {
            // 엔터키를 누르면 다음 단계로 진행
            if (Input.GetKeyDown(KeyCode.Return))
            {
                CompleteStep(); // 매니저에게 "완료" 신호 전송
            }
        }
    }
}