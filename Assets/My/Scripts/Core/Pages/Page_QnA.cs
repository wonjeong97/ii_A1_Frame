using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Data;
using Wonjeong.UI;

namespace My.Scripts.Core.Pages
{
    public class Page_QnA : GamePage
    {
        [Header("UI References")] 
        [SerializeField] private Text descriptionText; // 하단 설명 (3번 등장)
        [SerializeField] private Text questionText; // 상단 질문 (1번 등장)
        [SerializeField] private Text[] answerTexts; // 답변 버튼들 (2번 등장)

        [Header("Canvas Groups (Animation)")] 
        [SerializeField] private CanvasGroup descriptionGroup;
        [SerializeField] private CanvasGroup questionGroup;
        [SerializeField] private CanvasGroup answerGroup;

        private Coroutine _sequenceRoutine;
        private bool _isCompleted;
        private bool _isInputEnabled;

        public override void SetupData(object data)
        {
            var pageData = data as QnAPageData;
            if (pageData == null) return;

            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, pageData.descriptionText);
            if (questionText) UIManager.Instance.SetText(questionText.gameObject, pageData.questionText);

            if (answerTexts != null)
            {
                for (int i = 0; i < answerTexts.Length; i++)
                {
                    if (!answerTexts[i]) continue;
                    if (pageData.answerTexts != null && i < pageData.answerTexts.Length)
                    {
                        UIManager.Instance.SetText(answerTexts[i].gameObject, pageData.answerTexts[i]);
                        answerTexts[i].gameObject.SetActive(true);
                    }
                    else answerTexts[i].gameObject.SetActive(false);
                }
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            _isInputEnabled = false;

            SetGroupAlpha(questionGroup, 0f);
            SetGroupAlpha(answerGroup, 0f);
            SetGroupAlpha(descriptionGroup, 0f);

            if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = StartCoroutine(ShowSequence());
        }

        private void Update()
        {
            if (_isCompleted || !_isInputEnabled) return;

            // Player A (1~5)
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha2) ||
                Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Alpha4) ||
                Input.GetKeyDown(KeyCode.Alpha5))
            {
                _isCompleted = true;
                CompleteStep(1); // 1 = A가 선택
            }

            // Player B (6~0)
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Alpha7) ||
                Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Alpha9) ||
                Input.GetKeyDown(KeyCode.Alpha0))
            {
                _isCompleted = true;
                CompleteStep(2); // 2 = B가 선택
            }
        }

        private IEnumerator ShowSequence()
        {
            // 페이지 자체가 페이드인 될 때까지 대기
            if (canvasGroup) yield return new WaitUntil(() => canvasGroup.alpha >= 0.9f);

            // 순차적 등장: 질문 -> 답변 -> 설명
            yield return StartCoroutine(FadeGroup(questionGroup, 0f, 1f, 1.0f));
            yield return StartCoroutine(FadeGroup(answerGroup, 0f, 1f, 1.0f));
            yield return StartCoroutine(FadeGroup(descriptionGroup, 0f, 1f, 1.0f));

            _isInputEnabled = true;
        }

        private IEnumerator FadeGroup(CanvasGroup cg, float start, float end, float duration)
        {
            if (!cg) yield break;
            float t = 0f;
            cg.alpha = start;
            while (t < duration)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(start, end, t / duration);
                yield return null;
            }

            cg.alpha = end;
        }

        private void SetGroupAlpha(CanvasGroup cg, float alpha)
        {
            if (cg) cg.alpha = alpha;
        }
    }
}