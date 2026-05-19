using System;
using System.Text;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using My.Scripts.Hardware;
using My.Scripts.Utils;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 게임 진행 중 안내 및 대기 시간을 제공하는 트랜지션(전환) 페이지.
    /// 특정 모드에서는 아두이노 버튼 입력을 기다리거나 자동으로 다음 단계로 넘어감.
    /// 정적 인스턴스 의존성이 100% 제거되고 텍스트 서식 버그가 원천 방어되었습니다.
    /// </summary>
    public class Page_Transition : PopupGamePage<TransitionPageData>
    {
        [Header("Mode Settings")]
        [SerializeField] private bool autoPass = true; 
        [SerializeField] private float autoPassDelay = 4.0f; 
        [SerializeField] private bool keepContentOnFinish; 

        [Header("Arduino Integration")]
        [Tooltip("체크 시 카메라 연출 페이지로 동작하여, 등장 시 사운드/LED를 켜고 아두이노의 Shot 버튼 입력을 대기합니다.")]
        [SerializeField] private bool waitForShotButton;

        [Header("Common UI")] 
        [SerializeField] private Text descriptionText; 
        [SerializeField] private CanvasGroup contentGroup; 

        [Header("Intro Mode UI (Optional)")]
        [SerializeField] private Text playerAName; 
        [SerializeField] private Text playerBName; 
        [SerializeField] private CanvasGroup namesGroup; 

        private bool _isCompleted; 
        private float _enterTime; 
        
        private CancellationTokenSource _sequenceCts;

        private readonly static StringBuilder NameBuilder = new StringBuilder(128);

        // --- 의존성 주입 (DI) 변수 ---
        private SessionManager _sessionManager;
        private LevelManager _levelManager; 

        /// <summary> 부모들의 수령 체인 외에 본 페이지 내부 닉네임 및 문항 식별 처리를 위한 의존성 주입 </summary>
        [Inject]
        public void ConstructTransition(SessionManager sessionManager, LevelManager levelManager)
        {
            _sessionManager = sessionManager;
            _levelManager = levelManager;
        }

        protected override void SetupData(TransitionPageData data)
        {
            if (data == null) return;

            ApplyAndFormatText(descriptionText, data.descriptionText);
            ApplyAndFormatText(playerAName, data.playerAName);
            ApplyAndFormatText(playerBName, data.playerBName);

            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }
        
        /// <summary> 
        /// 텍스트 컴포넌트에 안전하게 데이터를 가포맷팅하여 바인딩
        /// </summary>
        private void ApplyAndFormatText(Text uiText, TextSetting setting)
        {
            if (!uiText || setting == null || string.IsNullOrEmpty(setting.text) || !_sessionManager) return;

            string nameA = string.IsNullOrWhiteSpace(_sessionManager.PlayerAFirstName) ? "PlayerA" : _sessionManager.PlayerAFirstName;
            string nameB = string.IsNullOrWhiteSpace(_sessionManager.PlayerBFirstName) ? "PlayerB" : _sessionManager.PlayerBFirstName;

            string formattedText;
            using (Utf16ValueStringBuilder sb = ZString.CreateStringBuilder())
            {
                sb.Append(setting.text);
                sb.Replace("{nameA}", nameA);
                sb.Replace("{nameB}", nameB);

                formattedText = sb.ToString();
            }
    
            if (_uiManager)
            {
                _uiManager.SetText(uiText.gameObject, setting);
            }
            uiText.text = formattedText;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            _enterTime = Time.unscaledTime;

            ResetIdleState(true);

            bool isTutorial = _levelManager && _levelManager.CurrentQuestionNumber == 0;

            if (isTutorial && waitForShotButton)
            {
                autoPass = true;
                autoPassDelay = 3.0f;
            }

            if (contentGroup) contentGroup.alpha = 0f;
            if (namesGroup) namesGroup.alpha = 0f;

            PlaySFXOnEnter();
            
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = new CancellationTokenSource();
            
            SequenceAsync(_sequenceCts.Token).Forget();
        }

        public override void OnExit()
        {
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = null;

            bool isQ15Page6 = _levelManager && _levelManager.CurrentQuestionNumber == 15 && gameObject.name.Contains("Page6");
            if (!isQ15Page6)
            {
                base.OnExit();
                return;
            }

            StopResetSequence(true);
            UnsubscribeHardwareInput();
            base.OnExit();
        }

        protected override void OnHardwareInput(string input, bool isLeft)
        {
            if (_isCompleted) return;

            bool isTutorial = _levelManager && _levelManager.CurrentQuestionNumber == 0;
            if (isTutorial && waitForShotButton) return;

            if (waitForShotButton && input == GameConstants.Hardware.InputShotOn)
            {
                ProcessManualNext();
            }
            else if (!waitForShotButton && input.EndsWith(GameConstants.Hardware.InputOnSuffix))
            {
                ProcessManualNext();
            }
        }

        private void PlaySFXOnEnter()
        {
            if (!_soundManager) return;
            
            string sfxName = gameObject.name.Contains("Page4") ? "공통_9" : gameObject.name.Contains("Page6") ? "공통_12" : null;
            if (sfxName != null)
            {
                _soundManager.PlaySFX(sfxName);
            }
        }

        private void Update()
        {
            if (_isCompleted || Time.unscaledTime - _enterTime < 1.5f) return;

            if (HasAnyInput())
            {
                HandleUserInput();
            }
            else
            {
                UpdateInactivity();
            }
        }
        
        private bool HasAnyInput()
        {
            return Input.anyKey || Input.touchCount > 0;
        }
        
        private void HandleUserInput()
        {
            ResetIdleState(false);

            if (IsInputBlockedByTutorial()) return;

            if (Input.GetKeyDown(KeyCode.Space) || !waitForShotButton)
            {
                ProcessManualNext();
            }
        }
        
        private bool IsInputBlockedByTutorial()
        {
            bool isTutorial = _levelManager && _levelManager.CurrentQuestionNumber == 0;
            return isTutorial && waitForShotButton;
        }

        private void ProcessManualNext()
        {
            if (_isCompleted) return;
            
            if (_soundManager) _soundManager.PlaySFX("공통_22");
            ResetIdleState(false);
            _isCompleted = true;

            if (waitForShotButton && _arduinoManager)
            {
                _arduinoManager.SendCommandToBoth(GameConstants.Hardware.CmdLedShotOff);
            }

            FinishAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid SequenceAsync(CancellationToken token)
        {
            if (contentGroup) contentGroup.FadeAsync(0f, 1f, 1f, token).Forget();
            if (namesGroup) namesGroup.FadeAsync(0f, 1f, 1f, token).Forget();
            
            TriggerHardwareLED(true);

            if (autoPass)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(autoPassDelay), ignoreTimeScale: true, cancellationToken: token);
                
                if (!_isCompleted)
                {
                    _isCompleted = true;
                    TriggerHardwareLED(false);
                    FinishAsync(token).Forget();
                }
            }
        }
        
        private async UniTaskVoid FinishAsync(CancellationToken token)
        {
            if (!keepContentOnFinish && descriptionText)
            {
                UniTask t1 = contentGroup ? contentGroup.FadeAsync(1f, 0f, 0.5f, token) : UniTask.CompletedTask;
                UniTask i2 = namesGroup ? namesGroup.FadeAsync(1f, 0f, 0.5f, token) : UniTask.CompletedTask;
                
                await UniTask.WhenAll(t1, i2);
            }
            
            CompleteStep();
        }
        
        private void TriggerHardwareLED(bool isOn)
        {
            if (!waitForShotButton || !_arduinoManager) return;
            
            string cmd = isOn ? GameConstants.Hardware.CmdLedShotOn : GameConstants.Hardware.CmdLedShotOff;
            _arduinoManager.SendCommandToBoth(cmd);
        }

        protected override void OnDestroy()
        {
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = null;

            base.OnDestroy();
        }
    }
}