using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._04_Play_Q2.Pages
{
    [Serializable]
    public class PlayQ2Page2Data
    {
        public TextSetting descriptionText;
        public TextSetting questionText;
        public TextSetting[] answerTexts;
    }

    public class PlayQ2Page2Controller : PlayQ2PageBase
    {
        [Header("Page 2 UI")]
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text questionText;
        [SerializeField] private Text[] answerText;
        
        [Header("Canvas Group")]
        [SerializeField] private CanvasGroup descriptionGroup;
        [SerializeField] private CanvasGroup questionGroup;
        [SerializeField] private CanvasGroup answerGroup;
        
        private Coroutine _showSequenceRoutine;
        private bool _isCompleted;
        private bool _isInputEnabled;

        public override void SetupData(object data)
        {
            var pageData = data as PlayQ2Page2Data;
            if (pageData == null) return;

            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, pageData.descriptionText);
            if (questionText) UIManager.Instance.SetText(questionText.gameObject, pageData.questionText);

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
                    else answerText[i].gameObject.SetActive(false);
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

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha2) || 
                Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Alpha4) || 
                Input.GetKeyDown(KeyCode.Alpha5))
            {
                _isCompleted = true;
                CompleteStep(1); 
            }

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
            if (canvasGroup != null) yield return new WaitUntil(() => canvasGroup.alpha >= 1f);

            yield return StartCoroutine(FadeCanvasGroup(questionGroup, 0f, 1f, 1.0f));
            yield return StartCoroutine(FadeCanvasGroup(answerGroup, 0f, 1f, 1.0f));
            yield return StartCoroutine(FadeCanvasGroup(descriptionGroup, 0f, 1f, 1.0f));
            _isInputEnabled = true;
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end, float duration)
        {
            if (cg == null) yield break;
            float timer = 0f; cg.alpha = start;
            while (timer < duration) { timer += Time.deltaTime; cg.alpha = Mathf.Lerp(start, end, timer / duration); yield return null; }
            cg.alpha = end;
        }

        private void SetGroupAlpha(CanvasGroup cg, float alpha) { if (cg != null) cg.alpha = alpha; }
    }
}