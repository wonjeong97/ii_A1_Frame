using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._03_Play_Q1.Pages
{   
    [Serializable]
    public class PlayQ1Page3Data
    {
        public TextSetting descriptionText;
        public TextSetting questionText;
        public TextSetting[] answerTexts; // 답변 목록
    }

    public class PlayQ1Page3Controller : PlayQ1PageBase
    {
        [Header("Page 3 UI")]
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text questionText;
        [SerializeField] private Text[] answerText; // 5개의 답변 텍스트 UI
        
        [Header("Canvas Group")]
        [SerializeField] private CanvasGroup descriptionGroup;
        [SerializeField] private CanvasGroup questionGroup;   
        [SerializeField] private CanvasGroup answerGroup;     
        
        private Coroutine _showSequenceRoutine;
        private bool _isCompleted;
        private bool _isInputEnabled;

        public override void SetupData(object data)
        {
            var pageData = data as PlayQ1Page3Data;
            if (pageData == null) return;

            if (descriptionText) 
                UIManager.Instance.SetText(descriptionText.gameObject, pageData.descriptionText);
            
            if (questionText) 
                UIManager.Instance.SetText(questionText.gameObject, pageData.questionText);

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
            _isInputEnabled = false;

            SetGroupAlpha(questionGroup, 0f);
            SetGroupAlpha(answerGroup, 0f);
            SetGroupAlpha(descriptionGroup, 0f);

            if (_showSequenceRoutine != null) StopCoroutine(_showSequenceRoutine);
            _showSequenceRoutine = StartCoroutine(ShowSequence());
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
                CompleteStep(1); 
            }

            // Player B (6~0)
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Alpha7) || 
                Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Alpha9) || 
                Input.GetKeyDown(KeyCode.Alpha0))
            {
                _isCompleted = true;
                CompleteStep(2); 
            }
        }

        private IEnumerator ShowSequence()
        {
            if (canvasGroup != null)
            {
                const float timeout = 2f;
                float t = 0f;
                while (canvasGroup.alpha < 1f && t < timeout)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            yield return StartCoroutine(FadeCanvasGroup(questionGroup, 0f, 1f, 1.0f));
            yield return StartCoroutine(FadeCanvasGroup(answerGroup, 0f, 1f, 1.0f));
            yield return StartCoroutine(FadeCanvasGroup(descriptionGroup, 0f, 1f, 1.0f));
            
            _isInputEnabled = true;
        }

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