using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZLogger;
using VContainer;
using R3;
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

        protected float currentIdleTime;
        protected bool isResetSequenceActive;

        protected CancellationTokenSource resetSequenceCts;
        protected CancellationTokenSource popupFadeCts;

        // --- R3 이벤트 구독 관리 ---
        protected IDisposable _hardwareSubscription;

        // --- 의존성 주입 (DI) 변수 ---
        protected Microsoft.Extensions.Logging.ILogger _logger;
        protected ArduinoManager _arduinoManager;
        protected SoundManager _soundManager;
        protected GameManager _gameManager;

        [Inject]
        public virtual void ConstructPopupBase(
            ILogger<PopupGamePage<T>> logger,
            ArduinoManager arduinoManager,
            SoundManager soundManager,
            GameManager gameManager)
        {
            _logger = logger;
            _arduinoManager = arduinoManager;
            _soundManager = soundManager;
            _gameManager = gameManager;
        }

        protected virtual void Start()
        {
            string path = GameConstants.Path.GetLocalizedPath(GameConstants.Path.JsonSetting);
            Settings settings = JsonLoader.Load<Settings>(path);
    
            if (settings != null)
            {
                inactivityThreshold = settings.warningTime;

                float calculatedDuration = settings.resetTime - settings.warningTime - warningDuration;
                if (calculatedDuration > 0) countdownDuration = calculatedDuration;
            }
            else
            {
                _logger?.LogWarning("No settings found for path: " + path);
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

        protected override void OnDestroy()
        {
            UnsubscribeHardwareInput();

            resetSequenceCts?.Cancel();
            resetSequenceCts?.Dispose();
            popupFadeCts?.Cancel();
            popupFadeCts?.Dispose();

            currentIdleTime = 0f;
            
            base.OnDestroy(); 
        }

        protected void SubscribeHardwareInput()
        {
            if (_arduinoManager && _hardwareSubscription == null)
            {
                _hardwareSubscription = _arduinoManager.OnHardwareInput
                    .Subscribe(x => ProcessHardwareInput(x.command, x.isLeft));
            }
        }

        protected void UnsubscribeHardwareInput()
        {
            _hardwareSubscription?.Dispose();
            _hardwareSubscription = null;
        }

        private void ProcessHardwareInput(string input, bool isLeft)
        {
            if (!gameObject.activeInHierarchy) return;

            ResetIdleState(false);
            OnHardwareInput(input, isLeft);
        }

        protected virtual void OnHardwareInput(string input, bool isLeft)
        {
        }

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

        protected void SetupPopupMessage(string warn, string reset)
        {
            msgWarning = string.IsNullOrEmpty(warn) ? string.Empty : warn;
            msgReset = string.IsNullOrEmpty(reset) ? string.Empty : reset;
        }

        protected void UpdateInactivity(bool isBlocked = false)
        {
            if (!isBlocked && !isResetSequenceActive)
            {
                currentIdleTime += Time.unscaledDeltaTime;

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

            resetSequenceCts?.Cancel();
            resetSequenceCts?.Dispose();

            resetSequenceCts = new CancellationTokenSource();

            ResetProcessAsync(resetSequenceCts.Token).Forget();
        }

        protected virtual async UniTaskVoid ResetProcessAsync(CancellationToken token)
        {
            if (_logger != null) _logger.ZLogInformation($"[{gameObject.name}] 리셋 시퀀스 시작");

            ShowPopup(msgWarning);

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(warningDuration), ignoreTimeScale: true, cancellationToken: token);

                if (popupCanvasGroup && popupCanvasGroup.gameObject.activeSelf)
                {
                    popupFadeCts?.Cancel();
                    popupFadeCts?.Dispose();
                    popupFadeCts = new CancellationTokenSource();

                    await popupCanvasGroup.FadeAsync(popupCanvasGroup.alpha, 0f, 1.0f, popupFadeCts.Token);
                    if (popupCanvasGroup) popupCanvasGroup.gameObject.SetActive(false);
                }

                if (_soundManager) _soundManager.PlaySFX("공통_23");

                float timer = countdownDuration;
                while (timer > 0f)
                {
                    timer -= 1.0f;
                    await UniTask.Delay(TimeSpan.FromSeconds(1.0), ignoreTimeScale: true, cancellationToken: token);
                }

                ShowPopup(msgReset);
                await UniTask.Delay(TimeSpan.FromSeconds(resetPopupDuration), ignoreTimeScale: true, cancellationToken: token);

                if (_gameManager)
                {
                    _gameManager.ReturnToTitle();
                }
                else
                {
                    SceneLoader.LoadAsync(GameConstants.Scene.Title, cancellationToken: token).Forget();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        protected virtual void StopResetSequence(bool immediate = true)
        {
            bool wasResetSequenceActive = isResetSequenceActive;

            isResetSequenceActive = false;
            currentIdleTime = 0f;

            if (wasResetSequenceActive && _soundManager)
            {
                _soundManager.StopSFX();
            }

            resetSequenceCts?.Cancel();
            
            HidePopupCanvas(immediate).Forget();
        }

        private async UniTask HidePopupCanvas(bool immediate)
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
            popupFadeCts = new CancellationTokenSource();

            try
            {
                await popupCanvasGroup.FadeAsync(popupCanvasGroup.alpha, 0f, 1.0f, popupFadeCts.Token);

                if (popupCanvasGroup) popupCanvasGroup.gameObject.SetActive(false);
            }
            catch (OperationCanceledException)
            {
            }
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

            popupFadeCts?.Cancel();
            popupFadeCts?.Dispose();
            popupFadeCts = new CancellationTokenSource();

            popupCanvasGroup.FadeAsync(popupCanvasGroup.alpha, 1f, 1.0f, popupFadeCts.Token).Forget();
        }
    }
}