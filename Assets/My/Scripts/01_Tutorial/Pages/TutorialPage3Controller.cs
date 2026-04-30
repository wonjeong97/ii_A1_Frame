using System;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Pages;
using My.Scripts.Global;
using My.Scripts._01_Tutorial;
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
    /// 튜토리얼 3페이지 컨트롤러.
    /// 물리 다이얼(휠)의 회전 조작을 감지하여 특정 횟수 이상 돌리면 다음 단계로 전환합니다.
    /// </summary>
    public class TutorialPage3Controller : PopupGamePage<TutorialPage3Data>
    {
        [Header("Page 3 UI")]
        [SerializeField] private Text descriptionText; 
        [SerializeField] private Text nicknameA; 
        [SerializeField] private Text nicknameB; 

        private TutorialPage3Data _data; 

        private int _lastP1Key = -1;
        private int _p1StepCount = 0;
        private float _p1LastTime;
        private int _p1LastDir; 

        private int _lastP2Key = -1;
        private int _p2StepCount = 0;
        private float _p2LastTime;
        private int _p2LastDir;

        private const int StepsForFullRotation = 4;
        private const float FastInputThreshold = 0.2f; 

        /// <summary> JSON에서 로드한 UI 텍스트 및 경고 팝업 데이터 주입 </summary>
        protected override void SetupData(TutorialPage3Data data)
        {
            _data = data;
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        public override object ExtractCurrentData()
        {
            return new TutorialPage3Data
            {
                descriptionText = TutorialPageUtils.BuildTextSetting(descriptionText, _data?.descriptionText),
                // 닉네임 텍스트는 런타임에 실제 이름으로 치환되므로 템플릿 문자열을 보존
                nicknamePlayerA = TutorialPageUtils.BuildTextSetting(nicknameA, _data?.nicknamePlayerA, _data?.nicknamePlayerA?.text),
                nicknamePlayerB = TutorialPageUtils.BuildTextSetting(nicknameB, _data?.nicknamePlayerB, _data?.nicknamePlayerB?.text),
                warningMessage  = _data?.warningMessage ?? string.Empty,
                resetMessage    = _data?.resetMessage   ?? string.Empty,
            };
        }

        /// <summary> 페이지 진입 시 실시간 이름 텍스트 할당 및 센서 입력 변수 초기화 </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            // 세션 데이터를 바탕으로 화면의 닉네임 플레이스홀더를 동적 교체
            if (SessionManager.Instance && _data != null)
            {
                if (nicknameA && _data.nicknamePlayerA != null)
                    nicknameA.text = _data.nicknamePlayerA.text.Replace("{nameA}", SessionManager.Instance.PlayerAFirstName).Replace("{nameB}", SessionManager.Instance.PlayerBFirstName);
                if (nicknameB && _data.nicknamePlayerB != null)
                    nicknameB.text = _data.nicknamePlayerB.text.Replace("{nameA}", SessionManager.Instance.PlayerAFirstName).Replace("{nameB}", SessionManager.Instance.PlayerBFirstName);
            }

            ResetIdleState(true);
            
            _lastP1Key = -1; _p1StepCount = 0; _p1LastDir = 0; _p1LastTime = 0f;
            _lastP2Key = -1; _p2StepCount = 0; _p2LastDir = 0; _p2LastTime = 0f;
        }

        /// <summary> 매 프레임 휠 조작 감지 및 유저 무응답 타임아웃 갱신 </summary>
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
        /// 4개의 접점 센서(1~4, 5~8번 키) 입력을 통해 다이얼의 회전 방향과 누적 스텝을 계산합니다.
        /// 목표 스텝(StepsForFullRotation) 도달 시 플레이어별로 완료 처리합니다.
        /// </summary>
        private void HandleWheelInput()
        {
            float now = Time.time;

            int p1Key = GetPressedKeyIndex(1, 4);
            if (p1Key != -1)
            {
                if (_lastP1Key != -1)
                {
                    // 이전 키와 현재 키의 인덱스 차이로 시계/반시계 방향 판별
                    int diff = (p1Key - _lastP1Key + 4) % 4;
                    int currentDir = 0;
                    
                    if (diff == 1) currentDir = 1;       
                    else if (diff == 3) currentDir = -1; 

                    // 너무 빠른 입력(바운싱 등)에 대한 관성 보정 필터링
                    if (now - _p1LastTime < FastInputThreshold && _p1LastDir != 0)
                    {
                        if (diff == 2 || (currentDir != 0 && currentDir != _p1LastDir))
                        {
                            currentDir = _p1LastDir;
                        }
                    }

                    if (currentDir != 0)
                    {
                        if (currentDir == _p1LastDir) _p1StepCount++;
                        else _p1StepCount = 1;

                        _p1LastDir = currentDir;
                        _p1LastTime = now;

                        if (_p1StepCount >= StepsForFullRotation)
                        {
                            SoundManager.Instance?.PlaySFX("카메라_1");
                            CompleteStep(1); // Player A 조작 완료 신호
                            _p1StepCount = 0; 
                        }
                    }
                }
                _lastP1Key = p1Key;
            }

            int p2Key = GetPressedKeyIndex(5, 8);
            if (p2Key != -1)
            {
                if (_lastP2Key != -1)
                {
                    int currIdx = p2Key - 5;
                    int lastIdx = _lastP2Key - 5;
                    int diff = (currIdx - lastIdx + 4) % 4;
                    
                    int currentDir = 0;
                    if (diff == 1) currentDir = 1;       
                    else if (diff == 3) currentDir = -1; 

                    // 관성 보정 (바운스 현상 필터링)
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
                            CompleteStep(2); // Player B 조작 완료 신호
                            _p2StepCount = 0;
                        }
                    }
                }
                _lastP2Key = p2Key;
            }
        }

        /// <summary> 지정된 범위(start~end)의 숫자 키 입력을 감지하여 반환하는 헬퍼 함수 </summary>
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