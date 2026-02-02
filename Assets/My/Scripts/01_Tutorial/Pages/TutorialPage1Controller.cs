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

        // [추가] 진입 시 텍스트 페이드 인 연출
        public override void OnEnter()
        {
            base.OnEnter(); // 기본 활성화 (Alpha 1)

            // 텍스트만 투명하게 시작해서 페이드 인
            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = 0f;
                descriptionText.color = c;
                StartCoroutine(FadeInTextRoutine());
            }
        }

        private IEnumerator FadeInTextRoutine()
        {
            float duration = 1.0f; // 1초 동안 페이드
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float alpha = Mathf.Clamp01(timer / duration);

                if (descriptionText)
                {
                    Color c = descriptionText.color;
                    c.a = alpha;
                    descriptionText.color = c;
                }
                yield return null;
            }

            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = 1f;
                descriptionText.color = c;
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