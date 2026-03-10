using System.Collections;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using My.Scripts.Hardware;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.UI;

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

            if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = StartCoroutine(ShowSequence());
        }

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

        protected override void OnHardwareInput(string input, bool isLeft)
        {
            if (_isCompleted || !_isInputEnabled) return;

            int selectedValue = 0;
            string side = isLeft ? "left" : "right";

            // 상수 사용
            if (input == GameConstants.Hardware.Input1On) selectedValue = 1;
            else if (input == GameConstants.Hardware.Input2On) selectedValue = 2;
            else if (input == GameConstants.Hardware.Input3On) selectedValue = 3;
            else if (input == GameConstants.Hardware.Input4On) selectedValue = 4;
            else if (input == GameConstants.Hardware.Input5On) selectedValue = 5;

            if (selectedValue != 0)
            {
                ProcessInput(selectedValue, side);
            }
        }

        private void ProcessInput(int selectedValue, string side)
        {
            ResetIdleState(false);
            _isCompleted = true;
            
            if (ArduinoManager.Instance)
            {
                // 상수 사용
                if (side == "left") ArduinoManager.Instance.SendCommandToLeft(GameConstants.Hardware.CmdLedAllOff);
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
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdSoundOn);
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOn);
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