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
    /// <summary>  질문 및 답변 선택 페이지 컨트롤러 </summary>
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

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            _isInputEnabled = false;
            
            ResetIdleState(true);

            SetGroupAlpha(questionGroup, 0f);
            SetGroupAlpha(answerGroup, 0f);
            SetGroupAlpha(descriptionGroup, 0f);

            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.OnHardwareInput -= HandleArduinoInput;
                ArduinoManager.Instance.OnHardwareInput += HandleArduinoInput;
            }

            if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = StartCoroutine(ShowSequence());
        }

        public override void OnExit()
        {
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.OnHardwareInput -= HandleArduinoInput;
            }
            base.OnExit();
        }

        private void OnDestroy()
        {
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.OnHardwareInput -= HandleArduinoInput;
            }
        }

        private void Update()
        {
            if (_isCompleted) return;

            bool inputDetected = false;

            if (_isInputEnabled)
            {
                int selectedValue = 0;
                string side = string.Empty;

                // Left 디버그 (Q W E R T)
                if (Input.GetKeyDown(KeyCode.Q)) { selectedValue = 1; side = "left"; }
                else if (Input.GetKeyDown(KeyCode.W)) { selectedValue = 2; side = "left"; }
                else if (Input.GetKeyDown(KeyCode.E)) { selectedValue = 3; side = "left"; }
                else if (Input.GetKeyDown(KeyCode.R)) { selectedValue = 4; side = "left"; }
                else if (Input.GetKeyDown(KeyCode.T)) { selectedValue = 5; side = "left"; }
                // Right 디버그 (Y U I O P)
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

        private void HandleArduinoInput(string input, bool isLeft)
        {
            if (_isCompleted || !_isInputEnabled) return;

            int selectedValue = 0;
            string side = isLeft ? "left" : "right";

            if (input == "1On") selectedValue = 1;
            else if (input == "2On") selectedValue = 2;
            else if (input == "3On") selectedValue = 3;
            else if (input == "4On") selectedValue = 4;
            else if (input == "5On") selectedValue = 5;

            if (selectedValue != 0)
            {
                ProcessInput(selectedValue, side);
            }
        }

        private void ProcessInput(int selectedValue, string side)
        {
            ResetIdleState(false);
            _isCompleted = true;
            
            // [수정] 입력이 들어온 쪽의 아두이노 LED만 개별적으로 끕니다.
            if (ArduinoManager.Instance)
            {
                if (side == "left") ArduinoManager.Instance.SendCommandToLeft("LEDAllOff");
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

            CompleteStep(side == "left" ? 1 : 2); 
        }

        private IEnumerator ShowSequence()
        {
            if (canvasGroup) yield return new WaitUntil(() => canvasGroup.alpha >= 0.9f);
            
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_8");
            
            yield return StartCoroutine(FadeContent(questionGroup, 0f, 1f, 1f));
            yield return StartCoroutine(FadeContent(answerGroup, 0f, 1f, 1f));
            yield return StartCoroutine(FadeContent(descriptionGroup, 0f, 1f, 1f));
            
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.SendCommandToBoth("SoundOn");
                ArduinoManager.Instance.SendCommandToBoth("LEDAllOn");
            }
            
            _isInputEnabled = true;
        }

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