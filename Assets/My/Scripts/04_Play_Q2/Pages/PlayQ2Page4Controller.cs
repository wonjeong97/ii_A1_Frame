using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._04_Play_Q2.Pages
{
    [Serializable]
    public class PlayQ2Page4Data { public TextSetting descriptionText; }

    public class PlayQ2Page4Controller : PlayQ2PageBase
    {
        [Header("Page 4 UI")]
        [SerializeField] private Text descriptionText;
        [SerializeField] private CanvasGroup contentGroup; 
        [SerializeField] private RectTransform buttonRect; 

        private bool _isCompleted;

        public override void SetupData(object data)
        {
            var pageData = data as PlayQ2Page4Data;
            if (pageData == null) return;
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, pageData.descriptionText);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            if (contentGroup) contentGroup.alpha = 0f;
            if (buttonRect) buttonRect.localScale = Vector3.one;
            StartCoroutine(MainSequence());
        }

        private void Update()
        {
            if (_isCompleted) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(KeyCode.Space)) { _isCompleted = true; CompleteStep(); }
#endif
        }

        private IEnumerator MainSequence()
        {
            yield return StartCoroutine(FadeInContent());
            if (!_isCompleted) yield return StartCoroutine(ButtonSequence());
        }

        private IEnumerator FadeInContent()
        {
            if (contentGroup)
            {
                float t = 0f; while (t < 1f) { t += Time.deltaTime; contentGroup.alpha = Mathf.Clamp01(t); yield return null; }
                contentGroup.alpha = 1f;
            }
        }

        private IEnumerator ButtonSequence()
        {
            if (buttonRect)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (_isCompleted) yield break;
                    yield return StartCoroutine(ScaleButton(Vector3.one, Vector3.one * 0.9f, 0.5f));
                    yield return StartCoroutine(ScaleButton(Vector3.one * 0.9f, Vector3.one, 0.5f));
                }
            }
        }

        private IEnumerator ScaleButton(Vector3 start, Vector3 end, float dur)
        {
            float t = 0f; while (t < dur) { t += Time.deltaTime; buttonRect.localScale = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t / dur)); yield return null; }
            buttonRect.localScale = end;
        }
    }
}