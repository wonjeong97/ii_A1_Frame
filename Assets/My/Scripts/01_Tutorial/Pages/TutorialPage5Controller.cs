using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
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

    public class TutorialPage5Controller : TutorialPageBase
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

        // 내부 변수들
        private Vector2 _initialPos;            
        private bool _isInitialized;
        private bool _hasStarted; 
        private bool _isInputBlocked;
        private int _currentStage; 

        private TextSetting _dataA_Info;
        private TextSetting _dataB_Start;
        private TextSetting _dataB_Info;

        public override void SetupData(object data)
        {
            var pageData = data as TutorialPage5Data;
            if (pageData == null) return;

            if (descriptionText) 
                UIManager.Instance.SetText(descriptionText.gameObject, pageData.txtA_Start);
            
            _dataA_Info = pageData.txtA_Info;
            _dataB_Start = pageData.txtB_Start;
            _dataB_Info = pageData.txtB_Info;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            if (!_isInitialized && imageFocus != null)
            {
                _initialPos = imageFocus.rectTransform.anchoredPosition;
                _isInitialized = true;
            }

            // 초기화
            _hasStarted = false;
            _isInputBlocked = false;
            _currentStage = 0; 
            
            if (imageFocus) imageFocus.rectTransform.anchoredPosition = _initialPos;
            
            // 페이지 전체 알파값 초기화 (부모 클래스의 canvasGroup 활용)
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
            // 1. 5초 대기
            yield return new WaitForSeconds(5.0f);

            _isInputBlocked = true;
            StartCoroutine(MoveFocusToCenter());

            if (_currentStage == 0)
            {
                // [Player A 완료]
                yield return StartCoroutine(TextChangeSequence(_dataA_Info));
                yield return new WaitForSeconds(4.0f);
                yield return StartCoroutine(TextChangeSequence(_dataB_Start));

                // B 단계 리셋
                _currentStage = 1;
                _hasStarted = false;
                _isInputBlocked = false;
            }
            else
            {
                // [Player B 완료]
                yield return StartCoroutine(TextChangeSequence(_dataB_Info));
                yield return new WaitForSeconds(4.0f); // 텍스트 읽을 시간
                
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