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
    public class TutorialPage2Data
    {
        public TextSetting descriptionText;
        public TextSetting nicknamePlayerA;
        public TextSetting nicknamePlayerB;
    }

    // ---------------------------------------------------------
    // 컨트롤러 클래스
    // ---------------------------------------------------------
    public class TutorialPage2Controller : TutorialPageBase
    {
        [Header("Page 2 UI")]
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text nicknameA;
        [SerializeField] private Text nicknameB;

        // 데이터 적용 구현
        public override void SetupData(object data)
        {
            var pageData = data as TutorialPage2Data;
            if (pageData == null) return;

            // UIManager를 통해 각 텍스트 UI 세팅
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, pageData.descriptionText);
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, pageData.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, pageData.nicknamePlayerB);
        }

        private void Update()
        {
            // 1번(A) 또는 2번(B) 키를 누르면 다음 페이지로 이동
            // CompleteStep에 인자(1 or 2)를 넘겨서 "누가 눌렀는지" 매니저에게 알림
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                CompleteStep(1); // Player A 트리거
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                CompleteStep(2); // Player B 트리거
            }
        }
    }
}