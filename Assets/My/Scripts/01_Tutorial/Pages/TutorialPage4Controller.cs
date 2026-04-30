using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using My.Scripts.Core.Pages;
using My.Scripts.Global;
using My.Scripts._01_Tutorial;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary> 튜토리얼 4페이지용 데이터 구조체 </summary>
    [Serializable]
    public class TutorialPage4Data
    {
        public TextSetting nicknamePlayerA; 
        public TextSetting nicknamePlayerB; 
        
        public string warningMessage; 
        public string resetMessage;   
    }

    /// <summary>
    /// 튜토리얼 4페이지 컨트롤러.
    /// 각 플레이어가 다이얼을 일정 횟수 이상 돌려 자신의 색상(또는 준비 상태) 조명을 활성화하는 과정을 처리합니다.
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

        /// <summary> JSON에서 로드한 UI 텍스트 및 경고 팝업 데이터 주입 </summary>
        protected override void SetupData(TutorialPage4Data data)
        {
            _data = data;
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);

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

        /// <summary> 페이지 진입 시 실시간 이름 할당, 선택된 컬러 적용 및 입력 상태 초기화 </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            if (SessionManager.Instance && _data != null)
            {
                if (nicknameA && _data.nicknamePlayerA != null)
                    nicknameA.text = _data.nicknamePlayerA.text.Replace("{nameA}", SessionManager.Instance.PlayerAFirstName).Replace("{nameB}", SessionManager.Instance.PlayerBFirstName);
                if (nicknameB && _data.nicknamePlayerB != null)
                    nicknameB.text = _data.nicknamePlayerB.text.Replace("{nameA}", SessionManager.Instance.PlayerAFirstName).Replace("{nameB}", SessionManager.Instance.PlayerBFirstName);
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

        /// <summary> ITriggerReceiver 구현부: 이전 페이지에서 넘어온 트리거 정보로 조명을 켭니다. </summary>
        public void ReceiveTrigger(int triggerInfo)
        {
            if (triggerInfo == 1) ActivatePlayerCheck(true);
            else if (triggerInfo == 2) ActivatePlayerCheck(false);
        }

        /// <summary> 매 프레임 휠 조작 감지 및 유저 무응답 타임아웃 갱신 </summary>
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

        /// <summary> 
        /// 4개의 접점 센서(하드웨어 키보드 1~4, 5~8번 입력)를 통해 다이얼의 회전 방향과 누적 스텝을 계산합니다.
        /// 목표 스텝 도달 시 해당 플레이어의 조명을 활성화합니다.
        /// </summary>
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

        /// <summary> 지정된 범위(start~end)의 숫자 키 입력을 감지하여 반환하는 헬퍼 함수 </summary>
        private int GetPressedKeyIndex(int start, int end)
        {
            for (int i = start; i <= end; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + i))) return i;
            }
            return -1;
        }

        /// <summary> 특정 플레이어의 조작이 완료되었을 때 조명을 켜고, 양쪽 모두 켜지면 완료 시퀀스를 시작합니다. </summary>
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

        /// <summary> 양쪽 모두 완료된 후 연출 여운을 주기 위한 2초 대기 코루틴 </summary>
        private IEnumerator WaitAndComplete()
        {   
            SoundManager.Instance?.PlaySFX("공통_3");
            yield return CoroutineData.GetWaitForSeconds(2f);
            CompleteStep(); 
        }

        /// <summary> 조명(Check) 이미지를 부드럽게 페이드인(Fade-in)하는 연출 </summary>
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

        /// <summary> 이미지의 투명도 즉시 설정 </summary>
        private void SetImageAlpha(Image img, float alpha)
        {
            if (!img) return;
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }
}