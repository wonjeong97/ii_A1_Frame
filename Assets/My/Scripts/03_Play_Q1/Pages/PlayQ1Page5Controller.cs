using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._03_Play_Q1.Pages
{
    [Serializable]
    public class PlayQ1Page5Data
    {
        public TextSetting descriptionText;
    }

    public class PlayQ1Page5Controller : PlayQ1PageBase
    {
        [Header("Page 5 UI")]
        [SerializeField] private Text descriptionText;
        
        [Header("References")]
        [SerializeField] private CanvasGroup contentGroup; 
        [SerializeField] private RectTransform buttonRect; 

        private bool _isCompleted = false;

        public override void SetupData(object data)
        {
            var pageData = data as PlayQ1Page5Data;
            if (pageData == null) return;

            if (descriptionText) 
                UIManager.Instance.SetText(descriptionText.gameObject, pageData.descriptionText);
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
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _isCompleted = true; 
                CompleteStep();      
            }
#endif
        }

        private IEnumerator MainSequence()
        {
            yield return StartCoroutine(FadeInContent());

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
            if (buttonRect != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (_isCompleted) yield break;
                    yield return StartCoroutine(ScaleButton(Vector3.one, Vector3.one * 0.9f, 0.5f));
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