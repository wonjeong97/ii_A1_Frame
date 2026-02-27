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
    /// <summary> 튜토리얼 4페이지 데이터 클래스 </summary>
    [Serializable]
    public class TutorialPage4Data
    {
        public TextSetting nicknamePlayerA; // 플레이어 A 닉네임 데이터
        public TextSetting nicknamePlayerB; // 플레이어 B 닉네임 데이터
        
        public string warningMessage; // 1차 경고 메시지
        public string resetMessage;   // 2차 초기화 메시지
    }

    /// <summary> 튜토리얼 4페이지 컨트롤러 </summary>
    public class TutorialPage4Controller : PopupGamePage<TutorialPage4Data>
    {
        [Header("Page 4 UI")]
        [SerializeField] private Text nicknameA; // 플레이어 A 닉네임 UI
        [SerializeField] private Text nicknameB; // 플레이어 B 닉네임 UI
        
        [Tooltip("체크 표시가 나타날 배경 이미지")]
        [SerializeField] private Image imgBackA; 
        [Tooltip("완료 시 나타날 체크(V) 이미지")]
        [SerializeField] private Image imgLightA; 
        
        [Tooltip("체크 표시가 나타날 배경 이미지")]
        [SerializeField] private Image imgBackB; 
        [Tooltip("완료 시 나타날 체크(V) 이미지")]
        [SerializeField] private Image imgLightB; 

        private bool isLightOnA; // 플레이어 A 완료 여부
        private bool isLightOnB; // 플레이어 B 완료 여부
        private bool _completionStarted; // 완료 시퀀스 진행 여부

        // 휠 입력 추적 변수 (관성 보정 포함)
        private int _lastP1Key = -1;
        private int _p1StepCount = 0;
        private float _p1LastTime;
        private int _p1LastDir; // 1: CW, -1: CCW

        private int _lastP2Key = -1;
        private int _p2StepCount = 0;
        private float _p2LastTime;
        private int _p2LastDir;
        
        // 360도 회전을 위해 3칸 이동 시 한 바퀴로 판정
        private const int StepsForFullRotation = 3; 
        private const float FastInputThreshold = 0.2f; // 빠른 입력 임계값

        /// <summary>  데이터 설정 </summary>
        protected override void SetupData(TutorialPage4Data data)
        {
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);

            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        /// <summary> 페이지 진입: 상태 초기화 </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            isLightOnA = false;
            isLightOnB = false;
            _completionStarted = false;
            
            // 휠 상태 초기화
            _lastP1Key = -1; _p1StepCount = 0; _p1LastDir = 0; _p1LastTime = 0f;
            _lastP2Key = -1; _p2StepCount = 0; _p2LastDir = 0; _p2LastTime = 0f;
            
            ResetIdleState(true); 
            
            // 이미지 초기화
            // 배경(Back)은 보이고(Alpha 1), 체크(Light)는 숨김(Alpha 0)
            SetImageAlpha(imgBackA, 1f);
            SetImageAlpha(imgLightA, 0f);
            if(imgLightA) imgLightA.gameObject.SetActive(false);

            SetImageAlpha(imgBackB, 1f);
            SetImageAlpha(imgLightB, 0f);
            if(imgLightB) imgLightB.gameObject.SetActive(false);
            
            if (GameManager.Instance)
            {
                Sprite spriteA = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerAColor);
                if (spriteA && imgBackA) imgBackA.sprite = spriteA;

                Sprite spriteB = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerBColor);
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

        /// <summary> 휠 회전 감지 및 점등 처리 (관성 보정 포함) </summary>
        private void HandleWheelInput()
        {
            float now = Time.time;

            // --- Player 1 (1~4) ---
            if (!isLightOnA) 
            {
                int p1Key = GetPressedKeyIndex(1, 4);
                if (p1Key != -1)
                {
                    if (_lastP1Key != -1)
                    {
                        int diff = (p1Key - _lastP1Key + 4) % 4;
                        int currentDir = 0;
                        
                        if (diff == 1) currentDir = 1;       // CW
                        else if (diff == 3) currentDir = -1; // CCW

                        // [관성 보정]
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

            // --- Player 2 (5~8) ---
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

        /// <summary> 플레이어 체크 활성화 (체크 표시 V 등장) </summary>
        public void ActivatePlayerCheck(bool isPlayerA)
        {
            ResetIdleState(false);

            if (isPlayerA)
            {
                if (isLightOnA) return; 
                isLightOnA = true;
                // 배경(Back)은 그대로 두고 체크(Light)만 페이드 인
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

        /// <summary> 대기 후 단계 완료 처리 </summary>
        private IEnumerator WaitAndComplete()
        {   
            SoundManager.Instance?.PlaySFX("공통_3");
            yield return CoroutineData.GetWaitForSeconds(2f);
            CompleteStep(); 
        }

        /// <summary> 
        /// 체크 표시(V) 페이드 인 연출 
        /// </summary>
        private IEnumerator ShowCheckMarkRoutine(Image backImage, Image lightImage)
        {
            if (!backImage || !lightImage) yield break;

            float timer = 0f;
            float duration = 1.0f; // 1초 동안 페이드 인
            
            Color lightColor = lightImage.color;
            lightColor.a = 0f;
            lightImage.color = lightColor;
            lightImage.gameObject.SetActive(true);

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float progress = timer / duration;
                
                // Light(체크 V)만 알파값 0 -> 1 증가
                lightColor.a = Mathf.Lerp(0f, 1f, progress);
                lightImage.color = lightColor;
                
                yield return null;
            }
            
            // 최종값 보정
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