using System.Collections;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 리셋 팝업 기능을 포함한 게임 페이지 베이스 클래스
    /// </summary>
    public abstract class PopupGamePage<T> : GamePage<T> where T : class
    {
        [Header("Popup References (Base)")]
        [SerializeField] protected CanvasGroup popupCanvasGroup;
        [SerializeField] protected Text popupText;

        [Header("Popup Settings (Base)")]
        [SerializeField] protected float warningDuration = 3f; 
        [SerializeField] protected float resetPopupDuration = 3f;

        // 내부 변수
        protected string msgWarning;
        protected string msgReset;
        
        protected float inactivityThreshold = 20f; 
        protected float countdownDuration = 10f;   
        
        protected float currentIdleTime = 0f;
        protected bool isResetSequenceActive = false;
        
        protected Coroutine resetSequenceRoutine;
        protected Coroutine popupFadeRoutine;

        /// <summary> 설정 로드 (자식 클래스에서 base.Start() 호출 필수) </summary>
        protected virtual void Start()
        {
            var settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);
            if (settings != null)
            {
                inactivityThreshold = settings.warningTime;
                
                float calculatedDuration = settings.resetTime - settings.warningTime - warningDuration;
                if (calculatedDuration > 0) countdownDuration = calculatedDuration;
            }
        }

        /// <summary> 데이터 셋업 시 메시지 설정 (자식 클래스 SetupData에서 호출) </summary>
        protected void SetupPopupMessage(string warn, string reset)
        {
            msgWarning = string.IsNullOrEmpty(warn) ? string.Empty : warn;
            msgReset = string.IsNullOrEmpty(reset) ? string.Empty : reset;
        }

        /// <summary> 비활성 시간 누적 및 체크 (Update에서 호출) </summary>
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

        /// <summary> 입력 감지 시 상태 초기화 </summary>
        /// <param name="immediate">true: 팝업 즉시 끔, false: 페이드 아웃</param>
        protected virtual void ResetIdleState(bool immediate = false)
        {
            currentIdleTime = 0f;

            if (isResetSequenceActive)
            {
                StopResetSequence(immediate);
            }
            // 팝업이 잔존하는 경우 처리
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

            // 1. 경고 팝업 띄움
            ShowPopup(msgWarning);
            
            // 2. 3초 대기
            yield return CoroutineData.GetWaitForSeconds(warningDuration); 

            // 3. 팝업 페이드 아웃 + 사운드 재생
            if (popupCanvasGroup && popupCanvasGroup.gameObject.activeSelf)
            {
                if (popupFadeRoutine != null) StopCoroutine(popupFadeRoutine);
                popupFadeRoutine = StartCoroutine(FadeGroup(popupCanvasGroup, popupCanvasGroup.alpha, 0f, 1.0f, false));
            }
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_23");

            // 4. 카운트 대기 (팝업이 안 보이는 상태로 카운트다운)
            float timer = countdownDuration;
            while (timer > 0f)
            {
                timer -= 1.0f;
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            // 5. 초기화 확정 안내 팝업
            ShowPopup(msgReset);
            yield return CoroutineData.GetWaitForSeconds(resetPopupDuration);

            // 6. 리셋 실행
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

        /// <summary> 공용 페이드 코루틴 (마지막 인자로 SetActive(false) 제어 가능) </summary>
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
        
        public override void OnExit()
        {
            StopResetSequence(true);
            base.OnExit();
        }
    }
}