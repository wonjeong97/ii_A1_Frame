using System;
using System.Collections;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using My.Scripts.Core.Pages;
using My.Scripts.Global;
using My.Scripts.Utils;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary>
    /// 튜토리얼 4페이지용 데이터 구조체.
    /// </summary>
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
        
        private const int StepsForFullRotation = 3; 
        private const float FastInputThreshold = 0.2f; 
        
        private PlayerWheelState _p1State;
        private PlayerWheelState _p2State;

        // 매번 새로운 문자열 객체를 생성하여 발생하는 GC 부하를 억제하기 위한 정적 버퍼.
        private readonly static StringBuilder StringBuilder = new StringBuilder(128);

        protected override void SetupData(TutorialPage4Data data)
        {
            _data = data;
            if (nicknameA)
            {
                UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            }
            if (nicknameB)
            {
                UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);
            }
            SetupPopupMessage(data.warningMessage, data.resetMessage);
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
            
            SetupPlayerInfo();
            ResetVisualStates();

            isLightOnA = false;
            isLightOnB = false;
            _completionStarted = false;
            
            _p1State = PlayerWheelState.Default;
            _p2State = PlayerWheelState.Default;
            
            ResetIdleState(true); 
            
            if (SoundManager.Instance)
            {
                SoundManager.Instance.PlaySFX("공통_1");
            }
        }
        
        /// <summary>
        /// 세션 데이터를 기반으로 닉네임과 캐릭터 색상을 UI에 동기화함.
        /// </summary>
        private void SetupPlayerInfo()
        {
            if (!SessionManager.Instance) return;
            if (_data == null) return;

            string nameA = SessionManager.Instance.PlayerAFirstName;
            string nameB = SessionManager.Instance.PlayerBFirstName;

            ApplyPlayerNickname(nicknameA, _data.nicknamePlayerA, nameA, nameB);
            ApplyPlayerNickname(nicknameB, _data.nicknamePlayerB, nameA, nameB);

            if (GameManager.Instance)
            {
                ApplyPlayerSprite(imgBackA, SessionManager.Instance.PlayerAColor);
                ApplyPlayerSprite(imgBackB, SessionManager.Instance.PlayerBColor);
            }
        }

        /// <summary> 텍스트 내 태그를 이름으로 치환함. </summary>
        private void ApplyPlayerNickname(Text textComp, TextSetting setting, string nameA, string nameB)
        {
            if (!textComp) return;
            if (setting == null || string.IsNullOrEmpty(setting.text)) return;

            StringBuilder.Clear();
            StringBuilder.Append(setting.text);
            StringBuilder.Replace("{nameA}", nameA);
            StringBuilder.Replace("{nameB}", nameB);

            textComp.text = StringBuilder.ToString();
        }

        private void ApplyPlayerSprite(Image imageComp, ColorData color)
        {
            if (!imageComp) return;
            if (!GameManager.Instance) return;

            Sprite playerSprite = GameManager.Instance.GetColorSprite(color);
            if (playerSprite)
            {
                imageComp.sprite = playerSprite;
            }
        }

        private void ResetVisualStates()
        {
            SetImageAlpha(imgBackA, 1f);
            SetImageAlpha(imgLightA, 0f);
            if (imgLightA)
            {
                imgLightA.gameObject.SetActive(false);
            }

            SetImageAlpha(imgBackB, 1f);
            SetImageAlpha(imgLightB, 0f);
            if (imgLightB)
            {
                imgLightB.gameObject.SetActive(false);
            }
        }

        public void ReceiveTrigger(int triggerInfo)
        {
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
            if (!isLightOnA)
            {
                ProcessDialInput(ref _p1State, 1, 4, true);
            }

            if (!isLightOnB)
            {
                ProcessDialInput(ref _p2State, 5, 8, false);
            }
        }
        
        /// <summary>
        /// 다이얼의 물리적 회전 신호를 읽어 목표 회전수에 도달했는지 확인.
        /// </summary>
        private void ProcessDialInput(ref PlayerWheelState state, int startKey, int endKey, bool isPlayerA)
        {
            int currentKey = WheelInputUtility.GetPressedKeyIndex(startKey, endKey);
            if (currentKey == -1) return;

            if (state.lastKey == -1)
            {
                state.lastKey = currentKey;
                return;
            }

            float now = Time.time;
            int diff = (currentKey - state.lastKey + 4) % 4;
            int currentDir = WheelInputUtility.ResolveDirection(diff, now, ref state);

            if (currentDir != 0)
            {
                state.stepCount = (currentDir == state.lastDir) ? state.stepCount + 1 : 1;
                state.lastDir = currentDir;
                state.lastTime = now;

                if (state.stepCount >= StepsForFullRotation)
                {
                    ActivatePlayerCheck(isPlayerA);
                }
            }

            state.lastKey = currentKey;
        }

        /// <summary> 특정 플레이어의 조작이 완료되었을 때 조명을 활성화하고, 전체 완료 여부를 판단함. </summary>
        public void ActivatePlayerCheck(bool isPlayerA)
        {
            ResetIdleState(false);

            // 이미 활성화된 상태라면 중복 연출을 방지하기 위해 로직을 중단함.
            if (!TryEnablePlayerLight(isPlayerA))
            {
                return;
            }

            // 모든 플레이어의 조건이 충족되었는지 확인 후 시퀀스를 진행함.
            ProcessCompletionIfReady();
        }

        /// <summary> 대상 플레이어의 라이트 상태를 활성화로 전환하고 페이드인 애니메이션을 시작함. </summary>
        private bool TryEnablePlayerLight(bool isPlayerA)
        {
            CancellationToken token = this.GetCancellationTokenOnDestroy();

            if (isPlayerA)
            {
                if (isLightOnA) return false;
                isLightOnA = true;
                ShowCheckMarkAsync(imgBackA, imgLightA, token).Forget();
            }
            else
            {
                if (isLightOnB) return false;
                isLightOnB = true;
                ShowCheckMarkAsync(imgBackB, imgLightB, token).Forget();
            }
            return true;
        }

        /// <summary> 모든 플레이어의 준비가 끝났는지 검사하고 최종 완료 시퀀스를 트리거함. </summary>
        private void ProcessCompletionIfReady()
        {
            if (!isLightOnA || !isLightOnB || _completionStarted) return;

            _completionStarted = true;

            if (SoundManager.Instance)
            {
                SoundManager.Instance.PlaySFX("카메라_1");
            }

            WaitAndCompleteAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }
        
        /// <summary> 일정 시간 대기 후 완료 신호를 전송함. </summary>
        private async UniTaskVoid WaitAndCompleteAsync(CancellationToken token)
        {   
            if (SoundManager.Instance)
            {
                SoundManager.Instance.PlaySFX("공통_3");
            }
            
            await UniTask.Delay(TimeSpan.FromSeconds(2.0), cancellationToken: token);
            CompleteStep(); 
        }
        
        /// <summary> 조명 이미지를 비동기로 페이드인 함. </summary>
        private async UniTaskVoid ShowCheckMarkAsync(Image backImage, Image lightImage, CancellationToken token)
        {
            if (!backImage || !lightImage) return;
            lightImage.gameObject.SetActive(true);
            await UIFadeUtility.FadeGraphicAsync(lightImage, 0f, 1f, 1.0f, token);
        }

        private void SetImageAlpha(Image img, float alpha)
        {
            if (!img) return;
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }
}