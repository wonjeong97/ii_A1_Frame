using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using My.Scripts.Global;
using My.Scripts.Hardware;
using My.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core
{
    /// <summary>
    /// 일정 시간 무응답 시 경고 팝업을 띄우고 타이틀로 복귀(리셋)하는 기능을 제공하는 추상 기반 클래스.
    /// 하드웨어 및 키보드 입력을 공통으로 수신하여 자식 클래스(각 개별 페이지)로 전달합니다.
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
        
        protected float inactivityThreshold = 30f; 
        protected float countdownDuration = 10f;   
        
        protected float currentIdleTime;
        protected bool isResetSequenceActive;
        
        protected CancellationTokenSource resetSequenceCts;
        protected CancellationTokenSource popupFadeCts;

        /// <summary> JSON 설정 파일에서 무응답 경고 및 리셋 타이머 기준값을 동적으로 불러와 할당합니다. </summary>
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

        /// <summary> 페이지 활성화 시 아두이노 하드웨어 입력 이벤트를 수신 대기합니다. </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            SubscribeHardwareInput();
        }

        /// <summary> 페이지 비활성화 시 입력 이벤트 구독을 해제하고 진행 중인 리셋 시퀀스를 강제 종료합니다. </summary>
        public override void OnExit()
        {
            StopResetSequence(true);
            UnsubscribeHardwareInput();
            base.OnExit();
        }

        /// <summary> 객체 파괴 시 메모리 누수 방지를 위해 이벤트 구독을 완전히 해제합니다. </summary>
        protected virtual void OnDestroy()
        {
            UnsubscribeHardwareInput();
        }

        /// <summary> 아두이노 매니저의 입력 콜백을 연결합니다. 중복 구독 방지를 위해 해제 후 재구독합니다. </summary>
        protected void SubscribeHardwareInput()
        {
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.onHardwareInput -= ProcessHardwareInput;
                ArduinoManager.Instance.onHardwareInput += ProcessHardwareInput;
            }
        }

        /// <summary> 아두이노 매니저의 입력 콜백 연결을 끊어 예기치 않은 동작을 막습니다. </summary>
        protected void UnsubscribeHardwareInput()
        {
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.onHardwareInput -= ProcessHardwareInput;
            }
        }
        
        /// <summary> 외부 입력 발생 시 무응답 타이머를 즉시 초기화하여 리셋을 방지하고, 유효한 입력을 자식 클래스로 전달합니다. </summary>
        private void ProcessHardwareInput(string input, bool isLeft)
        {
            if (!gameObject.activeInHierarchy) return;
            
            // 유효한 하드웨어 입력이 들어왔으므로 무응답 상태(및 리셋 팝업)를 해제합니다.
            ResetIdleState(false);
            
            // 실제 자식 페이지(Page_QnA 등)로 입력을 전달하여 정답 처리를 수행합니다.
            OnHardwareInput(input, isLeft);
        }
        // =========================================================================================

        /// <summary> 필터링이 완료된 실제 하드웨어 입력 처리부입니다. 상속받은 개별 페이지에서 오버라이드하여 구현합니다. </summary>
        protected virtual void OnHardwareInput(string input, bool isLeft) { }

        /// <summary> 
        /// 아두이노 장비가 없는 PC 개발 환경에서 키보드(QWERT/YUIOP)를 통해 하드웨어 1~5번 버튼 입력을 에뮬레이션합니다. 
        /// 동적 문자열 보간을 제거하고 상수를 직접 매핑하여 가비지(GC) 발생을 원천 차단함.
        /// </summary>
        protected bool ProcessCommonKeyboardInput()
        {
            (string inputCommand, bool isLeft) = GetEmulatedKey();

            if (!string.IsNullOrEmpty(inputCommand))
            {
                ProcessHardwareInput(inputCommand, isLeft);
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 눌린 키보드 키를 판별하여 해당하는 하드웨어 버튼 명령어(상수)와 좌우 위치를 즉시 반환함.
        /// </summary>
        private (string command, bool isLeft) GetEmulatedKey()
        {
            if (Input.GetKeyDown(KeyCode.Q)) return (GameConstants.Hardware.Input1On, true);
            if (Input.GetKeyDown(KeyCode.W)) return (GameConstants.Hardware.Input2On, true);
            if (Input.GetKeyDown(KeyCode.E)) return (GameConstants.Hardware.Input3On, true);
            if (Input.GetKeyDown(KeyCode.R)) return (GameConstants.Hardware.Input4On, true);
            if (Input.GetKeyDown(KeyCode.T)) return (GameConstants.Hardware.Input5On, true);
            
            if (Input.GetKeyDown(KeyCode.Y)) return (GameConstants.Hardware.Input1On, false);
            if (Input.GetKeyDown(KeyCode.U)) return (GameConstants.Hardware.Input2On, false);
            if (Input.GetKeyDown(KeyCode.I)) return (GameConstants.Hardware.Input3On, false);
            if (Input.GetKeyDown(KeyCode.O)) return (GameConstants.Hardware.Input4On, false);
            if (Input.GetKeyDown(KeyCode.P)) return (GameConstants.Hardware.Input5On, false);

            return (null, true);
        }

        /// <summary> 파생 클래스에서 JSON 데이터를 로드할 때 팝업용 메시지 텍스트를 초기화하기 위해 호출합니다. </summary>
        protected void SetupPopupMessage(string warn, string reset)
        {
            msgWarning = string.IsNullOrEmpty(warn) ? string.Empty : warn;
            msgReset = string.IsNullOrEmpty(reset) ? string.Empty : reset;
        }

        /// <summary> 매 프레임 무응답 시간을 누적하고 임계치(inactivityThreshold) 도달 시 리셋 시퀀스를 트리거합니다. </summary>
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

        /// <summary> 유효한 유저 입력이 들어왔을 때 타이머를 0으로 되돌리고 팝업 및 리셋 대기 상태를 해제합니다. </summary>
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

        /// <summary> 무응답 임계치 도달 시 팝업을 노출하고 타이틀 자동 복귀 카운트다운을 시작합니다. </summary>
        protected virtual void StartResetSequence()
        {
            if (isResetSequenceActive) return;
            isResetSequenceActive = true;
            
            // 기존 코루틴 취소 및 새로운 토큰 발행
            resetSequenceCts?.Cancel();
            resetSequenceCts?.Dispose();
            resetSequenceCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            
            ResetProcessAsync(resetSequenceCts.Token).Forget();
        }
        
        protected virtual async UniTaskVoid ResetProcessAsync(CancellationToken token)
        {
            Debug.Log($"[{gameObject.name}] 리셋 시퀀스 시작");

            ShowPopup(msgWarning);
            
            await UniTask.Delay(TimeSpan.FromSeconds(warningDuration), cancellationToken: token);

            if (popupCanvasGroup && popupCanvasGroup.gameObject.activeSelf)
            {
                popupFadeCts?.Cancel();
                popupFadeCts?.Dispose();
                popupFadeCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                
                UIFadeUtility.FadeCanvasGroupAsync(popupCanvasGroup, popupCanvasGroup.alpha, 0f, 1.0f, popupFadeCts.Token).Forget();
            }
            
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_23");

            float timer = countdownDuration;
            while (timer > 0f)
            {
                timer -= 1.0f;
                await UniTask.Delay(TimeSpan.FromSeconds(1.0), cancellationToken: token);
            }

            ShowPopup(msgReset);
            await UniTask.Delay(TimeSpan.FromSeconds(resetPopupDuration), cancellationToken: token);

            if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
            else SceneLoader.LoadAsync(GameConstants.Scene.Title).Forget();
        }

        /// <summary> 
        /// 유저 개입으로 인해 진행 중이던 리셋 시퀀스를 중단하고 팝업 UI를 화면에서 치웁니다. 
        /// </summary>
        protected virtual void StopResetSequence(bool immediate = true)
        {
            bool wasResetSequenceActive = isResetSequenceActive;
            
            isResetSequenceActive = false;
            currentIdleTime = 0f;
            
            if (wasResetSequenceActive && SoundManager.Instance)
            {
                SoundManager.Instance.StopSFX();
            }
            
            resetSequenceCts?.Cancel();
            HidePopupCanvas(immediate);
        }
        
        private void HidePopupCanvas(bool immediate)
        {
            if (!popupCanvasGroup) return;

            if (immediate)
            {
                popupFadeCts?.Cancel();
                popupCanvasGroup.alpha = 0f;
                popupCanvasGroup.gameObject.SetActive(false);
                return;
            }

            if (!popupCanvasGroup.gameObject.activeSelf) return;

            popupFadeCts?.Cancel();
            popupFadeCts?.Dispose();
            popupFadeCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            
            UIFadeUtility.FadeCanvasGroupAsync(popupCanvasGroup, popupCanvasGroup.alpha, 0f, 1.0f, popupFadeCts.Token).Forget();
        }

        /// <summary> 지정된 메시지로 팝업 텍스트를 갱신하고 페이드인 연출을 가동합니다. </summary>
        protected void ShowPopup(string message)
        {
            if (!popupCanvasGroup) return;
            if (popupText) popupText.text = message;

            if (!popupCanvasGroup.gameObject.activeSelf)
            {
                popupCanvasGroup.alpha = 0f;
                popupCanvasGroup.gameObject.SetActive(true);
            }

            popupFadeCts?.Cancel();
            popupFadeCts?.Dispose();
            popupFadeCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            
            UIFadeUtility.FadeCanvasGroupAsync(popupCanvasGroup, popupCanvasGroup.alpha, 1f, 1.0f, popupFadeCts.Token).Forget();
        }
    }
}