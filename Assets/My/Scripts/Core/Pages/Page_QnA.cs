using System.Collections;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using My.Scripts.Hardware;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.UI;

namespace My.Scripts.Core.Pages
{
    /// <summary> 
    /// 질문과 선택지(답변)를 화면에 표시하고 하드웨어 입력을 받아 처리하는 페이지 컨트롤러.
    /// 입력이 완료되면 서버로 데이터를 전송하고 다음 페이지(주로 Page_Check)로 전환합니다.
    /// </summary>
    public class Page_QnA : PopupGamePage<QnAPageData>
    {
        [Header("UI References")] 
        [SerializeField] private Text descriptionText; 
        [SerializeField] private Text questionText; 
        [SerializeField] private Text[] answerTexts; 

        [Header("Canvas Groups")] 
        [SerializeField] private CanvasGroup descriptionGroup; 
        [SerializeField] private CanvasGroup questionGroup; 
        [SerializeField] private CanvasGroup answerGroup; 

        private Coroutine _sequenceRoutine; 
        private bool _isCompleted; 
        private bool _isInputEnabled; 

        /// <summary> 
        /// JSON 설정에서 질문, 답변 배열 및 경고 메시지를 로드하여 UI에 매핑합니다.
        /// 데이터 개수에 맞춰 불필요한 선택지 UI는 비활성화하여 렌더링 오버헤드를 줄입니다.
        /// </summary>
        protected override void SetupData(QnAPageData data)
        {
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            if (questionText) UIManager.Instance.SetText(questionText.gameObject, data.questionText);

            if (answerTexts != null)
            {
                for (int i = 0; i < answerTexts.Length; i++)
                {
                    if (!answerTexts[i]) continue;
                    if (data.answerTexts != null && i < data.answerTexts.Length)
                    {
                        UIManager.Instance.SetText(answerTexts[i].gameObject, data.answerTexts[i]);
                        answerTexts[i].gameObject.SetActive(true);
                    }
                    else answerTexts[i].gameObject.SetActive(false);
                }
            }
            
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        /// <summary> 
        /// 페이지 진입 시 이전 상태를 초기화하고 화면을 숨깁니다.
        /// 이후 페이드인 연출을 순차적으로 실행하기 위한 코루틴을 가동합니다.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            _isInputEnabled = false;
            
            ResetIdleState(true);

            SetGroupAlpha(questionGroup, 0f);
            SetGroupAlpha(answerGroup, 0f);
            SetGroupAlpha(descriptionGroup, 0f);

            if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = StartCoroutine(ShowSequence());
        }

        /// <summary> 매 프레임 키보드 및 하드웨어 입력을 감지하여 무응답(Idle) 타임아웃을 갱신합니다. </summary>
        private void Update()
        {
            if (_isCompleted) return;

            bool inputDetected = false;

            if (_isInputEnabled)
            {
                inputDetected = ProcessCommonKeyboardInput();
            }

            if (inputDetected || Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false);
            }
            else
            {
                UpdateInactivity(!_isInputEnabled);
            }
        }

        /// <summary> 아두이노로부터 수신된 문자열 신호를 파싱하여 선택된 버튼의 인덱스(1~5)를 추출합니다. </summary>
        protected override void OnHardwareInput(string input, bool isLeft)
        {
            if (_isCompleted || !_isInputEnabled) return;

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

        /// <summary> 유효한 입력이 감지되면 하드웨어 LED를 끄고, 서버에 선택 결과를 비동기로 전송한 뒤 페이지 시퀀스를 종료합니다. </summary>
        private void ProcessInput(int selectedValue, string side)
        {
            ResetIdleState(false);
            _isCompleted = true;
            
            // 입력이 끝난 플레이어 측의 LED를 일괄 소등하여 중복 입력 방지 및 시각적 피드백 제공
            if (ArduinoManager.Instance)
            {
                if (side == "left") ArduinoManager.Instance.SendCommandToLeft(GameConstants.Hardware.CmdLedAllOff);
                else ArduinoManager.Instance.SendCommandToRight(GameConstants.Hardware.CmdLedAllOff);
            }
            
            // 선택된 데이터를 서버에 동기화
            if (GameManager.Instance && LevelManager.Instance)
            {
                int qNo = LevelManager.Instance.CurrentQuestionNumber;
                if (qNo > 0)
                {
                    GameManager.Instance.SendValueUpdateAPI(qNo, side, selectedValue);
                }
            }

            // 완료 신호를 다음 페이지(ITriggerReceiver)로 전달하기 위해 side 값(1 또는 2) 반환
            CompleteStep(side == "left" ? 1 : 2); 
        }

        /// <summary> 
        /// 질문 -> 선택지 -> 설명 순으로 화면에 부드럽게 노출시킵니다.
        /// 연출이 완전히 끝난 후에만 하드웨어 입력을 허용하여 성급한 조작을 방지합니다.
        /// </summary>
        private IEnumerator ShowSequence()
        {
            if (canvasGroup) yield return new WaitUntil(() => canvasGroup.alpha >= 0.9f);
            
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_8");
            
            yield return StartCoroutine(FadeContent(questionGroup, 0f, 1f, 1f));
            yield return StartCoroutine(FadeContent(answerGroup, 0f, 1f, 1f));
            yield return StartCoroutine(FadeContent(descriptionGroup, 0f, 1f, 1f));
            
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdSoundOn);
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOn);
            }
            
            _isInputEnabled = true;
        }

        /// <summary> CanvasGroup의 투명도(Alpha)를 지정된 시간 동안 선형 보간하여 시각적 전환을 수행합니다. </summary>
        private IEnumerator FadeContent(CanvasGroup cg, float start, float end, float duration)
        {
            if (!cg) yield break;
            if (end > 0f) cg.gameObject.SetActive(true);

            float t = 0f;
            cg.alpha = start;
            while (t < duration)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(start, end, t / duration);
                yield return null;
            }
            cg.alpha = end;
            
            if (end <= 0f) cg.gameObject.SetActive(false);
        }

        /// <summary> 그룹의 투명도를 즉시 설정하고, 완전히 투명할 경우 불필요한 렌더링을 막기 위해 비활성화합니다. </summary>
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