using System;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Pages;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary> 튜토리얼 3페이지 데이터 클래스 </summary>
    [Serializable]
    public class TutorialPage3Data
    {
        public TextSetting descriptionText; // 설명 텍스트 설정
        public TextSetting nicknamePlayerA; // 플레이어 A 닉네임 설정
        public TextSetting nicknamePlayerB; // 플레이어 B 닉네임 설정
        
        public string warningMessage; // 1차 경고 메시지
        public string resetMessage;   // 2차 초기화 메시지
    }

    /// <summary> 튜토리얼 3페이지 컨트롤러 </summary>
    public class TutorialPage3Controller : PopupGamePage<TutorialPage3Data>
    {
        [Header("Page 3 UI")]
        [SerializeField] private Text descriptionText; // 설명 텍스트 UI
        [SerializeField] private Text nicknameA; // 플레이어 A 닉네임 UI
        [SerializeField] private Text nicknameB; // 플레이어 B 닉네임 UI

        /// <summary>  데이터 설정: 텍스트 UI 적용 및 팝업 메시지 설정 </summary>
        protected override void SetupData(TutorialPage3Data data)
        {
            // UI 텍스트 설정
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);

            // 팝업 메시지 설정
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        /// <summary>  페이지 진입: 상태 초기화 </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            ResetIdleState(true); // 팝업 즉시 끄기 및 타이머 초기화
        }

        /// <summary>  매 프레임 입력 감지 및 비활성 체크 </summary>
        private void Update()
        {
            // 1. 입력 감지
            if (Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false); // 부드럽게 리셋 취소

                // 페이지 고유 로직: 숫자키 입력 처리
                if (Input.GetKeyDown(KeyCode.Alpha1)) CompleteStep(1); 
                else if (Input.GetKeyDown(KeyCode.Alpha2)) CompleteStep(2); 
            }
            else
            {
                // 2. 비활성 시간 누적
                UpdateInactivity();
            }
        }
    }
}