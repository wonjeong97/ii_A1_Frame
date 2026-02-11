using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Pages;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary> 튜토리얼 1페이지 데이터 클래스 </summary>
    [Serializable]
    public class TutorialPage1Data
    {
        public TextSetting descriptionText; // 설명 텍스트 데이터
        
        public string warningMessage; // 1차 경고 메시지
        public string resetMessage;   // 2차 초기화 메시지
    }

    /// <summary> 튜토리얼 1페이지 컨트롤러 </summary>
    public class TutorialPage1Controller : PopupGamePage<TutorialPage1Data>
    {
        [Header("Page 1 UI")]
        [SerializeField] private Text descriptionText; // 설명 텍스트 UI

        private readonly float fadeTime = 1f;

        /// <summary>  데이터 설정: 텍스트 UI 적용 및 팝업 메시지 설정 </summary>
        protected override void SetupData(TutorialPage1Data data)
        {
            // UI 텍스트 설정
            if (descriptionText) 
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            }

            // 팝업 메시지 설정 (부모 메서드 호출)
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        /// <summary>  페이지 진입: 상태 초기화 및 연출 시작 </summary>
        public override void OnEnter()
        {
            base.OnEnter(); 

            // 팝업 즉시 끄기 및 타이머 초기화
            ResetIdleState(true);

            // 텍스트 페이드 인 연출 
            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = 0f;
                descriptionText.color = c;
                StartCoroutine(FadeInTextRoutine());
            }
        }

        /// <summary>  매 프레임 입력 감지 및 비활성 체크 </summary>
        private void Update()
        {
            // 1. 입력 감지
            if (Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false); // 부드럽게 리셋 취소

                // 페이지 고유 로직: Enter 키 입력 시 성공 처리
                if (Input.GetKeyDown(KeyCode.Return))
                {
                    CompleteStep(); 
                }
            }
            else
            {
                // 2. 비활성 시간 누적
                UpdateInactivity();
            }
        }

        /// <summary>  텍스트 페이드 인 연출 코루틴 </summary>
        private IEnumerator FadeInTextRoutine()
        {
            float timer = 0f;

            while (timer < fadeTime)
            {
                timer += Time.deltaTime;
                float alpha = Mathf.Clamp01(timer / fadeTime);

                if (descriptionText)
                {
                    Color c = descriptionText.color;
                    c.a = alpha;
                    descriptionText.color = c;
                }
                yield return null;
            }

            // 최종값 보정
            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = 1f;
                descriptionText.color = c;
            }
        }
    }
}