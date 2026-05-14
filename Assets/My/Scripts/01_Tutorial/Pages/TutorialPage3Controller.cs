using System;
using System.Text;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Global;
using My.Scripts.Utils;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary> 튜토리얼 3페이지용 데이터 구조체 </summary>
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
        private const float FastInputThreshold = 0.2f; 

        private PlayerWheelState _p1State;
        private PlayerWheelState _p2State;

        // 문자열 치환 시 발생하는 메모리 할당을 줄이기 위한 정적 버퍼.
        private readonly static StringBuilder StringBuilder = new StringBuilder(128);

        protected override void SetupData(TutorialPage3Data data)
        {
            _data = data;
            if (descriptionText)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            }
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
            return new TutorialPage3Data
            {
                descriptionText = TutorialPageUtils.BuildTextSetting(descriptionText, _data?.descriptionText),
                nicknamePlayerA = TutorialPageUtils.BuildTextSetting(nicknameA, _data?.nicknamePlayerA, _data?.nicknamePlayerA?.text),
                nicknamePlayerB = TutorialPageUtils.BuildTextSetting(nicknameB, _data?.nicknamePlayerB, _data?.nicknamePlayerB?.text),
                warningMessage  = _data?.warningMessage ?? string.Empty,
                resetMessage    = _data?.resetMessage   ?? string.Empty,
            };
        }

        /// <summary>
        /// 활성화 시 상태 초기화 수행.
        /// 잔여 입력으로 인한 오작동을 막기 위함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            SetupPlayerInfo();
            ResetIdleState(true);
            
            _p1State = PlayerWheelState.Default;
            _p2State = PlayerWheelState.Default;
        }

        /// <summary>
        /// 세션 정보를 바탕으로 플레이어들의 닉네임을 UI에 반영함.
        /// </summary>
        private void SetupPlayerInfo()
        {
            if (!SessionManager.Instance || _data == null) return;

            string nameA = SessionManager.Instance.PlayerAFirstName;
            string nameB = SessionManager.Instance.PlayerBFirstName;

            ApplyPlayerNickname(nicknameA, _data.nicknamePlayerA, nameA, nameB);
            ApplyPlayerNickname(nicknameB, _data.nicknamePlayerB, nameA, nameB);
        }

        /// <summary>
        /// StringBuilder를 사용하여 가비지 생성 없이 텍스트 내 태그를 치환함.
        /// </summary>
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

        private void Update()
        {
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

        /// <summary>
        /// 다이얼 입력을 감지하고 유틸리티를 통해 방향을 판별함.
        /// 중복 코드 제거를 위해 WheelInputUtility 호출.
        /// </summary>
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

        /// <summary>
        /// 개별 플레이어의 입력 상태를 갱신하고 임계치 도달 여부 확인.
        /// 방향 보정 로직을 공통화하기 위함.
        /// </summary>
        private void ProcessPlayerInput(ref PlayerWheelState state, int currentKey, int playerNumber)
        {
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
                    if (SoundManager.Instance)
                    {
                        SoundManager.Instance.PlaySFX("카메라_1");
                    }
                    CompleteStep(playerNumber);
                    state.stepCount = 0;
                }
            }

            state.lastKey = currentKey;
        }
    }
}