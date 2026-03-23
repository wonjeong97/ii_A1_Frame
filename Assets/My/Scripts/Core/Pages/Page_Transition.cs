using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using My.Scripts.Hardware; 
using UnityEngine.SceneManagement;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    public class Page_Transition : PopupGamePage<TransitionPageData>
    {
        [Header("Mode Settings")]
        [SerializeField] private bool autoPass = true; 
        [SerializeField] private float autoPassDelay = 4.0f; 
        [SerializeField] private bool keepContentOnFinish; 

        [Header("Arduino Integration")]
        [Tooltip("체크 시 카메라 연출 페이지로 동작하여, 등장 시 사운드/LED를 켜고 아두이노의 Shot 버튼 입력을 대기합니다.")]
        [SerializeField] private bool waitForShotButton = false;

        [Header("Common UI")] 
        [SerializeField] private Text descriptionText; 
        [SerializeField] private CanvasGroup contentGroup; 

        [Header("Intro Mode UI (Optional)")]
        [SerializeField] private Text playerAName; 
        [SerializeField] private Text playerBName; 
        [SerializeField] private CanvasGroup namesGroup; 

        private bool _isCompleted; 
        private float _enterTime; 

        protected override void SetupData(TransitionPageData data)
        {
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            if (playerAName) UIManager.Instance.SetText(playerAName.gameObject, data.playerAName);
            if (playerBName) UIManager.Instance.SetText(playerBName.gameObject, data.playerBName);

            ReplaceNameTags(descriptionText);
            ReplaceNameTags(playerAName);
            ReplaceNameTags(playerBName);

            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }
        
        private void ReplaceNameTags(Text txt)
        {
            if (txt != null && SessionManager.Instance != null && !string.IsNullOrEmpty(txt.text))
            {
                string nameA = string.IsNullOrWhiteSpace(SessionManager.Instance.PlayerAFirstName) ? "PlayerA" : SessionManager.Instance.PlayerAFirstName;
                string nameB = string.IsNullOrWhiteSpace(SessionManager.Instance.PlayerBFirstName) ? "PlayerB" : SessionManager.Instance.PlayerBFirstName;
                
                txt.text = txt.text.Replace("{nameA}", nameA).Replace("{nameB}", nameB);
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            _enterTime = Time.time;

            ResetIdleState(true);

            // =========================================================================================
            // 튜토리얼 모드(CurrentQuestionNumber == 0)이고 카메라 버튼 안내(waitForShotButton)일 경우,
            // 에디터 설정과 무관하게 3초 후 강제 자동 진행(autoPass)을 활성화합니다.
            // =========================================================================================
            bool isTutorial = LevelManager.Instance && LevelManager.Instance.CurrentQuestionNumber == 0;
            if (isTutorial && waitForShotButton)
            {
                autoPass = true;
                autoPassDelay = 3.0f;
            }
            // =========================================================================================

            if (contentGroup) contentGroup.alpha = 0f;
            if (namesGroup) namesGroup.alpha = 0f;

            PlaySFXOnEnter();
            
            StartCoroutine(SequenceRoutine());
        }

        public override void OnExit()
        {
            bool isQ15Page6 = false;
            if (LevelManager.Instance && LevelManager.Instance.CurrentQuestionNumber == 15)
            {
                if (gameObject.name.Contains("Page6"))
                {
                    isQ15Page6 = true;
                }
            }

            if (isQ15Page6)
            {
                StopResetSequence(true);
                UnsubscribeHardwareInput();
            }
            else
            {
                base.OnExit();
            }
        }

        protected override void OnHardwareInput(string input, bool isLeft)
        {
            if (_isCompleted) return;

            // =========================================================================================
            // 튜토리얼 모드에서는 어떠한 물리 버튼(Shot 버튼 포함) 조작도 철저히 무시합니다.
            // =========================================================================================
            bool isTutorial = LevelManager.Instance && LevelManager.Instance.CurrentQuestionNumber == 0;
            if (isTutorial && waitForShotButton) return;
            // =========================================================================================

            if (waitForShotButton && input == GameConstants.Hardware.InputShotOn)
            {
                ProcessManualNext();
            }
            else if (!waitForShotButton && input.EndsWith(GameConstants.Hardware.InputOnSuffix))
            {
                ProcessManualNext();
            }
        }

        private void PlaySFXOnEnter()
        {
            if (!SoundManager.Instance) return;
            
            if (gameObject.name.Contains("Page4"))
            {
                SoundManager.Instance.PlaySFX("공통_9");
            }
            else if (gameObject.name.Contains("Page6"))
            {
                SoundManager.Instance.PlaySFX("공통_12");
            }
        }

        private void Update()
        {
            if (_isCompleted) return;
            
            if (Time.time - _enterTime < 1.5f) return; 

            if (Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false);

                // =========================================================================================
                // 튜토리얼 모드 시 키보드 스페이스바를 통한 편법 스킵도 철저히 막습니다.
                // =========================================================================================
                bool isTutorial = LevelManager.Instance && LevelManager.Instance.CurrentQuestionNumber == 0;
                if (isTutorial && waitForShotButton) return;
                // =========================================================================================

                if (Input.GetKeyDown(KeyCode.Space) || !waitForShotButton)
                {
                    ProcessManualNext();
                }
            }
            else
            {
                UpdateInactivity();
            }
        }

        private void ProcessManualNext()
        {
            if (_isCompleted) return;
            
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_22");
            ResetIdleState(false);
            _isCompleted = true;

            if (waitForShotButton && ArduinoManager.Instance)
            {
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedShotOff);
            }

            StartCoroutine(FinishRoutine());
        }

        private IEnumerator SequenceRoutine()
        {
            yield return StartCoroutine(FadeGroup(contentGroup, 0f, 1f, 1f));
            if (namesGroup)
            {
                yield return StartCoroutine(FadeGroup(namesGroup, 0f, 1f, 1f));
            }
            
            if (waitForShotButton && ArduinoManager.Instance)
            {
                // LED는 정상적으로 점등됩니다. (입력만 안 받을 뿐입니다)
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedShotOn);
            }

            if (autoPass)
            {
                yield return CoroutineData.GetWaitForSeconds(autoPassDelay);
                
                if (!_isCompleted)
                {
                    _isCompleted = true;
                    
                    if (waitForShotButton && ArduinoManager.Instance)
                    {
                        ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedShotOff);
                    }

                    yield return StartCoroutine(FinishRoutine());
                }
            }
        }

        private IEnumerator FinishRoutine()
        {
            if (!keepContentOnFinish)
            {
                if (descriptionText)
                {
                    yield return StartCoroutine(FadeGroup(contentGroup, 1f, 0f, 0.5f));
                    if (namesGroup)
                    {
                        yield return StartCoroutine(FadeGroup(namesGroup, 1f, 0f, 0.5f));
                    }
                }
            }
            
            CompleteStep();
        }

        private IEnumerator FadeGroup(CanvasGroup cg, float start, float end, float duration)
        {
            if (!cg) yield break;
            float t = 0;
            cg.alpha = start;
            while (t < duration)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(start, end, t / duration);
                yield return null;
            }

            cg.alpha = end;
        }
        
        public IEnumerator FadeOutTextOnly(float duration)
        {
            if (!descriptionText) yield break;

            Color c = descriptionText.color;
            float startAlpha = c.a;
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                c.a = Mathf.Lerp(startAlpha, 0f, t / duration);
                descriptionText.color = c;
                yield return null;
            }

            c.a = 0f;
            descriptionText.color = c;
        }
    }
}