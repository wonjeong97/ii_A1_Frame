using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._02_Play.Pages
{
    [Serializable]
    public class PlayTutorialPage4Data
    {
        public TextSetting descriptionText;
    }

    public class PlayTutorialPage4Controller : PlayTutorialPageBase
    {
        [Header("Page 4 UI")]
        [SerializeField] private Text descriptionText;
        
        [Header("References")]
        [SerializeField] private CanvasGroup contentGroup; // 텍스트+이미지 그룹
        [SerializeField] private RectTransform buttonRect; // 버튼 이미지

        private bool _isCompleted = false;

        public override void SetupData(object data)
        {
            var pageData = data as PlayTutorialPage4Data;
            if (pageData == null) return;

            if (descriptionText) 
                UIManager.Instance.SetText(descriptionText.gameObject, pageData.descriptionText);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            
            // 초기화
            if (contentGroup) contentGroup.alpha = 0f;
            if (buttonRect) buttonRect.localScale = Vector3.one;

            // 자동 시퀀스 시작 (페이드인 -> 버튼연출 -> 완료)
            StartCoroutine(MainSequence());
        }

        private void Update()
        {
            // 이미 완료되었으면 입력 무시
            if (_isCompleted) return;
            
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 스페이스바를 누르면 즉시 다음 페이지로 이동
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _isCompleted = true; // 중복 호출 방지
                CompleteStep();      // 즉시 완료 처리
            }
#endif
        }

        private IEnumerator MainSequence()
        {
            // 1. 콘텐츠 페이드 인
            yield return StartCoroutine(FadeInContent());

            // 2. 버튼 연출 (페이드 인 완료 후 실행)
            // 스페이스바로 미리 넘어가버렸다면(_isCompleted == true) 연출 생략 가능
            if (!_isCompleted)
            {
                yield return StartCoroutine(ButtonSequence());
            }
        }

        private IEnumerator FadeInContent()
        {
            if (contentGroup != null)
            {
                float timer = 0f;
                while (timer < 1.0f)
                {
                    timer += Time.deltaTime;
                    contentGroup.alpha = Mathf.Clamp01(timer / 1.0f);
                    yield return null;
                }
                contentGroup.alpha = 1f;
            }
        }

        private IEnumerator ButtonSequence()
        {
            // 버튼 눌리는 효과 2회 (2초)
            if (buttonRect != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    // 스페이스바로 중간에 넘어갔으면 루프 중단
                    if (_isCompleted) yield break;

                    // 누르기
                    yield return StartCoroutine(ScaleButton(Vector3.one, Vector3.one * 0.9f, 0.5f));
                    // 떼기
                    yield return StartCoroutine(ScaleButton(Vector3.one * 0.9f, Vector3.one, 0.5f));
                }
            }
        }

        private IEnumerator ScaleButton(Vector3 startScale, Vector3 endScale, float duration)
        {
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, timer / duration);
                buttonRect.localScale = Vector3.Lerp(startScale, endScale, progress);
                yield return null;
            }
            buttonRect.localScale = endScale;
        }
    }
}