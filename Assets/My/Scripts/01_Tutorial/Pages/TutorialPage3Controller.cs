using System;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Pages;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary> 튜토리얼 3페이지 데이터 클래스 </summary>
    [Serializable]
    public class TutorialPage3Data
    {
        public TextSetting descriptionText; // 설명 텍스트 설정
        public TextSetting nicknamePlayerA; // 플레이어 A 닉네임 설정
        public TextSetting nicknamePlayerB; // 플레이어 B 닉네임 설정
        
        public string warningMessage; // 1차 경고 메시지
        public string resetMessage;   // 2차 초기화 메시지
    }

    /// <summary> 튜토리얼 3페이지 컨트롤러 </summary>
    public class TutorialPage3Controller : PopupGamePage<TutorialPage3Data>
    {
        [Header("Page 3 UI")]
        [SerializeField] private Text descriptionText; 
        [SerializeField] private Text nicknameA; 
        [SerializeField] private Text nicknameB; 

        // 휠 입력 추적 변수
        private int _lastP1Key = -1;
        private int _p1StepCount = 0;
        private float _p1LastTime;
        private int _p1LastDir; // 1: CW, -1: CCW

        private int _lastP2Key = -1;
        private int _p2StepCount = 0;
        private float _p2LastTime;
        private int _p2LastDir;

        // 4분할 입력 기준: 4스텝을 1회전으로 판정
        private const int StepsForFullRotation = 4;
        private const float FastInputThreshold = 0.2f; // 빠른 입력 임계값

        protected override void SetupData(TutorialPage3Data data)
        {
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            ResetIdleState(true);
            
            // 상태 초기화
            _lastP1Key = -1; _p1StepCount = 0; _p1LastDir = 0; _p1LastTime = 0f;
            _lastP2Key = -1; _p2StepCount = 0; _p2LastDir = 0; _p2LastTime = 0f;
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

        /// <summary> 휠 회전 감지 및 완료 처리 </summary>
        private void HandleWheelInput()
        {
            float now = Time.time;

            // --- Player 1 (1~4) ---
            int p1Key = GetPressedKeyIndex(1, 4);
            if (p1Key != -1)
            {
                if (_lastP1Key != -1)
                {
                    int diff = (p1Key - _lastP1Key + 4) % 4;
                    int currentDir = 0;
                    
                    if (diff == 1) currentDir = 1;       // CW
                    else if (diff == 3) currentDir = -1; // CCW

                    // 빠른 입력 시 방향 역전이나 점프(2칸) 무시하고 이전 방향 유지
                    if (now - _p1LastTime < FastInputThreshold && _p1LastDir != 0)
                    {
                        if (diff == 2 || (currentDir != 0 && currentDir != _p1LastDir))
                        {
                            currentDir = _p1LastDir;
                        }
                    }

                    if (currentDir != 0)
                    {
                        // 방향이 유지되면 카운트 증가, 바뀌면 리셋
                        if (currentDir == _p1LastDir) _p1StepCount++;
                        else _p1StepCount = 1;

                        _p1LastDir = currentDir;
                        _p1LastTime = now;

                        // 한 바퀴 완료 체크
                        if (_p1StepCount >= StepsForFullRotation)
                        {
                            SoundManager.Instance?.PlaySFX("카메라_1");
                            CompleteStep(1);
                            _p1StepCount = 0; // 중복 호출 방지
                        }
                    }
                }
                _lastP1Key = p1Key;
            }

            // --- Player 2 (5~8) ---
            int p2Key = GetPressedKeyIndex(5, 8);
            if (p2Key != -1)
            {
                if (_lastP2Key != -1)
                {
                    int currIdx = p2Key - 5;
                    int lastIdx = _lastP2Key - 5;
                    int diff = (currIdx - lastIdx + 4) % 4;
                    
                    int currentDir = 0;
                    if (diff == 1) currentDir = 1;       // CW
                    else if (diff == 3) currentDir = -1; // CCW

                    // [관성 보정]
                    if (now - _p2LastTime < FastInputThreshold && _p2LastDir != 0)
                    {
                        if (diff == 2 || (currentDir != 0 && currentDir != _p2LastDir))
                        {
                            currentDir = _p2LastDir;
                        }
                    }

                    if (currentDir != 0)
                    {
                        if (currentDir == _p2LastDir) _p2StepCount++;
                        else _p2StepCount = 1;

                        _p2LastDir = currentDir;
                        _p2LastTime = now;

                        if (_p2StepCount >= StepsForFullRotation)
                        {   
                            SoundManager.Instance?.PlaySFX("카메라_1");
                            CompleteStep(2);
                            _p2StepCount = 0;
                        }
                    }
                }
                _lastP2Key = p2Key;
            }
        }

        /// <summary> 키 입력 헬퍼 </summary>
        private int GetPressedKeyIndex(int start, int end)
        {
            for (int i = start; i <= end; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + i))) return i;
            }
            return -1;
        }
    }
}