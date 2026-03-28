using System;
using System.Collections;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using My.Scripts.Hardware;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 양측 플레이어의 하드웨어 입력을 받아 질문에 대한 답변을 처리하고 API를 동기화하는 페이지.
    /// </summary>
    public class Page_QnA : PopupGamePage<QnAPageData>
    {
        [Header("UI References")] 
        [SerializeField] private Text descriptionText; 
        [SerializeField] private Text questionText; 
        [SerializeField] private Text[] answerTexts; 
        
        [Header("Check UI References")]
        [SerializeField] private Text nicknameA; 
        [SerializeField] private Text nicknameB; 
        [SerializeField] private CanvasGroup cgLightA; 
        [SerializeField] private Image imgLightA; 
        [SerializeField] private CanvasGroup cgLightB; 
        [SerializeField] private Image imgLightB; 

        [Header("Canvas Groups")] 
        [SerializeField] private CanvasGroup contentsGroup;

        private Coroutine _sequenceRoutine; 
        
        private bool _isAnsweredA;
        private bool _isAnsweredB;
        private bool _completionStarted; 
        private bool _isInputEnabled; 

        /// <summary>
        /// 외부 JSON 데이터의 텍스트를 UI에 바인딩하며, 누락 시 경고 로그를 출력함.
        /// </summary>
        /// <param name="data">초기화할 QnA 데이터 객체</param>
        protected override void SetupData(QnAPageData data)
        {
            if (data == null) return;

            if (data.descriptionText != null)
            {
                if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            }
            else Debug.LogWarning("descriptionText 누락됨.");

            if (data.questionText != null)
            {
                if (questionText) UIManager.Instance.SetText(questionText.gameObject, data.questionText);
            }
            else Debug.LogWarning("questionText 누락됨.");
            
            if (data.nicknamePlayerA != null && !string.IsNullOrEmpty(data.nicknamePlayerA.text))
            {
                if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            }
            else Debug.LogWarning("nicknamePlayerA 누락됨.");
            
            if (data.nicknamePlayerB != null && !string.IsNullOrEmpty(data.nicknamePlayerB.text))
            {
                if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);
            }
            else Debug.LogWarning("nicknamePlayerB 누락됨.");

            if (answerTexts != null)
            {
                if (data.answerTexts != null)
                {
                    for (int i = 0; i < answerTexts.Length; i++)
                    {
                        if (!answerTexts[i]) continue;
                        
                        if (i < data.answerTexts.Length && data.answerTexts[i] != null)
                        {
                            UIManager.Instance.SetText(answerTexts[i].gameObject, data.answerTexts[i]);
                            answerTexts[i].gameObject.SetActive(true);
                        }
                        else
                        {
                            answerTexts[i].gameObject.SetActive(false);
                        }
                    }
                }
                else Debug.LogWarning("answerTexts 배열 데이터 누락됨.");
            }
            
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        /// <summary>
        /// 페이지 활성화 시 내부 상태를 초기화하고 연출 시퀀스를 시작함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isAnsweredA = false;
            _isAnsweredB = false;
            _completionStarted = false;
            _isInputEnabled = false;
            
            ResetIdleState(true);
            SetGroupAlpha(contentsGroup, 0f);
            
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

            if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = StartCoroutine(ShowSequence());
        }

        /// <summary>
        /// 매 프레임 키보드 또는 터치 입력을 감지하여 무응답 타이머를 갱신함.
        /// </summary>
        private void Update()
        {
            if (_completionStarted) return;

            bool inputDetected = false;
            if (_isInputEnabled) inputDetected = ProcessCommonKeyboardInput();

            if (inputDetected || Input.anyKey || Input.touchCount > 0) ResetIdleState(false);
            else UpdateInactivity(!_isInputEnabled);
        }

        /// <summary>
        /// 아두이노 하드웨어의 버튼 이벤트를 수신하여 로직으로 전달함.
        /// </summary>
        /// <param name="input">버튼 신호 문자열</param>
        /// <param name="isLeft">좌측(Player A) 여부</param>
        protected override void OnHardwareInput(string input, bool isLeft)
        {
            if (_completionStarted || !_isInputEnabled) return;

            int selectedValue = 0;
            
            // # TODO: 하드코딩된 "left"/"right" 문자열 할당으로 인한 가비지 방지를 위해 enum 또는 const 캐싱 변수 활용 필요.
            string side = isLeft ? "left" : "right";

            switch (input)
            {
                case GameConstants.Hardware.Input1On: selectedValue = 1; break;
                case GameConstants.Hardware.Input2On: selectedValue = 2; break;
                case GameConstants.Hardware.Input3On: selectedValue = 3; break;
                case GameConstants.Hardware.Input4On: selectedValue = 4; break;
                case GameConstants.Hardware.Input5On: selectedValue = 5; break;
            }

            if (selectedValue != 0) ProcessInput(selectedValue, side);
        }

        /// <summary>
        /// 유효한 입력에 대해 LED 상태를 끄고 API 서버에 선택값을 전송함.
        /// </summary>
        /// <param name="selectedValue">선택한 버튼의 인덱스</param>
        /// <param name="side">입력 주체 위치</param>
        private void ProcessInput(int selectedValue, string side)
        {
            bool isPlayerA = side.Equals("left");

            if (isPlayerA && _isAnsweredA) return;
            if (!isPlayerA && _isAnsweredB) return;

            ResetIdleState(false);
            
            if (isPlayerA) _isAnsweredA = true;
            else _isAnsweredB = true;
            
            if (ArduinoManager.Instance)
            {
                if (isPlayerA) ArduinoManager.Instance.SendCommandToLeft(GameConstants.Hardware.CmdLedAllOff);
                else ArduinoManager.Instance.SendCommandToRight(GameConstants.Hardware.CmdLedAllOff);
            }
            
            if (GameManager.Instance && LevelManager.Instance)
            {
                int qNo = LevelManager.Instance.CurrentQuestionNumber;
                if (qNo > 0) GameManager.Instance.SendValueUpdateAPI(qNo, side, selectedValue);
            }

            CanvasGroup targetCg = isPlayerA ? cgLightA : cgLightB;
            StartCoroutine(LightOnRoutine(targetCg));
            
            CheckCompletion();
        }

        /// <summary>
        /// 양측 플레이어가 모두 답변을 완료했는지 확인하고 완료 절차를 트리거함.
        /// </summary>
        private void CheckCompletion()
        {   
            if (_isAnsweredA && _isAnsweredB && !_completionStarted)
            {
                _completionStarted = true;
                StartCoroutine(CompleteRoutine());
            }
        }

        /// <summary>
        /// 답변 완료 시 사운드 피드백을 제공하고 다음 스텝으로 넘어감.
        /// </summary>
        private IEnumerator CompleteRoutine()
        {   
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_22");
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            CompleteStep();
        }

        /// <summary>
        /// 답변을 완료한 플레이어 측의 확인 라이트 UI를 점진적으로 밝힘.
        /// </summary>
        /// <param name="cg">대상 캔버스 그룹</param>
        private IEnumerator LightOnRoutine(CanvasGroup cg)
        {
            if (!cg) yield break;
            cg.gameObject.SetActive(true);
            cg.alpha = 0f;

            float t = 0f;
            while (t < 1.0f)
            {
                t += Time.deltaTime;
                // ex: t=0.5 -> alpha=0.5 (50%)
                cg.alpha = Mathf.Lerp(0f, 1f, t);
                yield return null;
            }
            cg.alpha = 1f;
        }

        /// <summary>
        /// 질문과 선택지를 노출시키고 튜토리얼 여부에 따라 입력 대기 상태를 제어함.
        /// </summary>
        private IEnumerator ShowSequence()
        {
            if (canvasGroup) yield return new WaitUntil(() => canvasGroup.alpha >= 0.9f);
            
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_8");
            
            yield return StartCoroutine(FadeContent(contentsGroup, 0f, 1f, 0.5f));
            
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdSoundOn);
                yield return CoroutineData.GetWaitForSeconds(0.1f);
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOn);
            }
            
            bool isTutorial = false;
            if (LevelManager.Instance)
            {
                isTutorial = LevelManager.Instance.CurrentQuestionNumber == 0;
            }
            
            if (isTutorial)
            {
                // 사용자의 입력 개입을 막아 튜토리얼 시퀀스의 흐름을 강제함
                _isInputEnabled = false; 
                yield return CoroutineData.GetWaitForSeconds(3.5f);
                
                if (ArduinoManager.Instance)
                {
                    ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOff);
                }
                
                CompleteStep();
            }
            else
            {
                _isInputEnabled = true;
            }
        }

        /// <summary>
        /// 대상 캔버스 그룹의 알파값을 설정된 시간 동안 부드럽게 변경함.
        /// </summary>
        private IEnumerator FadeContent(CanvasGroup cg, float start, float end, float duration)
        {
            if (!cg) yield break;
            if (end > 0f) cg.gameObject.SetActive(true);

            float t = 0f;
            cg.alpha = start;
            while (t < duration)
            {
                t += Time.deltaTime;
                // ex: start=0, end=1, t=0.25, duration=0.5 -> result=0.5
                cg.alpha = Mathf.Lerp(start, end, t / duration);
                yield return null;
            }
            cg.alpha = end;
            
            if (end <= 0f) cg.gameObject.SetActive(false);
        }

        /// <summary>
        /// 캔버스 그룹의 투명도를 즉시 설정하고, 0일 경우 비활성화하여 오버드로우를 줄임.
        /// </summary>
        private void SetGroupAlpha(CanvasGroup cg, float alpha)
        {
            if (cg)
            {
                cg.alpha = alpha;
                cg.gameObject.SetActive(alpha > 0f);
            }
        }
    }
}