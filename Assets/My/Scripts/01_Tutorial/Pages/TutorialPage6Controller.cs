using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using My.Scripts.Global;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;
using UnityEngine.SceneManagement;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage6Data
    {
        [Header("Player A")]
        public TextSetting txtA_Start;
        public TextSetting txtA_Info;

        [Header("Player B")]
        public TextSetting txtB_Start;
        public TextSetting txtB_Info;
        
        public string warningMessage; 
        public string resetMessage;   
    }

    /// <summary> 튜토리얼 6페이지 컨트롤러 </summary>
    public class TutorialPage6Controller : GamePage<TutorialPage6Data>
    {
        [Header("Page 6 UI")]
        [SerializeField] private Text descriptionText; // 설명 텍스트
        [SerializeField] private Image imageFocus; // 조작 대상 이미지

        [Header("Settings")]
        [SerializeField] private float moveSpeed = 500f; // 이동 속도
        [SerializeField] private float minX = -400; // X축 최소 범위
        [SerializeField] private float maxX = 400f; // X축 최대 범위
        [SerializeField] private float minY = -200f; // Y축 최소 범위
        [SerializeField] private float maxY = 250f; // Y축 최대 범위
        
        [SerializeField] private float fadeDuration = 0.5f; // 페이드 시간
        [SerializeField] private float centerMoveTime = 0.5f; // 중앙 복귀 시간

        [Header("Popup References")]
        [SerializeField] private CanvasGroup popupCanvasGroup; 
        [SerializeField] private Text popupText; 

        [Header("Popup Settings")]
        [SerializeField] private float warningDuration = 3f; 
        [SerializeField] private float resetPopupDuration = 3f;

        // 내부 로직 변수
        private string _msgWarning;
        private string _msgReset;
        
        private float _inactivityThreshold = 20f; 
        private float _countdownDuration = 10f;   
        
        private float _currentIdleTime = 0f;
        private bool _isResetSequenceActive = false;
        private Coroutine _resetSequenceRoutine;

        private Vector2 _initialPos; 
        private bool _isInitialized; 
        private bool _hasStarted; 
        private bool _isInputBlocked; 
        private int _currentStage; 

        private TextSetting _dataA_Info;
        private TextSetting _dataB_Start;
        private TextSetting _dataB_Info;
        
        private Coroutine _stageSequenceRoutine;

        private void Start()
        {
            var settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);
            if (settings != null)
            {
                _inactivityThreshold = settings.warningTime;
                
                float calculatedDuration = settings.resetTime - settings.warningTime - warningDuration;
                if (calculatedDuration > 0) _countdownDuration = calculatedDuration;
            }
        }

        protected override void SetupData(TutorialPage6Data data)
        {
            if (descriptionText) 
                UIManager.Instance.SetText(descriptionText.gameObject, data.txtA_Start);
            
            _dataA_Info = data.txtA_Info;
            _dataB_Start = data.txtB_Start;
            _dataB_Info = data.txtB_Info;

            // 메시지 적용
            if (!string.IsNullOrEmpty(data.warningMessage)) _msgWarning = data.warningMessage;
            if (!string.IsNullOrEmpty(data.resetMessage)) _msgReset = data.resetMessage;
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
            _stageSequenceRoutine = null; 
            
            // 리셋 로직 초기화
            StopResetSequence();
            _currentIdleTime = 0f;
            
            if (imageFocus) imageFocus.rectTransform.anchoredPosition = _initialPos;
            
            SetAlpha(1f);
            SetTextAlpha(1f);
        }

        private void Update()
        {
            // 1. 입력 감지
            if (Input.anyKey || Input.touchCount > 0)
            {
                // 리셋 중단 및 타이머 초기화 (입력이 있으면 무조건 초기화)
                if (_isResetSequenceActive || _currentIdleTime > 0f)
                {
                    StopResetSequence();
                }

                // 실제 이동 로직은 입력이 차단되지 않았을 때만 수행
                if (!_isInputBlocked)
                {
                    HandleInputByStage();
                }
            }
            else
            {
                // 2. 비활성 시간 누적
                // [중요] 입력이 차단된 상태(_isInputBlocked)에서는 타이머를 증가시키지 않음
                if (!_isInputBlocked && !_isResetSequenceActive)
                {
                    _currentIdleTime += Time.deltaTime;
                    if (_currentIdleTime >= _inactivityThreshold)
                    {
                        StartResetSequence();
                    }
                }
            }
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
                    _stageSequenceRoutine = StartCoroutine(ProcessStageSequence());
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

        // --- 리셋 로직 (공통 패턴) ---

        private void StartResetSequence()
        {
            if (_isResetSequenceActive) return;
            _isResetSequenceActive = true;
            _resetSequenceRoutine = StartCoroutine(ResetProcessRoutine());
        }

        private void StopResetSequence()
        {
            _isResetSequenceActive = false;
            _currentIdleTime = 0f;
            
            if (_resetSequenceRoutine != null) StopCoroutine(_resetSequenceRoutine);
            
            if (popupCanvasGroup)
            {
                popupCanvasGroup.alpha = 0f;
                popupCanvasGroup.gameObject.SetActive(false);
            }
        }

        private IEnumerator ResetProcessRoutine()
        {
            Debug.Log("[TutorialPage6] 리셋 시퀀스 시작");

            // [1단계] 경고
            ShowPopup(_msgWarning);
            yield return CoroutineData.GetWaitForSeconds(warningDuration); 

            // [2단계] 카운트다운
            float timer = _countdownDuration;
            while (timer > 0f)
            {
                timer -= 1.0f;
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            // [3단계] 초기화 안내
            ShowPopup(_msgReset);
            yield return CoroutineData.GetWaitForSeconds(resetPopupDuration);

            // [4단계] 리셋
            if (GameManager.Instance != null) GameManager.Instance.ReturnToTitle();
            else SceneManager.LoadScene(GameConstants.Scene.Title);
        }

        private void ShowPopup(string message)
        {
            if (!popupCanvasGroup) return;

            if (popupText) popupText.text = message;
            
            popupCanvasGroup.gameObject.SetActive(true);
            StartCoroutine(FadeGroup(popupCanvasGroup, popupCanvasGroup.alpha, 1f, 0.5f));
        }

        private IEnumerator FadeGroup(CanvasGroup cg, float start, float end, float duration)
        {
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

        // --- 기존 시퀀스 로직 ---

        private IEnumerator ProcessStageSequence()
        {
            yield return CoroutineData.GetWaitForSeconds(5.0f); 

            _isInputBlocked = true; // 여기서 차단되면 타이머 누적 안됨
            StartCoroutine(MoveFocusToCenter());

            if (_currentStage == 0)
            {
                yield return StartCoroutine(TextChangeSequence(_dataA_Info));
                yield return CoroutineData.GetWaitForSeconds(4.0f);
                yield return StartCoroutine(TextChangeSequence(_dataB_Start));

                _currentStage = 1;
                _hasStarted = false;
                _isInputBlocked = false; // 해제되면 다시 타이머 누적 시작
                _stageSequenceRoutine = null;
            }
            else
            {
                yield return StartCoroutine(TextChangeSequence(_dataB_Info));
                yield return CoroutineData.GetWaitForSeconds(4.0f);
                CompleteStep(); 
                _stageSequenceRoutine = null;
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

        public override void OnExit()
        {
            StopResetSequence();

            if (_stageSequenceRoutine != null)
            {
                StopCoroutine(_stageSequenceRoutine);
                _stageSequenceRoutine = null;
            }
            StopAllCoroutines();
            base.OnExit();
        }
    }
}