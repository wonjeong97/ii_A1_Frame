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
    /// <summary> 전환 및 안내 텍스트 페이지 컨트롤러 </summary>
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

            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            _enterTime = Time.time;

            ResetIdleState(true);

            if (contentGroup) contentGroup.alpha = 0f;
            if (namesGroup) namesGroup.alpha = 0f;

            PlaySFXOnEnter();
            
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.OnHardwareInput -= HandleArduinoInput;
                ArduinoManager.Instance.OnHardwareInput += HandleArduinoInput;
            }
            
            StartCoroutine(SequenceRoutine());
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

        private void HandleArduinoInput(string input, bool isLeft)
        {
            if (_isCompleted) return;

            // 카메라 대기 모드일 때 ShotOn 입력 감지
            if (waitForShotButton && input == "ShotOn")
            {
                ProcessManualNext();
            }
            // 일반 대기 모드일 때 아무 버튼이나 누르면 스킵
            else if (!waitForShotButton && input.EndsWith("On"))
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
            if (Time.time - _enterTime < 1.5f) return; // 진입 직후 오입력 방지

            if (Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false);

                // 스페이스바를 누르거나, waitForShotButton이 아닐때의 터치 입력 시 넘김 처리
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
            ResetIdleState(false);
            _isCompleted = true;

            // [추가] 수동/입력으로 넘어갈 때 LED 끄기
            if (waitForShotButton && ArduinoManager.Instance)
            {
                ArduinoManager.Instance.SendCommandToBoth("LEDShotOff");
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
            
            // [추가] 연출 등장 직후 카메라 샷 버튼 조명 및 사운드 켜기
            if (waitForShotButton && ArduinoManager.Instance)
            {
                ArduinoManager.Instance.SendCommandToBoth("SoundOn");
                ArduinoManager.Instance.SendCommandToBoth("LEDShotOn");
            }

            if (autoPass)
            {
                yield return CoroutineData.GetWaitForSeconds(autoPassDelay);
                
                if (!_isCompleted)
                {
                    _isCompleted = true;
                    
                    // [추가] 자동 넘김 시 LED 끄기
                    if (waitForShotButton && ArduinoManager.Instance)
                    {
                        ArduinoManager.Instance.SendCommandToBoth("LEDShotOff");
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
    }
}