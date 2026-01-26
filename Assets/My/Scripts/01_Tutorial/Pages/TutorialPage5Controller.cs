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
    public class TutorialPage5Data
    {
        [Header("Player A")]
        public TextSetting txtA_Start;
        public TextSetting txtA_Info;

        [Header("Player B")]
        public TextSetting txtB_Start;
        public TextSetting txtB_Info;
    }

    // [수정] GamePage<TutorialPage5Data> 상속
    public class TutorialPage5Controller : GamePage<TutorialPage5Data>
    {
        [Header("Page 5 UI")]
        [SerializeField] private Text descriptionText;
        [SerializeField] private Image imageFocus; 

        [Header("Settings")]
        [SerializeField] private float moveSpeed = 500f; 
        [SerializeField] private float minX = -400;
        [SerializeField] private float maxX = 400f;
        [SerializeField] private float minY = -200f;
        [SerializeField] private float maxY = 250f;
        
        [SerializeField] private float fadeDuration = 0.5f; 
        [SerializeField] private float centerMoveTime = 0.5f; 

        private Vector2 _initialPos;            
        private bool _isInitialized;
        private bool _hasStarted; 
        private bool _isInputBlocked;
        private int _currentStage; 

        private TextSetting _dataA_Info;
        private TextSetting _dataB_Start;
        private TextSetting _dataB_Info;

        // SetupData 오버라이드
        protected override void SetupData(TutorialPage5Data data)
        {
            if (descriptionText) 
                UIManager.Instance.SetText(descriptionText.gameObject, data.txtA_Start);
            
            _dataA_Info = data.txtA_Info;
            _dataB_Start = data.txtB_Start;
            _dataB_Info = data.txtB_Info;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            if (!_isInitialized && imageFocus != null)
            {
                _initialPos = imageFocus.rectTransform.anchoredPosition;
                _isInitialized = true;
            }

            _hasStarted = false;
            _isInputBlocked = false;
            _currentStage = 0; 
            
            if (imageFocus) imageFocus.rectTransform.anchoredPosition = _initialPos;
            
            SetAlpha(1f);
            SetTextAlpha(1f);
        }

        private void Update()
        {
            if (_isInputBlocked) return;
            HandleInputByStage();
        }

        private void HandleInputByStage()
        {
            if (imageFocus == null) return;

            Vector2 moveDir = Vector2.zero;
            if (_currentStage == 0) // A: 상하
            {
                if (Input.GetKey(KeyCode.UpArrow)) moveDir.y = 1;
                else if (Input.GetKey(KeyCode.DownArrow)) moveDir.y = -1;
            }
            else // B: 좌우
            {
                if (Input.GetKey(KeyCode.RightArrow)) moveDir.x = 1;
                else if (Input.GetKey(KeyCode.LeftArrow)) moveDir.x = -1;
            }

            if (moveDir != Vector2.zero)
            {
                if (!_hasStarted)
                {
                    _hasStarted = true;
                    StartCoroutine(ProcessStageSequence());
                }

                Vector2 currentPos = imageFocus.rectTransform.anchoredPosition;
                Vector2 nextPos = currentPos + (moveDir * (moveSpeed * Time.deltaTime));

                if (_currentStage == 0)
                {
                    nextPos.x = _initialPos.x;
                    nextPos.y = Mathf.Clamp(nextPos.y, _initialPos.y + minY, _initialPos.y + maxY);
                }
                else
                {
                    nextPos.y = _initialPos.y;
                    nextPos.x = Mathf.Clamp(nextPos.x, _initialPos.x + minX, _initialPos.x + maxX);
                }
                imageFocus.rectTransform.anchoredPosition = nextPos;
            }
        }

        private IEnumerator ProcessStageSequence()
        {
            yield return new WaitForSeconds(5.0f);

            _isInputBlocked = true;
            StartCoroutine(MoveFocusToCenter());

            if (_currentStage == 0)
            {
                yield return StartCoroutine(TextChangeSequence(_dataA_Info));
                yield return new WaitForSeconds(4.0f);
                yield return StartCoroutine(TextChangeSequence(_dataB_Start));

                _currentStage = 1;
                _hasStarted = false;
                _isInputBlocked = false;
            }
            else
            {
                yield return StartCoroutine(TextChangeSequence(_dataB_Info));
                yield return new WaitForSeconds(4.0f);
                CompleteStep();
            }
        }

        private IEnumerator MoveFocusToCenter()
        {
            if (imageFocus == null) yield break;
            float timer = 0f;
            Vector2 startPos = imageFocus.rectTransform.anchoredPosition;
            while (timer < centerMoveTime)
            {
                timer += Time.deltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, timer / centerMoveTime);
                imageFocus.rectTransform.anchoredPosition = Vector2.Lerp(startPos, _initialPos, progress);
                yield return null;
            }
            imageFocus.rectTransform.anchoredPosition = _initialPos;
        }

        private IEnumerator TextChangeSequence(TextSetting newTextData)
        {
            yield return StartCoroutine(FadeTextRoutine(1f, 0f));
            if (newTextData != null && descriptionText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, newTextData);
            }
            yield return StartCoroutine(FadeTextRoutine(0f, 1f));
        }

        private IEnumerator FadeTextRoutine(float startAlpha, float endAlpha)
        {
            if (descriptionText == null) yield break;
            float timer = 0f;
            SetTextAlpha(startAlpha);
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / fadeDuration;
                SetTextAlpha(Mathf.Lerp(startAlpha, endAlpha, progress));
                yield return null;
            }
            SetTextAlpha(endAlpha);
        }

        private void SetTextAlpha(float alpha)
        {
            if (descriptionText == null) return;
            Color c = descriptionText.color;
            c.a = alpha;
            descriptionText.color = c;
        }
    }
}