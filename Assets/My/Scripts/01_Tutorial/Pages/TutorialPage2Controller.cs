using System;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage2Data
    {
        public TextSetting descriptionText;
        public TextSetting nicknamePlayerA;
        public TextSetting nicknamePlayerB;
    }

    /// <summary> 튜토리얼 2페이지 컨트롤러 (플레이어 선택) </summary>
    public class TutorialPage2Controller : GamePage<TutorialPage2Data>
    {
        [Header("Page 2 UI")]
        [SerializeField] private Text descriptionText; // 설명 텍스트
        [SerializeField] private Text nicknameA; // 플레이어 A 닉네임
        [SerializeField] private Text nicknameB; // 플레이어 B 닉네임

        /// <summary> 데이터 설정 (UI 텍스트 적용) </summary>
        protected override void SetupData(TutorialPage2Data data)
        {
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);
        }

        /// <summary> 입력 감지 (숫자키 1, 2) </summary>
        private void Update()
        {
            // 1번 키 입력 (Player A 선택)
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                CompleteStep(1); 
            }
            // 2번 키 입력 (Player B 선택)
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                CompleteStep(2); 
            }
        }
    }
}