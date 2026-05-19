using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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
    public class TutorialPage4Data
    {
        public TextSetting nicknamePlayerA; 
        public TextSetting nicknamePlayerB; 
        public string warningMessage; 
        public string resetMessage;   
    }

    /// <summary>
    /// 각 플레이어가 다이얼을 돌려 조명을 활성화하는 과정을 제어하는 컨트롤러.
    /// 외부 트리거와 물리 입력의 중복 간섭으로 인한 비동기 연출 꼬임 현상이 완벽히 방어되었습니다.
    /// </summary>
    public class TutorialPage4Controller : PopupGamePage<TutorialPage4Data>, ITriggerReceiver
    {   
        [Header("Page 4 UI")]
        [SerializeField] private Text nicknameA; 
        [SerializeField] private Text nicknameB; 
        [SerializeField] private Image imgBackA; 
        [SerializeField] private Image imgLightA; 
        [SerializeField] private Image imgBackB; 
        [SerializeField] private Image imgLightB; 

        private TutorialPage4Data _data;
        private bool isLightOnA; 
        private bool isLightOnB; 
        private bool _completionStarted; 
        
        private bool _isLightAChanging;
        private bool _isLightBChanging;
        
        private const int StepsForFullRotation = 3; 

        private PlayerWheelState _p1State;
        private PlayerWheelState _p2State;

        // --- 의존성 주입 (DI) 변수 ---
        private SessionManager _sessionManager;
        private new ILogger<TutorialPage4Controller> _logger;

        [Inject]
        public void Construct(
            GameManager gameManager, 
            SessionManager sessionManager, 
            SoundManager soundManager, 
            ILogger<TutorialPage4Controller> logger)
        {
            _gameManager = gameManager;
            _sessionManager = sessionManager;
            _soundManager = soundManager; 
            _logger = logger;
        }

        protected override void SetupData(TutorialPage4Data data)
        {
            _data = data;
            SetupPopupMessage(data?.warningMessage ?? string.Empty, data?.resetMessage ?? string.Empty);
        }

        public override object ExtractCurrentData()
        {
            return new TutorialPage4Data
            {
                nicknamePlayerA = TutorialPageUtils.BuildTextSetting(nicknameA, _data?.nicknamePlayerA, _data?.nicknamePlayerA?.text),
                nicknamePlayerB = TutorialPageUtils.BuildTextSetting(nicknameB, _data?.nicknamePlayerB, _data?.nicknamePlayerB?.text),
                warningMessage  = _data?.warningMessage ?? string.Empty,
                resetMessage    = _data?.resetMessage   ?? string.Empty,
            };
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            if (nicknameA && _data?.nicknamePlayerA != null && _uiManager)
            {
                _uiManager.SetText(nicknameA.gameObject, _data.nicknamePlayerA);
            }
            if (nicknameB && _data?.nicknamePlayerB != null && _uiManager)
            {
                _uiManager.SetText(nicknameB.gameObject, _data.nicknamePlayerB);
            }

            SetupPlayerInfo();
            ResetVisualStates();

            isLightOnA = false;
            isLightOnB = false;
            _completionStarted = false;
            _isLightAChanging = false;
            _isLightBChanging = false;
            
            _p1State = PlayerWheelState.Default;
            _p2State = PlayerWheelState.Default;
            
            ResetIdleState(true); 

            if (_soundManager)
            {
                _soundManager.PlaySFX("공통_1");
            }
        }
        
        private void SetupPlayerInfo()
        {
            if (!_sessionManager || _data == null) return;

            string nameA = _sessionManager.PlayerAFirstName;
            string nameB = _sessionManager.PlayerBFirstName;

            ApplyPlayerNickname(nicknameA, _data.nicknamePlayerA, nameA, nameB);
            ApplyPlayerNickname(nicknameB, _data.nicknamePlayerB, nameA, nameB);

            if (_gameManager)
            {
                ApplyPlayerSprite(imgBackA, _sessionManager.PlayerAColor);
                ApplyPlayerSprite(imgBackB, _sessionManager.PlayerBColor);
            }
        }

        private void ApplyPlayerNickname(Text textComp, TextSetting setting, string nameA, string nameB)
        {
            if (!textComp || setting == null || string.IsNullOrEmpty(setting.text)) return;

            using (var sb = ZString.CreateStringBuilder())
            {
                sb.Append(setting.text);
                sb.Replace("{nameA}", nameA ?? string.Empty);
                sb.Replace("{nameB}", nameB ?? string.Empty);

                textComp.text = sb.ToString();
            }
        }

        private void ApplyPlayerSprite(Image imageComp, ColorData color)
        {
            if (!imageComp || !_gameManager) return;

            Sprite playerSprite = _gameManager.GetColorSprite(color);
            if (playerSprite)
            {
                imageComp.sprite = playerSprite;
            }
        }

        private void ResetVisualStates()
        {
            if (imgBackA) imgBackA.SetAlpha(1f);
            if (imgLightA)
            {
                imgLightA.SetAlpha(0f);
                imgLightA.gameObject.SetActive(false);
            }

            if (imgBackB) imgBackB.SetAlpha(1f);
            if (imgLightB)
            {
                imgLightB.SetAlpha(0f);
                imgLightB.gameObject.SetActive(false);
            }
        }

        public void ReceiveTrigger(int triggerInfo)
        {
            // 전체 전환 연출이 시작되었다면 어떠한 트리거도 차단
            if (_completionStarted) return; 

            if (triggerInfo == 1)
            {
                ActivatePlayerCheck(true);
            }
            else if (triggerInfo == 2)
            {
                ActivatePlayerCheck(false);
            }
        }

        private void Update()
        {
            if (_completionStarted) return; 

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
            // 연출이 진행 중이거나 켜진 상태라면 아예 다이얼 연산을 타지 않도록 2중 잠금
            if (!isLightOnA && !_isLightAChanging)
            {
                ProcessDialInput(ref _p1State, 1, 4, true);
            }

            if (!isLightOnB && !_isLightBChanging)
            {
                ProcessDialInput(ref _p2State, 5, 8, false);
            }
        }
        
        private void ProcessDialInput(ref PlayerWheelState state, int startKey, int endKey, bool isPlayerA)
        {
            int currentKey = WheelInputUtility.GetPressedKeyIndex(startKey, endKey);
            if (currentKey == -1) return;

            int previousDir = state.lastDir;
            int currentDir = WheelInputUtility.ResolveDirection(currentKey, 4, ref state);

            if (currentDir != 0)
            {
                state.stepCount = (currentDir == previousDir) ? state.stepCount + 1 : 1;

                if (state.stepCount >= StepsForFullRotation)
                {
                    ActivatePlayerCheck(isPlayerA);
                }
            }
        }

        public void ActivatePlayerCheck(bool isPlayerA)
        {
            if (_completionStarted) return;
            
            ResetIdleState(false);

            if (!TryEnablePlayerLight(isPlayerA))
            {
                return;
            }

            ProcessCompletionIfReady();
        }

        private bool TryEnablePlayerLight(bool isPlayerA)
        {
            CancellationToken token = this.GetCancellationTokenOnDestroy();

            if (isPlayerA)
            {
                // 이미 불이 켜졌거나 상태가 바뀌는 중이라면 튕겨냄 (Thread-safe 효과)
                if (isLightOnA || _isLightAChanging) return false;
                _isLightAChanging = true;
                ShowCheckMarkAsync(imgBackA, imgLightA, true, token).Forget();
            }
            else
            {
                if (isLightOnB || _isLightBChanging) return false;
                _isLightBChanging = true;
                ShowCheckMarkAsync(imgBackB, imgLightB, false, token).Forget();
            }
            return true;
        }

        private void ProcessCompletionIfReady()
        {
            if (!isLightOnA || !isLightOnB || _completionStarted) return;

            _completionStarted = true;

            WaitAndCompleteAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }
        
        private async UniTaskVoid WaitAndCompleteAsync(CancellationToken token)
        {   
            try
            {
                if (_soundManager)
                {
                    _soundManager.PlaySFX("공통_3");
                }
                
                await UniTask.Delay(2000, ignoreTimeScale: true, cancellationToken: token);
                CompleteStep(); 
            }
            catch (OperationCanceledException)
            {
            }
        }
        
        private async UniTaskVoid ShowCheckMarkAsync(Image backImage, Image lightImage, bool isPlayerA, CancellationToken token)
        {
            try
            {
                if (!backImage || !lightImage) return;
                lightImage.gameObject.SetActive(true);
                
                if (_soundManager)
                {
                    _soundManager.PlaySFX("카메라_1");
                }
                await lightImage.FadeAsync(0f, 1f, 1.0f, token);
                
                // 애니메이션이 무사히 끝나면 플래그 완전 확정 및 연출 락 해제
                if (isPlayerA)
                {
                    isLightOnA = true;
                    _isLightAChanging = false;
                }
                else
                {
                    isLightOnB = true;
                    _isLightBChanging = false;
                }

                // 애니메이션 완료 후 전체 체크 스캔 한 번 더 수행 (트리거 순서 꼬임 보완)
                ProcessCompletionIfReady();
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}