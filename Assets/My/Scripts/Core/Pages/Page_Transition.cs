using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using My.Scripts.Hardware;
using My.Scripts.Utils;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 게임 진행 중 안내 및 대기 시간을 제공하는 트랜지션(전환) 페이지.
    /// 특정 모드에서는 아두이노 버튼 입력을 기다리거나 자동으로 다음 단계로 넘어감.
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

        /// <summary> 외부에서 전달받은 데이터를 UI 컴포넌트에 바인딩함. 누락 시 경고 로그를 출력함. </summary>
        protected override void SetupData(TransitionPageData data)
        {
            if (data == null) return;

            ApplyTextSetting(descriptionText, data.descriptionText, "descriptionText");
            ApplyTextSetting(playerAName, data.playerAName, "playerAName");
            ApplyTextSetting(playerBName, data.playerBName, "playerBName");

            ReplaceNameTags(descriptionText);
            ReplaceNameTags(playerAName);
            ReplaceNameTags(playerBName);

            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }
        
        /// <summary> 텍스트 컴포넌트에 데이터를 안전하게 할당하고, 누락 시 경고를 출력함. </summary>
        private void ApplyTextSetting(Text uiText, TextSetting setting, string fieldName)
        {
            if (!uiText) return;
            
            if (setting != null)
            {
                UIManager.Instance.SetText(uiText.gameObject, setting);
            }
            else
            {
                Debug.LogWarning($"{fieldName} 데이터 누락됨.");
            }
        }
        
        /// <summary> 텍스트 내의 포맷 문자열을 실제 유저 세션의 닉네임으로 치환함 (GC Zero). </summary>
        private void ReplaceNameTags(Text txt)
        {
            if (!txt || !SessionManager.Instance || string.IsNullOrEmpty(txt.text)) return;

            string nameA = SessionManager.Instance.PlayerAFirstName;
            string nameB = SessionManager.Instance.PlayerBFirstName;
            
            if (string.IsNullOrWhiteSpace(nameA))
            {
                Debug.LogWarning("PlayerAFirstName 값이 누락됨.");
                nameA = "PlayerA"; 
            }
            if (string.IsNullOrWhiteSpace(nameB))
            {
                Debug.LogWarning("PlayerBFirstName 값이 누락됨.");
                nameB = "PlayerB"; 
            }
            
            NameBuilder.Clear();
            NameBuilder.Append(txt.text);
            NameBuilder.Replace("{nameA}", nameA);
            NameBuilder.Replace("{nameB}", nameB);
            
            txt.text = NameBuilder.ToString();
        }

        /// <summary> 페이지 진입 시 타이머 초기화 및 페이드인 연출을 시작함. 튜토리얼 예외 처리를 포함함. </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            _enterTime = Time.time;

            ResetIdleState(true);

            bool isTutorial = LevelManager.Instance && LevelManager.Instance.CurrentQuestionNumber == 0;

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
            _sequenceCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            
            SequenceAsync(_sequenceCts.Token).Forget();
        }

        /// <summary> 특정 씬(15번 문항)에서의 강제 초기화 루틴 중단을 처리하고 페이지를 벗어남. </summary>
        public override void OnExit()
        {
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = null;

            bool isQ15Page6 = LevelManager.Instance && 
                              LevelManager.Instance.CurrentQuestionNumber == 15 && 
                              gameObject.name.Contains("Page6");

            if (isQ15Page6)
            {
                StopResetSequence(true);
                UnsubscribeHardwareInput();
            }
            else
            {
                base.OnExit();
            }
        }

        /// <summary> 아두이노 하드웨어 입력 이벤트를 처리함. </summary>
        protected override void OnHardwareInput(string input, bool isLeft)
        {
            if (_isCompleted) return;

            // 튜토리얼 모드에서는 유저의 물리적인 하드웨어 조작을 완전히 무시하여 흐름을 통제함.
            bool isTutorial = false;
            if (LevelManager.Instance) isTutorial = LevelManager.Instance.CurrentQuestionNumber == 0;

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

        /// <summary> 페이지 종류에 맞는 입장 효과음을 재생함. </summary>
        private void PlaySFXOnEnter()
        {
            if (!SoundManager.Instance) return;
            
            if (gameObject.name.Contains("Page4"))
            {
                SoundManager.Instance.PlaySFX("공통_9");
            }
            else if (gameObject.name.Contains("Page6"))
            {
                SoundManager.Instance.PlaySFX("공통_12");
            }
        }

        /// <summary> 매 프레임 키보드 또는 터치 입력을 검사하여 수동 진행을 처리함. </summary>
        private void Update()
        {
            // 더블 클릭 등 잦은 입력으로 인한 의도치 않은 빠른 스킵을 방지하기 위한 유예 시간.
            if (_isCompleted || Time.time - _enterTime < 1.5f) return;

            if (HasAnyInput())
            {
                HandleUserInput();
            }
            else
            {
                UpdateInactivity();
            }
        }
        
        /// <summary> 터치 또는 키보드 입력이 발생했는지 확인함. </summary>
        private bool HasAnyInput()
        {
            return Input.anyKey || Input.touchCount > 0;
        }
        
        /// <summary> 유저 입력이 발생했을 때 타이머를 초기화하고 수동 스킵 여부를 판별함. </summary>
        private void HandleUserInput()
        {
            ResetIdleState(false);

            // 튜토리얼 모드에서는 키보드 입력을 통한 스킵을 차단함.
            if (IsInputBlockedByTutorial()) return;

            if (Input.GetKeyDown(KeyCode.Space) || !waitForShotButton)
            {
                ProcessManualNext();
            }
        }
        
        /// <summary> 현재 튜토리얼 셔터 대기 상태인지 확인하여 강제 스킵을 방지함. </summary>
        private bool IsInputBlockedByTutorial()
        {
            bool isTutorial = LevelManager.Instance && LevelManager.Instance.CurrentQuestionNumber == 0;
            return isTutorial && waitForShotButton;
        }

        /// <summary> 수동 조작을 통한 다음 페이지 진행 로직을 실행함. </summary>
        private void ProcessManualNext()
        {
            if (_isCompleted) return;
            
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_22");
            ResetIdleState(false);
            _isCompleted = true;

            if (waitForShotButton && ArduinoManager.Instance)
            {
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedShotOff);
            }

            FinishAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// UI를 점진적으로 밝히고 하드웨어 연출 및 자동 진행 타이머를 시작함.
        /// 각 연출 단계를 분리하여 전체 흐름을 직관적으로 파악할 수 있게 함.
        /// </summary>
        private async UniTaskVoid SequenceAsync(CancellationToken token)
        {
            if (contentGroup) UIFadeUtility.FadeCanvasGroupAsync(contentGroup, 0f, 1f, 1f, token).Forget();
            if (namesGroup) UIFadeUtility.FadeCanvasGroupAsync(namesGroup, 0f, 1f, 1f, token).Forget();
            
            TriggerHardwareLED(true);

            if (autoPass)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(autoPassDelay), cancellationToken: token);
                
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
                UniTask t1 = contentGroup ? UIFadeUtility.FadeCanvasGroupAsync(contentGroup, 1f, 0f, 0.5f, token) : UniTask.CompletedTask;
                UniTask t2 = namesGroup ? UIFadeUtility.FadeCanvasGroupAsync(namesGroup, 1f, 0f, 0.5f, token) : UniTask.CompletedTask;
                
                await UniTask.WhenAll(t1, t2);
            }
            
            CompleteStep();
        }
        
        /// <summary>
        /// 셔터 버튼 대기 모드일 경우 아두이노 LED를 켜거나 끔.
        /// </summary>
        private void TriggerHardwareLED(bool isOn)
        {
            if (!waitForShotButton || !ArduinoManager.Instance) return;
            
            string cmd = isOn ? GameConstants.Hardware.CmdLedShotOn : GameConstants.Hardware.CmdLedShotOff;
            ArduinoManager.Instance.SendCommandToBoth(cmd);
        }
    }
}