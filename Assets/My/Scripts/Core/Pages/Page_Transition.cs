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
    /// 게임 진행 중 안내 및 대기 시간을 제공하는 트랜지션(전환) 페이지.
    /// 특정 모드에서는 아두이노 버튼 입력을 기다리거나 자동으로 다음 단계로 넘어감.
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

        /// <summary>
        /// 외부에서 전달받은 데이터를 UI 컴포넌트에 바인딩함. 누락 시 경고 로그를 출력함.
        /// </summary>
        /// <param name="data">초기화할 트랜지션 데이터</param>
        protected override void SetupData(TransitionPageData data)
        {
            if (data == null) return;

            if (data.descriptionText != null)
            {
                if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            }
            else Debug.LogWarning("descriptionText 데이터 누락됨.");

            if (data.playerAName != null)
            {
                if (playerAName) UIManager.Instance.SetText(playerAName.gameObject, data.playerAName);
            }
            else Debug.LogWarning("playerAName 데이터 누락됨.");

            if (data.playerBName != null)
            {
                if (playerBName) UIManager.Instance.SetText(playerBName.gameObject, data.playerBName);
            }
            else Debug.LogWarning("playerBName 데이터 누락됨.");

            ReplaceNameTags(descriptionText);
            ReplaceNameTags(playerAName);
            ReplaceNameTags(playerBName);

            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }
        
        /// <summary>
        /// 텍스트 내의 포맷 문자열을 실제 유저 세션의 닉네임으로 치환함.
        /// </summary>
        /// <param name="txt">치환할 대상 Text 컴포넌트</param>
        private void ReplaceNameTags(Text txt)
        {
            if (!txt || !SessionManager.Instance || string.IsNullOrEmpty(txt.text)) return;

            string nameA = SessionManager.Instance.PlayerAFirstName;
            string nameB = SessionManager.Instance.PlayerBFirstName;
            
            if (string.IsNullOrWhiteSpace(nameA))
            {
                Debug.LogWarning("PlayerAFirstName 값이 누락됨.");
                nameA = ""; 
            }
            if (string.IsNullOrWhiteSpace(nameB))
            {
                Debug.LogWarning("PlayerBFirstName 값이 누락됨.");
                nameB = ""; 
            }
            
            // # TODO: Replace 호출은 매번 새로운 문자열을 생성하여 GC를 유발하므로, 성능이 중요한 경우 StringBuilder 포맷팅으로 개선 필요.
            txt.text = txt.text.Replace("{nameA}", nameA).Replace("{nameB}", nameB);
        }

        /// <summary>
        /// 페이지 진입 시 타이머 초기화 및 페이드인 연출을 시작함. 튜토리얼 예외 처리를 포함함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            _enterTime = Time.time;

            ResetIdleState(true);

            // 튜토리얼 모드(CurrentQuestionNumber == 0)에서 카메라 셔터 대기 상태일 경우, 
            // 유저의 버튼 입력 대기 상태를 무시하고 3초 뒤 강제 진행하도록 설정을 덮어씌움.
            bool isTutorial = false;
            if (LevelManager.Instance) isTutorial = LevelManager.Instance.CurrentQuestionNumber == 0;

            if (isTutorial && waitForShotButton)
            {
                autoPass = true;
                autoPassDelay = 3.0f;
            }

            if (contentGroup) contentGroup.alpha = 0f;
            if (namesGroup) namesGroup.alpha = 0f;

            PlaySFXOnEnter();
            
            StartCoroutine(SequenceRoutine());
        }

        /// <summary>
        /// 특정 씬(15번 문항)에서의 강제 초기화 루틴 중단을 처리하고 페이지를 벗어남.
        /// </summary>
        public override void OnExit()
        {
            bool isQ15Page6 = false;
            if (LevelManager.Instance && LevelManager.Instance.CurrentQuestionNumber == 15)
            {
                // # TODO: gameObject.name.Contains는 문자열 연산 부하가 있으므로, 별도의 플래그나 Enum으로 상태를 구분할 것.
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

        /// <summary>
        /// 아두이노 하드웨어 입력 이벤트를 처리함.
        /// </summary>
        /// <param name="input">입력 신호</param>
        /// <param name="isLeft">입력 위치</param>
        protected override void OnHardwareInput(string input, bool isLeft)
        {
            if (_isCompleted) return;

            // 튜토리얼 모드에서는 유저의 물리적인 하드웨어 조작을 완전히 무시하여 흐름을 통제함.
            bool isTutorial = false;
            if (LevelManager.Instance) isTutorial = LevelManager.Instance.CurrentQuestionNumber == 0;

            if (isTutorial && waitForShotButton) return;

            if (waitForShotButton && input == GameConstants.Hardware.InputShotOn)
            {
                ProcessManualNext();
            }
            else if (!waitForShotButton && input.EndsWith(GameConstants.Hardware.InputOnSuffix))
            {
                ProcessManualNext();
            }
        }

        /// <summary>
        /// 페이지 종류에 맞는 입장 효과음을 재생함.
        /// </summary>
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

        /// <summary>
        /// 매 프레임 키보드 또는 터치 입력을 검사하여 수동 진행을 처리함.
        /// </summary>
        private void Update()
        {
            if (_isCompleted) return;
            
            // 더블 클릭 등 잦은 입력으로 인한 의도치 않은 빠른 스킵을 방지하기 위한 유예 시간.
            if (Time.time - _enterTime < 1.5f) return; 

            if (Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false);

                bool isTutorial = false;
                if (LevelManager.Instance) isTutorial = LevelManager.Instance.CurrentQuestionNumber == 0;

                // 튜토리얼 모드에서는 키보드 입력을 통한 스킵도 차단함.
                if (isTutorial && waitForShotButton) return;

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

        /// <summary>
        /// 수동 조작을 통한 다음 페이지 진행 로직을 실행함.
        /// </summary>
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

        /// <summary>
        /// UI를 점진적으로 밝히고 자동 진행 타이머를 시작함.
        /// </summary>
        private IEnumerator SequenceRoutine()
        {
            if (contentGroup) yield return StartCoroutine(FadeGroup(contentGroup, 0f, 1f, 1f));
            
            if (namesGroup)
            {
                yield return StartCoroutine(FadeGroup(namesGroup, 0f, 1f, 1f));
            }
            
            if (waitForShotButton && ArduinoManager.Instance)
            {
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

        /// <summary>
        /// 페이지 퇴장 시 필요한 UI 페이드아웃 효과를 적용함.
        /// </summary>
        private IEnumerator FinishRoutine()
        {
            if (!keepContentOnFinish)
            {
                if (descriptionText)
                {
                    if (contentGroup) yield return StartCoroutine(FadeGroup(contentGroup, 1f, 0f, 0.5f));
                    if (namesGroup) yield return StartCoroutine(FadeGroup(namesGroup, 1f, 0f, 0.5f));
                }
            }
            
            CompleteStep();
        }

        /// <summary>
        /// 대상 캔버스 그룹의 투명도를 시간에 따라 보간함.
        /// </summary>
        private IEnumerator FadeGroup(CanvasGroup cg, float start, float end, float duration)
        {
            if (!cg) yield break;
            float t = 0;
            cg.alpha = start;
            while (t < duration)
            {
                t += Time.deltaTime;
                // ex: start=0, end=1, t=0.5, duration=1 -> alpha=0.5
                cg.alpha = Mathf.Lerp(start, end, t / duration);
                yield return null;
            }

            cg.alpha = end;
        }
        
        /// <summary>
        /// Description 텍스트 컴포넌트만의 투명도를 독립적으로 낮춤.
        /// </summary>
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