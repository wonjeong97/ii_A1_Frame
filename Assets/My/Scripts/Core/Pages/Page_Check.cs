using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using My.Scripts.Hardware;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary> 
    /// 양측 플레이어의 하드웨어 입력(준비 상태)을 대기하고 점등 연출을 수행하는 페이지 컨트롤러.
    /// 두 플레이어의 조명이 모두 켜져야 다음 시퀀스로 넘어갑니다. 
    /// </summary>
    public class Page_Check : PopupGamePage<CheckPageData>, ITriggerReceiver
    {
        [Header("UI References")] 
        [SerializeField] private Text nicknameA; 
        [SerializeField] private Text nicknameB; 
        [SerializeField] private Text waitText;  

        [Header("Check Images")] 
        [SerializeField] private CanvasGroup cgLightA; 
        [SerializeField] private Image imgLightA; 
        [SerializeField] private CanvasGroup cgLightB; 
        [SerializeField] private Image imgLightB; 

        private bool isLightOnA; 
        private bool isLightOnB; 
        private bool _completionStarted; 
        private float _enterTime; 

        /// <summary> JSON 설정 데이터 주입. 런타임 오류 방지를 위해 값이 널(null)일 경우 Fallback 대신 명시적 경고 로그 출력 </summary>
        protected override void SetupData(CheckPageData data)
        {
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            else Debug.LogWarning("[Page_Check] nicknameA가 할당되지 않았습니다.");

            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);
            else Debug.LogWarning("[Page_Check] nicknameB가 할당되지 않았습니다.");

            if (waitText) UIManager.Instance.SetText(waitText.gameObject, data.waitText);
            else Debug.LogWarning("[Page_Check] waitText가 할당되지 않았습니다.");

            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        /// <summary> 페이지 진입 시 이전 플레이의 잔상(조명 켜짐 등)을 제거하고 상태 초기화 </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            StopAllCoroutines();
            
            isLightOnA = false;
            isLightOnB = false;
            _completionStarted = false;
            _enterTime = Time.time; 

            ResetIdleState(true);

            if (cgLightA) 
            {
                cgLightA.alpha = 0f;
                cgLightA.gameObject.SetActive(false);
            }
            
            if (cgLightB) 
            {
                cgLightB.alpha = 0f;
                cgLightB.gameObject.SetActive(false);
            }
            
            if (SessionManager.Instance && GameManager.Instance)
            {
                Sprite spriteA = GameManager.Instance.GetColorSprite(SessionManager.Instance.PlayerAColor);
                if (imgLightA) imgLightA.sprite = spriteA;

                Sprite spriteB = GameManager.Instance.GetColorSprite(SessionManager.Instance.PlayerBColor);
                if (imgLightB) imgLightB.sprite = spriteB;
            }
        }

        /// <summary> 프레임 단위 무응답 타임아웃 갱신 및 키보드 예외 입력 감지 </summary>
        private void Update()
        {
            if (_completionStarted) return; 

            bool inputDetected = ProcessCommonKeyboardInput();

            if (inputDetected || Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false); 
            }
            else
            {
                UpdateInactivity();
            }
        }
        
        /// <summary> ITriggerReceiver 구현부: 이전 페이지(QnA 등)의 선택 결과를 바탕으로 조명 즉시 활성화 </summary>
        public void ReceiveTrigger(int triggerInfo)
        {
            if (triggerInfo == 1) ActivatePlayerCheck(true);
            else if (triggerInfo == 2) ActivatePlayerCheck(false);
        }

        /// <summary> 하드웨어(아두이노)에서 전달된 물리 버튼 문자열 파싱 및 선택값 추출 </summary>
        protected override void OnHardwareInput(string input, bool isLeft)
        {
            if (_completionStarted) return;

            int selectedValue = 0;
            string side = isLeft ? "left" : "right";

            // C# 컴파일러가 제공하는 해시 기반 switch문 최적화를 통해 다중 문자열 비교 오버헤드와 가비지 할당을 차단
            switch (input)
            {
                case GameConstants.Hardware.Input1On: selectedValue = 1; break;
                case GameConstants.Hardware.Input2On: selectedValue = 2; break;
                case GameConstants.Hardware.Input3On: selectedValue = 3; break;
                case GameConstants.Hardware.Input4On: selectedValue = 4; break;
                case GameConstants.Hardware.Input5On: selectedValue = 5; break;
            }

            if (selectedValue != 0)
            {
                ProcessInput(selectedValue, side);
            }
        }

        /// <summary> 추출된 입력값에 따라 아두이노 LED를 끄고, 서버 API로 선택 데이터 전송 </summary>
        private void ProcessInput(int selectedValue, string side)
        {
            bool isPlayerA = side.Equals("left");

            if ((isPlayerA && !isLightOnA) || (!isPlayerA && !isLightOnB))
            {
                if (ArduinoManager.Instance)
                {
                    if (isPlayerA) ArduinoManager.Instance.SendCommandToLeft(GameConstants.Hardware.CmdLedAllOff);
                    else ArduinoManager.Instance.SendCommandToRight(GameConstants.Hardware.CmdLedAllOff);
                }

                if (GameManager.Instance && LevelManager.Instance)
                {
                    int qNo = LevelManager.Instance.CurrentQuestionNumber;
                    if (qNo > 0)
                    {
                        GameManager.Instance.SendValueUpdateAPI(qNo, side, selectedValue);
                    }
                }
            }

            ActivatePlayerCheck(isPlayerA);
        }

        /// <summary> 플레이어별 조명 연출 시작. 화면 전환이 덜 끝났을 때 성급하게 켜지는 것을 막기 위해 진입 시간 기반 대기 보장 </summary>
        public void ActivatePlayerCheck(bool isPlayerA)
        {
            ResetIdleState(false);

            float delay = Mathf.Max(0f, 0.5f - (Time.time - _enterTime));

            if (isPlayerA)
            {
                if (isLightOnA) return;
                isLightOnA = true;
                StartCoroutine(LightOnRoutine(cgLightA, delay));
            }
            else
            {
                if (isLightOnB) return;
                isLightOnB = true;
                StartCoroutine(LightOnRoutine(cgLightB, delay));
            }
        }

        /// <summary> 양쪽 플레이어의 조명이 모두 켜졌는지 검사하여 다음 단계(완료) 트리거 </summary>
        private void CheckCompletion()
        {   
            if (isLightOnA && isLightOnB && !_completionStarted)
            {
                _completionStarted = true;
                StartCoroutine(CompleteRoutine());
            }
        }

        /// <summary> 완료 사운드 재생 후 연출 여운을 주기 위한 1초 대기 코루틴 </summary>
        private IEnumerator CompleteRoutine()
        {   
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_22");
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            CompleteStep();
        }

        /// <summary> CanvasGroup의 투명도(Alpha)를 선형 보간하여 부드러운 점등 효과 연출 </summary>
        private IEnumerator LightOnRoutine(CanvasGroup cg, float delay)
        {
            if (!cg) yield break;
            
            if (delay > 0f) yield return CoroutineData.GetWaitForSeconds(delay);

            cg.gameObject.SetActive(true);
            cg.alpha = 0f;

            float t = 0f;
            float duration = 1.0f; 

            while (t < duration)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(0f, 1f, t / duration);
                yield return null;
            }

            cg.alpha = 1f;
            
            CheckCompletion();
        }
    }
}