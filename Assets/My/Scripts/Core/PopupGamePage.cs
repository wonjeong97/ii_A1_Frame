using System.Collections;
using My.Scripts.Global;
using My.Scripts.Hardware;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 리셋 팝업 기능 및 공용 입력(하드웨어/키보드) 처리를 포함한 게임 페이지 베이스 클래스
    /// </summary>
    public abstract class PopupGamePage<T> : GamePage<T> where T : class
    {
        [Header("Popup References (Base)")]
        [SerializeField] protected CanvasGroup popupCanvasGroup;
        [SerializeField] protected Text popupText;

        [Header("Popup Settings (Base)")]
        [SerializeField] protected float warningDuration = 3f; 
        [SerializeField] protected float resetPopupDuration = 3f;

        protected string msgWarning;
        protected string msgReset;
        
        protected float inactivityThreshold = 20f; 
        protected float countdownDuration = 10f;   
        
        protected float currentIdleTime = 0f;
        protected bool isResetSequenceActive = false;
        
        protected Coroutine resetSequenceRoutine;
        protected Coroutine popupFadeRoutine;

        protected virtual void Start()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);
            if (settings != null)
            {
                inactivityThreshold = settings.warningTime;
                
                float calculatedDuration = settings.resetTime - settings.warningTime - warningDuration;
                if (calculatedDuration > 0) countdownDuration = calculatedDuration;
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            SubscribeHardwareInput();
        }

        public override void OnExit()
        {
            StopResetSequence(true);
            UnsubscribeHardwareInput();
            base.OnExit();
        }

        protected virtual void OnDestroy()
        {
            UnsubscribeHardwareInput();
        }

        /// <summary>
        /// 아두이노 하드웨어 입력 이벤트를 구독합니다.
        /// </summary>
        protected void SubscribeHardwareInput()
        {
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.OnHardwareInput -= ProcessHardwareInput;
                ArduinoManager.Instance.OnHardwareInput += ProcessHardwareInput;
            }
        }

        /// <summary>
        /// 아두이노 하드웨어 입력 이벤트 구독을 해제합니다.
        /// </summary>
        protected void UnsubscribeHardwareInput()
        {
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.OnHardwareInput -= ProcessHardwareInput;
            }
        }

        /// <summary>
        /// 외부에서 들어온 입력을 필터링하고 무응답 타이머를 리셋한 뒤 자식 클래스로 전달합니다.
        /// </summary>
        /// <param name="input">입력 신호 문자열</param>
        /// <param name="isLeft">좌측 아두이노 여부</param>
        private void ProcessHardwareInput(string input, bool isLeft)
        {
            if (!gameObject.activeInHierarchy || isResetSequenceActive) return;
            ResetIdleState(false);
            OnHardwareInput(input, isLeft);
        }

        /// <summary>
        /// 필터링이 완료된 실제 입력 처리부입니다. 자식 클래스에서 오버라이드하여 구현합니다.
        /// </summary>
        /// <param name="input">입력 신호 문자열</param>
        /// <param name="isLeft">좌측 아두이노 여부</param>
        protected virtual void OnHardwareInput(string input, bool isLeft) { }

        /// <summary>
        /// PC 키보드 디버그 입력(QWERT, YUIOP)을 감지하여 아두이노 입력 포맷으로 변환 및 실행합니다.
        /// </summary>
        /// <returns>입력이 감지되었는지 여부</returns>
        protected bool ProcessCommonKeyboardInput()
        {
            int selectedValue = 0;
            bool isLeft = true;

            if (Input.GetKeyDown(KeyCode.Q)) { selectedValue = 1; isLeft = true; }
            else if (Input.GetKeyDown(KeyCode.W)) { selectedValue = 2; isLeft = true; }
            else if (Input.GetKeyDown(KeyCode.E)) { selectedValue = 3; isLeft = true; }
            else if (Input.GetKeyDown(KeyCode.R)) { selectedValue = 4; isLeft = true; }
            else if (Input.GetKeyDown(KeyCode.T)) { selectedValue = 5; isLeft = true; }
            else if (Input.GetKeyDown(KeyCode.Y)) { selectedValue = 1; isLeft = false; }
            else if (Input.GetKeyDown(KeyCode.U)) { selectedValue = 2; isLeft = false; }
            else if (Input.GetKeyDown(KeyCode.I)) { selectedValue = 3; isLeft = false; }
            else if (Input.GetKeyDown(KeyCode.O)) { selectedValue = 4; isLeft = false; }
            else if (Input.GetKeyDown(KeyCode.P)) { selectedValue = 5; isLeft = false; }

            if (selectedValue != 0)
            {
                ProcessHardwareInput($"{selectedValue}On", isLeft);
                return true;
            }
            return false;
        }

        protected void SetupPopupMessage(string warn, string reset)
        {
            msgWarning = string.IsNullOrEmpty(warn) ? string.Empty : warn;
            msgReset = string.IsNullOrEmpty(reset) ? string.Empty : reset;
        }

        protected void UpdateInactivity(bool isBlocked = false)
        {
            if (!isBlocked && !isResetSequenceActive)
            {
                currentIdleTime += Time.deltaTime;
                if (currentIdleTime >= inactivityThreshold)
                {
                    StartResetSequence();
                }
            }
        }

        protected virtual void ResetIdleState(bool immediate = false)
        {
            currentIdleTime = 0f;

            if (isResetSequenceActive)
            {
                StopResetSequence(immediate);
            }
            else if (popupCanvasGroup && popupCanvasGroup.gameObject.activeSelf)
            {
                if (immediate) StopResetSequence(true);
            }
        }

        protected virtual void StartResetSequence()
        {
            if (isResetSequenceActive) return;
            isResetSequenceActive = true;
            resetSequenceRoutine = StartCoroutine(ResetProcessRoutine());
        }

        protected virtual void StopResetSequence(bool immediate = true)
        {
            bool wasResetSequenceActive = isResetSequenceActive;
            isResetSequenceActive = false;
            currentIdleTime = 0f;
            
            if (wasResetSequenceActive)
            {
                if (SoundManager.Instance) SoundManager.Instance.StopSFX();
            }
            
            if (resetSequenceRoutine != null) StopCoroutine(resetSequenceRoutine);
            
            if (popupCanvasGroup)
            {
                if (immediate)
                {
                    popupCanvasGroup.alpha = 0f;
                    popupCanvasGroup.gameObject.SetActive(false);
                }
                else
                {
                    if (popupCanvasGroup.gameObject.activeSelf)
                    {
                        if (popupFadeRoutine != null) StopCoroutine(popupFadeRoutine);
                        popupFadeRoutine = StartCoroutine(FadeGroup(popupCanvasGroup, popupCanvasGroup.alpha, 0f, 1.0f, false));
                    }
                }
            }
        }

        protected virtual IEnumerator ResetProcessRoutine()
        {
            Debug.Log($"[{gameObject.name}] 리셋 시퀀스 시작");

            ShowPopup(msgWarning);
            
            yield return CoroutineData.GetWaitForSeconds(warningDuration); 

            if (popupCanvasGroup && popupCanvasGroup.gameObject.activeSelf)
            {
                if (popupFadeRoutine != null) StopCoroutine(popupFadeRoutine);
                popupFadeRoutine = StartCoroutine(FadeGroup(popupCanvasGroup, popupCanvasGroup.alpha, 0f, 1.0f, false));
            }
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_23");

            float timer = countdownDuration;
            while (timer > 0f)
            {
                timer -= 1.0f;
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            ShowPopup(msgReset);
            yield return CoroutineData.GetWaitForSeconds(resetPopupDuration);

            if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
            else SceneManager.LoadScene(GameConstants.Scene.Title);
        }

        protected void ShowPopup(string message)
        {
            if (!popupCanvasGroup) return;
            if (popupText) popupText.text = message;

            if (!popupCanvasGroup.gameObject.activeSelf)
            {
                popupCanvasGroup.alpha = 0f;
                popupCanvasGroup.gameObject.SetActive(true);
            }

            if (popupFadeRoutine != null) StopCoroutine(popupFadeRoutine);
            popupFadeRoutine = StartCoroutine(FadeGroup(popupCanvasGroup, popupCanvasGroup.alpha, 1f, 1.0f, true));
        }

        private IEnumerator FadeGroup(CanvasGroup cg, float start, float end, float duration, bool activeAtEnd)
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
            if (!activeAtEnd && end <= 0.01f) cg.gameObject.SetActive(false);
        }
    }
}