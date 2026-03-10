using System;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Pages;
using My.Scripts.Global; // GameManager 참조 추가
using Wonjeong.Data;
using Wonjeong.UI;

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

    public class TutorialPage3Controller : PopupGamePage<TutorialPage3Data>
    {
        [Header("Page 3 UI")]
        [SerializeField] private Text descriptionText; 
        [SerializeField] private Text nicknameA; 
        [SerializeField] private Text nicknameB; 

        private TutorialPage3Data _data; // 데이터를 보관해둡니다

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

        protected override void SetupData(TutorialPage3Data data)
        {
            _data = data;
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        public override void OnEnter()
        {
            base.OnEnter();

            // 화면이 켜질 때 최신 이름 데이터 적용
            if (SessionManager.Instance && _data != null)
            {
                if (nicknameA && _data.nicknamePlayerA != null)
                    nicknameA.text = _data.nicknamePlayerA.text.Replace("{nameA}", SessionManager.Instance.PlayerALastName).Replace("{nameB}", SessionManager.Instance.PlayerBLastName);
                if (nicknameB && _data.nicknamePlayerB != null)
                    nicknameB.text = _data.nicknamePlayerB.text.Replace("{nameA}", SessionManager.Instance.PlayerALastName).Replace("{nameB}", SessionManager.Instance.PlayerBLastName);
            }

            ResetIdleState(true);
            
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

        private void HandleWheelInput()
        {
            float now = Time.time;

            int p1Key = GetPressedKeyIndex(1, 4);
            if (p1Key != -1)
            {
                if (_lastP1Key != -1)
                {
                    int diff = (p1Key - _lastP1Key + 4) % 4;
                    int currentDir = 0;
                    
                    if (diff == 1) currentDir = 1;       
                    else if (diff == 3) currentDir = -1; 

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
                            CompleteStep(1);
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