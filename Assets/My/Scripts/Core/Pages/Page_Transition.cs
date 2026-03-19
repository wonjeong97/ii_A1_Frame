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
    /// <summary> 
    /// 전환 화면 및 안내 텍스트를 표시하고 다음 단계로 넘어가는 역할을 담당하는 페이지 컨트롤러.
    /// 지정된 시간 후 자동으로 넘어가는 모드(Auto Pass)와 아두이노 하드웨어(Shot 버튼) 입력을 대기하는 모드를 모두 지원합니다.
    /// </summary>
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

        /// <summary> JSON 설정에서 텍스트(설명, 플레이어 이름, 경고문)를 로드하여 UI에 매핑합니다. </summary>
        protected override void SetupData(TransitionPageData data)
        {
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            if (playerAName) UIManager.Instance.SetText(playerAName.gameObject, data.playerAName);
            if (playerBName) UIManager.Instance.SetText(playerBName.gameObject, data.playerBName);

            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        /// <summary> 페이지 진입 시 상태를 초기화하고 페이드인 연출 코루틴을 실행합니다. </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            _enterTime = Time.time;

            ResetIdleState(true);

            if (contentGroup) contentGroup.alpha = 0f;
            if (namesGroup) namesGroup.alpha = 0f;

            PlaySFXOnEnter();
            
            StartCoroutine(SequenceRoutine());
        }

        /// <summary> 페이지 퇴장 시 리소스를 정리합니다. 15번 질문의 6페이지일 경우 예외적으로 입력 구독 해제만 수행합니다. </summary>
        public override void OnExit()
        {
            // 15번 문제의 6페이지에 대한 하드코딩된 예외 처리
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

        /// <summary> 아두이노로부터 입력된 문자열 신호를 분석하여 수동 넘김(Shot 버튼 등) 처리를 수행합니다. </summary>
        protected override void OnHardwareInput(string input, bool isLeft)
        {
            if (_isCompleted) return;

            // Shot 대기 모드일 경우 Shot 버튼 입력만 승인, 아닐 경우 일반 버튼 입력 승인
            if (waitForShotButton && input == GameConstants.Hardware.InputShotOn)
            {
                ProcessManualNext();
            }
            else if (!waitForShotButton && input.EndsWith(GameConstants.Hardware.InputOnSuffix))
            {
                ProcessManualNext();
            }
        }

        /// <summary> 페이지 이름에 기반하여 하드코딩된 등장 효과음을 재생합니다. </summary>
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

        /// <summary> 매 프레임 키보드 및 화면 터치 입력을 감지하여 수동 넘김을 처리하거나 무응답 타임아웃을 갱신합니다. </summary>
        private void Update()
        {
            if (_isCompleted) return;
            
            // 너무 빠른 스킵을 방지하기 위한 1.5초 쿨타임
            if (Time.time - _enterTime < 1.5f) return; 

            if (Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false);

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

        /// <summary> 하드웨어/소프트웨어 입력에 의해 호출되며, 진행 완료 상태로 변경 후 아두이노 LED를 소등합니다. </summary>
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

        /// <summary> 화면 페이드인 연출을 진행하고, 모드에 따라 Shot 버튼 LED를 점등하거나 지정된 시간 후 자동으로 다음 단계로 넘깁니다. </summary>
        private IEnumerator SequenceRoutine()
        {
            yield return StartCoroutine(FadeGroup(contentGroup, 0f, 1f, 1f));
            if (namesGroup)
            {
                yield return StartCoroutine(FadeGroup(namesGroup, 0f, 1f, 1f));
            }
            
            if (waitForShotButton && ArduinoManager.Instance)
            {
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedShotOn);
            }

            // 자동 넘김(Auto-pass)이 활성화되어 있다면 지정된 시간 대기 후 스스로 완료 처리
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

        /// <summary> 퇴장 페이드아웃 연출을 수행하고 페이지 완료 이벤트를 호출합니다. </summary>
        private IEnumerator FinishRoutine()
        {
            // 설정에 따라 퇴장 시 콘텐츠를 화면에 남겨둘지 결정
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

        /// <summary> 대상 CanvasGroup의 투명도를 선형 보간하여 시각적인 전환 효과를 생성합니다. </summary>
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
        
        /// <summary> 외부 또는 기타 연출에서 텍스트 컴포넌트만의 투명도를 별도로 조절할 때 사용합니다. </summary>
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