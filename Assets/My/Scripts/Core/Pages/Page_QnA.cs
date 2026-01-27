using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Data;
using Wonjeong.UI;

namespace My.Scripts.Core.Pages
{
    /// <summary> 질문 및 답변 선택 페이지 컨트롤러 </summary>
    public class Page_QnA : GamePage<QnAPageData>
    {
        [Header("UI References")] 
        [SerializeField] private Text descriptionText; // 하단 설명 텍스트
        [SerializeField] private Text questionText; // 상단 질문 텍스트
        [SerializeField] private Text[] answerTexts; // 답변 버튼 텍스트 배열

        [Header("Canvas Groups (Animation)")] 
        [SerializeField] private CanvasGroup descriptionGroup; // 설명 그룹
        [SerializeField] private CanvasGroup questionGroup; // 질문 그룹
        [SerializeField] private CanvasGroup answerGroup; // 답변 그룹

        private Coroutine _sequenceRoutine; // 연출 코루틴
        private bool _isCompleted; // 완료 여부
        private bool _isInputEnabled; // 입력 가능 여부

        /// <summary> 데이터 설정 (질문/답변 텍스트 적용) </summary>
        protected override void SetupData(QnAPageData data)
        {
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            if (questionText) UIManager.Instance.SetText(questionText.gameObject, data.questionText);

            if (answerTexts != null)
            {
                for (int i = 0; i < answerTexts.Length; i++)
                {
                    if (!answerTexts[i]) continue;
                    if (data.answerTexts != null && i < data.answerTexts.Length)
                    {
                        UIManager.Instance.SetText(answerTexts[i].gameObject, data.answerTexts[i]);
                        answerTexts[i].gameObject.SetActive(true);
                    }
                    else answerTexts[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary> 페이지 진입 (초기화 및 등장 연출) </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            _isInputEnabled = false;

            // 그룹 투명화 초기화
            SetGroupAlpha(questionGroup, 0f);
            SetGroupAlpha(answerGroup, 0f);
            SetGroupAlpha(descriptionGroup, 0f);

            if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = StartCoroutine(ShowSequence());
        }

        /// <summary> 입력 감지 (숫자키 선택) </summary>
        private void Update()
        {
            if (_isCompleted || !_isInputEnabled) return;

            // Player A (1~5)
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha2) ||
                Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Alpha4) ||
                Input.GetKeyDown(KeyCode.Alpha5))
            {
                _isCompleted = true;
                CompleteStep(1); // 1: A 선택
            }

            // Player B (6~0)
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Alpha7) ||
                Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Alpha9) ||
                Input.GetKeyDown(KeyCode.Alpha0))
            {
                _isCompleted = true;
                CompleteStep(2); // 2: B 선택
            }
        }

        /// <summary> 순차 등장 연출 (질문 -> 답변 -> 설명) </summary>
        private IEnumerator ShowSequence()
        {
            // 페이지 페이드 인 대기
            if (canvasGroup) yield return new WaitUntil(() => canvasGroup.alpha >= 0.9f);

            // 순차 등장
            yield return StartCoroutine(FadeGroup(questionGroup, 0f, 1f, 1.0f));
            yield return StartCoroutine(FadeGroup(answerGroup, 0f, 1f, 1.0f));
            yield return StartCoroutine(FadeGroup(descriptionGroup, 0f, 1f, 1.0f));

            _isInputEnabled = true;
        }

        /// <summary> 그룹 투명도 페이드 코루틴 </summary>
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

        /// <summary> 그룹 투명도 즉시 설정 </summary>
        private void SetGroupAlpha(CanvasGroup cg, float alpha)
        {
            if (cg) cg.alpha = alpha;
        }
    }
}