using System;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage1Data
    {
        public TextSetting descriptionText;
    }

    /// <summary> 튜토리얼 1페이지 컨트롤러 (엔터 키 대기) </summary>
    public class TutorialPage1Controller : GamePage<TutorialPage1Data>
    {
        [Header("Page 1 UI")]
        [SerializeField] private Text descriptionText; // 설명 텍스트

        /// <summary> 데이터 설정 (텍스트 적용) </summary>
        protected override void SetupData(TutorialPage1Data data)
        {
            if (descriptionText) 
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            }
        }

        /// <summary> 입력 감지 (Enter 키로 완료) </summary>
        private void Update()
        {
            // Enter 키 입력 시
            if (Input.GetKeyDown(KeyCode.Return))
            {
                CompleteStep(); // 단계 완료
            }
        }
    }
}