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

        protected override void SetupData(QnAPageData data)
        {
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            if (questionText) UIManager.Instance.SetText(questionText.gameObject, data.questionText);
            
            // # FIX: 빈 데이터를 세팅하여 위치가 0,0으로 가는 현상을 막기 위해 유효성 검사 추가
            if (nicknameA && data.nicknamePlayerA != null && !string.IsNullOrEmpty(data.nicknamePlayerA.text)) 
                UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            
            if (nicknameB && data.nicknamePlayerB != null && !string.IsNullOrEmpty(data.nicknamePlayerB.text)) 
                UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);

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
            _isAnsweredA = false;
            _isAnsweredB = false;
            _completionStarted = false;
            _isInputEnabled = false;
            
            ResetIdleState(true);
            SetGroupAlpha(contentsGroup, 0f);
            
            if (cgLightA) { cgLightA.alpha = 0f; cgLightA.gameObject.SetActive(false); }
            if (cgLightB) { cgLightB.alpha = 0f; cgLightB.gameObject.SetActive(false); }

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

        private void Update()
        {
            if (_completionStarted) return;

            bool inputDetected = false;
            if (_isInputEnabled) inputDetected = ProcessCommonKeyboardInput();

            if (inputDetected || Input.anyKey || Input.touchCount > 0) ResetIdleState(false);
            else UpdateInactivity(!_isInputEnabled);
        }

        protected override void OnHardwareInput(string input, bool isLeft)
        {
            if (_completionStarted || !_isInputEnabled) return;

            int selectedValue = 0;
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

        private void CheckCompletion()
        {   
            if (_isAnsweredA && _isAnsweredB && !_completionStarted)
            {
                _completionStarted = true;
                StartCoroutine(CompleteRoutine());
            }
        }

        private IEnumerator CompleteRoutine()
        {   
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_22");
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            CompleteStep();
        }

        private IEnumerator LightOnRoutine(CanvasGroup cg)
        {
            if (!cg) yield break;
            cg.gameObject.SetActive(true);
            cg.alpha = 0f;

            float t = 0f;
            while (t < 1.0f)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(0f, 1f, t);
                yield return null;
            }
            cg.alpha = 1f;
        }

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