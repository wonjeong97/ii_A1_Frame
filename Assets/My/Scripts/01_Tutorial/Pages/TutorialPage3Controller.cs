using System;
using Cysharp.Text; 
using Microsoft.Extensions.Logging; 
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;
using ZLogger;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage3Data
    {
        public TextSetting descriptionText; 
        public TextSetting nicknamePlayerA; 
        public TextSetting nicknamePlayerB; 
        
        public string warningMessage; 
        public string resetMessage;   
    }

    /// <summary>
    /// 물리 다이얼(휠)의 회전 조작을 감지하여 특정 횟수 이상 돌리면 다음 단계로 전환하는 컨트롤러.
    /// </summary>
    public class TutorialPage3Controller : PopupGamePage<TutorialPage3Data>
    {   
        [Header("Page 3 UI")]
        [SerializeField] private Text descriptionText; 
        [SerializeField] private Text nicknameA; 
        [SerializeField] private Text nicknameB; 

        private TutorialPage3Data _data; 

        private const int StepsForFullRotation = 4;

        private PlayerWheelState _p1State;
        private PlayerWheelState _p2State;
        
        private bool _isCompleted; 

        // --- 의존성 주입 (DI) 변수 ---
        private SessionManager _sessionManager;
        private new ILogger<TutorialPage3Controller> _logger;

        [Inject]
        public void Construct(SessionManager sessionManager, SoundManager soundManager, ILogger<TutorialPage3Controller> logger)
        {
            _sessionManager = sessionManager;
            _soundManager = soundManager; 
            
            _logger = logger;
        }

        protected override void SetupData(TutorialPage3Data data)
        {
            _data = data;
            SetupPopupMessage(data?.warningMessage ?? string.Empty, data?.resetMessage ?? string.Empty);
        }

        public override object ExtractCurrentData()
        {
            return new TutorialPage3Data
            {
                descriptionText = TutorialPageUtils.BuildTextSetting(descriptionText, _data?.descriptionText),
                nicknamePlayerA = TutorialPageUtils.BuildTextSetting(nicknameA, _data?.nicknamePlayerA, _data?.nicknamePlayerA?.text),
                nicknamePlayerB = TutorialPageUtils.BuildTextSetting(nicknameB, _data?.nicknamePlayerB, _data?.nicknamePlayerB?.text),
                warningMessage  = _data?.warningMessage ?? string.Empty,
                resetMessage    = _data?.resetMessage   ?? string.Empty,
            };
        }

        public override void OnEnter()
        {
            base.OnEnter();

            _isCompleted = false; 

            if (descriptionText && _data?.descriptionText != null && _uiManager != null)
            {
                _uiManager.SetText(descriptionText.gameObject, _data.descriptionText);
            }

            SetupPlayerInfo();
            ResetIdleState(true);
            
            _p1State = PlayerWheelState.Default;
            _p2State = PlayerWheelState.Default;
        }

        private void SetupPlayerInfo()
        {
            if (!_sessionManager || _data == null) return;

            string nameA = _sessionManager.PlayerAFirstName;
            string nameB = _sessionManager.PlayerBFirstName;

            ApplyPlayerNickname(nicknameA, _data.nicknamePlayerA, nameA, nameB);
            ApplyPlayerNickname(nicknameB, _data.nicknamePlayerB, nameA, nameB);
        }

        private void ApplyPlayerNickname(Text textComp, TextSetting setting, string nameA, string nameB)
        {
            if (!textComp || setting == null || string.IsNullOrEmpty(setting.text)) return;
            
            if (_uiManager)
            {
                _uiManager.SetText(textComp.gameObject, setting);
            }

            using (Utf16ValueStringBuilder sb = ZString.CreateStringBuilder())
            {
                sb.Append(setting.text);
                sb.Replace("{nameA}", nameA ?? string.Empty);
                sb.Replace("{nameB}", nameB ?? string.Empty);

                textComp.text = sb.ToString(); 
            }
        }

        private void Update()
        {
            if (_isCompleted) return;

            HandleWheelInput();

            if (Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false);
            }
            else
            {
                UpdateInactivity();
            }
        }

        private void HandleWheelInput()
        {
            int p1Key = WheelInputUtility.GetPressedKeyIndex(1, 4);
            if (p1Key != -1)
            {
                ProcessPlayerInput(ref _p1State, p1Key, 1);
            }

            int p2Key = WheelInputUtility.GetPressedKeyIndex(5, 8);
            if (p2Key != -1)
            {
                ProcessPlayerInput(ref _p2State, p2Key, 2);
            }
        }

        private void ProcessPlayerInput(ref PlayerWheelState state, int currentKey, int playerNumber)
        {
            if (_isCompleted) return; 

            int previousDir = state.lastDir;

            int currentDir = WheelInputUtility.ResolveDirection(currentKey, 4, ref state);

            if (currentDir != 0)
            {
                state.stepCount = (currentDir == previousDir) ? state.stepCount + 1 : 1;

                if (state.stepCount >= StepsForFullRotation)
                {
                    _isCompleted = true; 
                    
                    CompleteStep(playerNumber);
                    state.stepCount = 0;
                }
            }
        }
    }
}