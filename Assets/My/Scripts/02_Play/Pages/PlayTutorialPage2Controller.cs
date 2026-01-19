using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._02_Play.Pages
{   
    [Serializable]
    public class PlayTutorialPage2Data
    {
        public TextSetting descriptionText;
        public TextSetting questionText;
        public TextSetting[] answerTexts; // 배열로 답변 텍스트 관리
    }

    public class PlayTutorialPage2Controller : PlayTutorialPageBase
    {
        [Header("Page 2 UI")]
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text questionText;
        [SerializeField] private Text[] answerText; // 5개의 답변 텍스트 UI 연결
        
        [Header("Canvas Group")]
        [SerializeField] private CanvasGroup descriptionGroup; // 3번 컷인
        [SerializeField] private CanvasGroup questionGroup;    // 1번 컷인
        [SerializeField] private CanvasGroup answerGroup;      // 2번 컷인
        
        private Coroutine _showSequenceRoutine;
        private bool _isCompleted;
        private bool _isInputEnabled; // 입력 활성화 플래그

        public override void SetupData(object data)
        {
            var pageData = data as PlayTutorialPage2Data;
            if (pageData == null) return;

            // 1. 설명 텍스트 적용
            if (descriptionText) 
                UIManager.Instance.SetText(descriptionText.gameObject, pageData.descriptionText);
            
            // 2. 질문 텍스트 적용
            if (questionText) 
                UIManager.Instance.SetText(questionText.gameObject, pageData.questionText);

            // 3. 답변 텍스트 적용 (배열 순회)
            if (answerText != null)
            {
                for (int i = 0; i < answerText.Length; i++)
                {
                    if (answerText[i] == null) continue;
                    if (pageData.answerTexts != null && i < pageData.answerTexts.Length)
                    {
                        UIManager.Instance.SetText(answerText[i].gameObject, pageData.answerTexts[i]);
                        answerText[i].gameObject.SetActive(true);
                    }
                    else
                    {
                        answerText[i].text = string.Empty;
                        answerText[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false; 
            _isInputEnabled = false; // [수정] 초기에는 입력 차단

            // 초기화: 모든 그룹 투명하게 시작
            SetGroupAlpha(questionGroup, 0f);
            SetGroupAlpha(answerGroup, 0f);
            SetGroupAlpha(descriptionGroup, 0f);

            // 순차적 등장 시퀀스 시작
            if (_showSequenceRoutine != null)
            {
                StopCoroutine(_showSequenceRoutine);
            }
            _showSequenceRoutine = StartCoroutine(ShowSequence());
        }

        private void Update()
        {
            // [수정] 완료되었거나 입력이 아직 활성화되지 않았으면 리턴
            if (_isCompleted || !_isInputEnabled) return;

            // [Player A] 1 ~ 5번 키 입력
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha2) || 
                Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Alpha4) || 
                Input.GetKeyDown(KeyCode.Alpha5))
            {
                _isCompleted = true;
                CompleteStep(1); // 1 = Player A Trigger
            }

            // [Player B] 6 ~ 0번 키 입력
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Alpha7) || 
                Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Alpha9) || 
                Input.GetKeyDown(KeyCode.Alpha0))
            {
                _isCompleted = true;
                CompleteStep(2); // 2 = Player B Trigger
            }
        }

        private IEnumerator ShowSequence()
        {
            // 페이지 본체(부모 CanvasGroup)의 알파값이 완전히 1이 될 때까지 대기
            if (canvasGroup != null)
            {
                yield return new WaitUntil(() => canvasGroup.alpha >= 1f);
            }

            // [1번 컷인] 질문 등장 (1초)
            yield return StartCoroutine(FadeCanvasGroup(questionGroup, 0f, 1f, 1.0f));

            // [2번 컷인] 답변 버튼들 등장 (1초)
            yield return StartCoroutine(FadeCanvasGroup(answerGroup, 0f, 1f, 1.0f));

            // [3번 컷인] 설명 텍스트 등장 (1초)
            yield return StartCoroutine(FadeCanvasGroup(descriptionGroup, 0f, 1f, 1.0f));
            
            // 모든 연출이 끝난 후 입력 활성화
            _isInputEnabled = true;
        }

        // 캔버스 그룹 페이드 연출 코루틴
        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end, float duration)
        {
            if (cg == null) yield break;

            float timer = 0f;
            cg.alpha = start;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                cg.alpha = Mathf.Lerp(start, end, timer / duration);
                yield return null;
            }
            cg.alpha = end;
        }

        private void SetGroupAlpha(CanvasGroup cg, float alpha)
        {
            if (cg != null) cg.alpha = alpha;
        }
    }
}