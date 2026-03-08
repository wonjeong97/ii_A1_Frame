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
    /// <summary> 플레이어 준비 확인 및 점등 연출 페이지 </summary>
    public class Page_Check : PopupGamePage<CheckPageData>
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

        /// <summary>
        /// 전달받은 데이터로 UI 텍스트 및 팝업 메시지를 설정.
        /// </summary>
        /// <param name="data">설정할 페이지 데이터</param>
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

        /// <summary>
        /// 페이지 진입 시 초기화 및 이벤트 구독 수행.
        /// </summary>
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
            else Debug.LogWarning("[Page_Check] cgLightA가 할당되지 않았습니다.");
            
            if (cgLightB) 
            {
                cgLightB.alpha = 0f;
                cgLightB.gameObject.SetActive(false);
            }
            else Debug.LogWarning("[Page_Check] cgLightB가 할당되지 않았습니다.");
            
            if (GameManager.Instance)
            {
                // GetComponent 탐색 비용 최소화를 위해 직접 할당받은 Image 참조 사용
                Sprite spriteA = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerAColor);
                if (imgLightA) imgLightA.sprite = spriteA;
                else Debug.LogWarning("[Page_Check] imgLightA가 할당되지 않았습니다.");

                Sprite spriteB = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerBColor);
                if (imgLightB) imgLightB.sprite = spriteB;
                else Debug.LogWarning("[Page_Check] imgLightB가 할당되지 않았습니다.");
            }

            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.OnHardwareInput -= HandleArduinoInput;
                ArduinoManager.Instance.OnHardwareInput += HandleArduinoInput;
            }
        }

        /// <summary>
        /// 페이지 퇴장 시 하드웨어 입력 이벤트 구독 해제.
        /// </summary>
        public override void OnExit()
        {
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.OnHardwareInput -= HandleArduinoInput;
            }
            base.OnExit();
        }

        /// <summary>
        /// 오브젝트 파괴 시 하드웨어 입력 이벤트 구독 해제 (메모리 누수 방지).
        /// </summary>
        private void OnDestroy()
        {
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.OnHardwareInput -= HandleArduinoInput;
            }
        }

        /// <summary>
        /// 키보드 입력 감지 및 무응답 타임아웃 갱신.
        /// </summary>
        private void Update()
        {
            if (_completionStarted) return; 

            bool inputDetected = false;
            int selectedValue = 0;
            string side = string.Empty;

            // 디버그 키맵 검사
            if (Input.GetKeyDown(KeyCode.Q)) { selectedValue = 1; side = "left"; }
            else if (Input.GetKeyDown(KeyCode.W)) { selectedValue = 2; side = "left"; }
            else if (Input.GetKeyDown(KeyCode.E)) { selectedValue = 3; side = "left"; }
            else if (Input.GetKeyDown(KeyCode.R)) { selectedValue = 4; side = "left"; }
            else if (Input.GetKeyDown(KeyCode.T)) { selectedValue = 5; side = "left"; }
            else if (Input.GetKeyDown(KeyCode.Y)) { selectedValue = 1; side = "right"; }
            else if (Input.GetKeyDown(KeyCode.U)) { selectedValue = 2; side = "right"; }
            else if (Input.GetKeyDown(KeyCode.I)) { selectedValue = 3; side = "right"; }
            else if (Input.GetKeyDown(KeyCode.O)) { selectedValue = 4; side = "right"; }
            else if (Input.GetKeyDown(KeyCode.P)) { selectedValue = 5; side = "right"; }

            if (selectedValue != 0)
            {
                inputDetected = true;
                ProcessInput(selectedValue, side);
            }

            if (inputDetected || Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false); 
            }
            else
            {
                UpdateInactivity();
            }
        }

        /// <summary>
        /// 아두이노 입력 이벤트 처리.
        /// </summary>
        /// <param name="input">입력 문자열</param>
        /// <param name="isLeft">좌측 아두이노 여부</param>
        private void HandleArduinoInput(string input, bool isLeft)
        {
            if (_completionStarted) return;

            int selectedValue = 0;
            string side = isLeft ? "left" : "right";

            if (input.Equals("1On")) selectedValue = 1;
            else if (input.Equals("2On")) selectedValue = 2;
            else if (input.Equals("3On")) selectedValue = 3;
            else if (input.Equals("4On")) selectedValue = 4;
            else if (input.Equals("5On")) selectedValue = 5;

            if (selectedValue != 0)
            {
                ProcessInput(selectedValue, side);
            }
        }

        /// <summary>
        /// 입력된 값을 바탕으로 API 전송 및 점등 연출 트리거.
        /// </summary>
        /// <param name="selectedValue">선택된 버튼 값</param>
        /// <param name="side">입력된 측면 ("left" 또는 "right")</param>
        private void ProcessInput(int selectedValue, string side)
        {
            bool isPlayerA = side.Equals("left");

            // 중복 입력(이미 불이 켜진 상태)이 아닐 때만 API 전송 및 LED 제어
            if ((isPlayerA && !isLightOnA) || (!isPlayerA && !isLightOnB))
            {
                if (ArduinoManager.Instance)
                {
                    if (isPlayerA) ArduinoManager.Instance.SendCommandToLeft("LEDAllOff");
                    else ArduinoManager.Instance.SendCommandToRight("LEDAllOff");
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

        /// <summary>
        /// 특정 플레이어의 확인 상태 활성화 및 점등 코루틴 시작.
        /// </summary>
        /// <param name="isPlayerA">플레이어 A 여부</param>
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

        /// <summary>
        /// 두 플레이어 모두 점등되었는지 확인 후 완료 시퀀스 진행.
        /// </summary>
        private void CheckCompletion()
        {   
            if (isLightOnA && isLightOnB && !_completionStarted)
            {
                _completionStarted = true;
                StartCoroutine(CompleteRoutine());
            }
        }

        /// <summary>
        /// 완료 연출 (사운드 재생 및 대기 후 다음 단계).
        /// </summary>
        private IEnumerator CompleteRoutine()
        {   
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_22");
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            CompleteStep();
        }

        /// <summary>
        /// 캔버스 그룹 알파 페이드 인 점등 효과.
        /// </summary>
        /// <param name="cg">페이드 인 대상 캔버스 그룹</param>
        /// <param name="delay">실행 전 대기 시간 (초)</param>
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