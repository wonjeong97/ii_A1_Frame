using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Pages;
using My.Scripts.Global;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

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

    public class TutorialPage4Controller : PopupGamePage<TutorialPage4Data>
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

        private int _lastP1Key = -1;
        private int _p1StepCount = 0;
        private float _p1LastTime;
        private int _p1LastDir; 

        private int _lastP2Key = -1;
        private int _p2StepCount = 0;
        private float _p2LastTime;
        private int _p2LastDir;
        
        private const int StepsForFullRotation = 3; 
        private const float FastInputThreshold = 0.2f; 

        protected override void SetupData(TutorialPage4Data data)
        {
            _data = data;
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);

            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            // 화면이 켜질 때 이름 데이터 교체
            if (SessionManager.Instance && _data != null)
            {
                if (nicknameA && _data.nicknamePlayerA != null)
                    nicknameA.text = _data.nicknamePlayerA.text.Replace("{nameA}", SessionManager.Instance.PlayerALastName).Replace("{nameB}", SessionManager.Instance.PlayerBLastName);
                if (nicknameB && _data.nicknamePlayerB != null)
                    nicknameB.text = _data.nicknamePlayerB.text.Replace("{nameA}", SessionManager.Instance.PlayerALastName).Replace("{nameB}", SessionManager.Instance.PlayerBLastName);
            }

            isLightOnA = false;
            isLightOnB = false;
            _completionStarted = false;
            
            _lastP1Key = -1; _p1StepCount = 0; _p1LastDir = 0; _p1LastTime = 0f;
            _lastP2Key = -1; _p2StepCount = 0; _p2LastDir = 0; _p2LastTime = 0f;
            
            ResetIdleState(true); 
            
            SetImageAlpha(imgBackA, 1f);
            SetImageAlpha(imgLightA, 0f);
            if(imgLightA) imgLightA.gameObject.SetActive(false);

            SetImageAlpha(imgBackB, 1f);
            SetImageAlpha(imgLightB, 0f);
            if(imgLightB) imgLightB.gameObject.SetActive(false);
            
            if (GameManager.Instance && SessionManager.Instance)
            {
                Sprite spriteA = GameManager.Instance.GetColorSprite(SessionManager.Instance.PlayerAColor);
                if (spriteA && imgBackA) imgBackA.sprite = spriteA;
                Sprite spriteB = GameManager.Instance.GetColorSprite(SessionManager.Instance.PlayerBColor);
                if (spriteB && imgBackB) imgBackB.sprite = spriteB;
            }
            
            SoundManager.Instance?.PlaySFX("공통_1");
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
            float now = Time.time;

            if (!isLightOnA) 
            {
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
                                ActivatePlayerCheck(true);
                            }
                        }
                    }
                    _lastP1Key = p1Key;
                }
            }

            if (!isLightOnB)
            {
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
                                ActivatePlayerCheck(false);
                            }
                        }
                    }
                    _lastP2Key = p2Key;
                }
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

        public void ActivatePlayerCheck(bool isPlayerA)
        {
            ResetIdleState(false);

            if (isPlayerA)
            {
                if (isLightOnA) return; 
                isLightOnA = true;
                StartCoroutine(ShowCheckMarkRoutine(imgBackA, imgLightA));
            }
            else
            {
                if (isLightOnB) return;
                isLightOnB = true;
                StartCoroutine(ShowCheckMarkRoutine(imgBackB, imgLightB));
            }
            
            if (isLightOnA && isLightOnB)
            {
                if (!_completionStarted)
                {   
                    SoundManager.Instance?.PlaySFX("카메라_1");
                    _completionStarted = true;
                    StartCoroutine(WaitAndComplete());
                }
            }
        }

        private IEnumerator WaitAndComplete()
        {   
            SoundManager.Instance?.PlaySFX("공통_3");
            yield return CoroutineData.GetWaitForSeconds(2f);
            CompleteStep(); 
        }

        private IEnumerator ShowCheckMarkRoutine(Image backImage, Image lightImage)
        {
            if (!backImage || !lightImage) yield break;

            float timer = 0f;
            float duration = 1.0f; 
            
            Color lightColor = lightImage.color;
            lightColor.a = 0f;
            lightImage.color = lightColor;
            lightImage.gameObject.SetActive(true);

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float progress = timer / duration;
                
                lightColor.a = Mathf.Lerp(0f, 1f, progress);
                lightImage.color = lightColor;
                
                yield return null;
            }
            
            lightColor.a = 1f;
            lightImage.color = lightColor;
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