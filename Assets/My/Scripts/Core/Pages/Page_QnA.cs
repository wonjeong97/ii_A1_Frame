using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary> 질문 및 답변 선택 페이지 컨트롤러 </summary>
    public class Page_QnA : GamePage<QnAPageData>
    {
        [Header("UI References")] 
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text questionText;
        [SerializeField] private Text[] answerTexts;

        [Header("Canvas Groups")] 
        [SerializeField] private CanvasGroup descriptionGroup;
        [SerializeField] private CanvasGroup questionGroup;
        [SerializeField] private CanvasGroup answerGroup;

        [Header("Popup Settings")]
        [Tooltip("1차 경고 표시 시간 (초)")]
        [SerializeField] private float warningDuration = 3f; 
        
        [Tooltip("2차 초기화 안내 표시 시간 (초)")]
        [SerializeField] private float resetPopupDuration = 3f;

        [Header("Single Popup")]
        [SerializeField] private CanvasGroup popupCanvasGroup; // 통합 팝업 그룹
        [SerializeField] private Text popupText; // 팝업 내부 텍스트
        
        
        private string msgWarning;
        private string msgReset;

        private Coroutine _sequenceRoutine; 
        private Coroutine _resetSequenceRoutine; 

        private bool _isCompleted; 
        private bool _isInputEnabled; 
        
        private float _currentIdleTime = 0f;
        private bool _isResetSequenceActive = false; 
        
        private float inactivityThreshold = 20f; 
        private float countdownDuration = 10f;   

        /// <summary> 시작 시 설정 파일(Settings.json)을 로드하여 시간 값을 초기화. </summary>
        private void Start()
        {
            var settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);
            if (settings != null)
            {
                inactivityThreshold = settings.warningTime;

                // 전체 리셋 시간에서 경고 시작 시간과 팝업 지속 시간을 뺀 나머지를 카운트다운으로 설정
                float calculatedDuration = settings.resetTime - settings.warningTime - warningDuration;
                
                if (calculatedDuration > 0)
                {
                    countdownDuration = calculatedDuration;
                }
                else
                {
                    Debug.LogWarning("[Page_QnA] 카운트다운 시간이 0 이하입니다.");
                }
            }
        }

        /// <summary> 데이터 매니저로부터 받은 페이지 데이터(텍스트 등)를 UI에 적용. </summary>
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
            
            if (!string.IsNullOrEmpty(data.warningMessage)) 
            {
                msgWarning = data.warningMessage;
            }
            
            if (!string.IsNullOrEmpty(data.resetMessage)) 
            {
                msgReset = data.resetMessage;
            }
        }

        /// <summary> 페이지 진입 시 호출되어 상태를 초기화하고 등장 연출을 시작. </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            _isInputEnabled = false;
            
            StopResetSequence();
            _currentIdleTime = 0f;

            SetGroupAlpha(questionGroup, 0f);
            SetGroupAlpha(answerGroup, 0f);
            SetGroupAlpha(descriptionGroup, 0f);

            if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = StartCoroutine(ShowSequence());
        }

        /// <summary> 페이지 퇴장 시 호출되어 실행 중인 시퀀스를 정리. </summary>
        public override void OnExit()
        {
            base.OnExit();
            StopResetSequence();
        }

        /// <summary> 매 프레임 입력을 감지하고 비활성 시간을 누적 체크. </summary>
        private void Update()
        {
            if (_isCompleted) return;

            // 1. 입력 감지 (키보드 또는 터치)
            if (Input.anyKey || Input.touchCount > 0)
            {
                // 리셋 시퀀스가 진행 중이거나 대기 시간이 쌓였다면 초기화
                if (_isResetSequenceActive || _currentIdleTime > 0f)
                {
                    StopResetSequence();
                    Debug.Log("[Page_QnA] 입력 감지: 리셋 시퀀스 중단");
                }
                
                // 정답 선택 처리
                if (_isInputEnabled)
                {
                    HandleSelectionInput();
                }
            }
            else
            {
                // 2. 비활성 시간 누적
                if (_isInputEnabled && !_isResetSequenceActive)
                {
                    _currentIdleTime += Time.deltaTime;
                    
                    // 임계치 도달 시 리셋 시퀀스 시작
                    if (_currentIdleTime >= inactivityThreshold)
                    {
                        StartResetSequence();
                    }
                }
            }
        }

        /// <summary> 플레이어의 키 입력(숫자키)에 따라 답변을 선택 처리. </summary>
        private void HandleSelectionInput()
        {
            // Player A (1~5)
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha2) ||
                Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Alpha4) ||
                Input.GetKeyDown(KeyCode.Alpha5))
            {
                _isCompleted = true;
                CompleteStep(1); // 1: Player A 완료 정보
            }

            // Player B (6~0)
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Alpha7) ||
                Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Alpha9) ||
                Input.GetKeyDown(KeyCode.Alpha0))
            {
                _isCompleted = true;
                CompleteStep(2); // 2: Player B 완료 정보
            }
        }

        /// <summary> 비활성 리셋 시퀀스 코루틴을 시작. </summary>
        private void StartResetSequence()
        {
            if (_isResetSequenceActive) return;
            _isResetSequenceActive = true;
            _resetSequenceRoutine = StartCoroutine(ResetProcessRoutine());
        }

        /// <summary> 리셋 시퀀스를 중단하고 팝업을 닫음. </summary>
        private void StopResetSequence()
        {
            _isResetSequenceActive = false;
            _currentIdleTime = 0f;
            
            if (_resetSequenceRoutine != null) StopCoroutine(_resetSequenceRoutine);
            
            // 팝업 즉시 숨김
            if (popupCanvasGroup)
            {
                popupCanvasGroup.alpha = 0f;
                popupCanvasGroup.gameObject.SetActive(false);
            }
        }

        /// <summary> 
        /// 단계별 리셋 로직을 수행하는 코루틴. 
        /// <para>과정: 1차 경고(유지) -> 카운트다운 -> 2차 안내(텍스트 변경) -> 초기화</para>
        /// </summary>
        private IEnumerator ResetProcessRoutine()
        {
            Debug.Log("[Page_QnA] 리셋 시퀀스 진입");

            // [단계 1] 1차 경고: 텍스트 설정 및 표시 (3초 대기, 팝업 유지)
            ShowPopup(msgWarning);
            yield return CoroutineData.GetWaitForSeconds(warningDuration); 
            
            // [단계 2] 카운트다운 (팝업이 켜진 상태로 진행)
            Debug.Log("[Page_QnA] 카운트다운 시작");
            float timer = countdownDuration;
            while (timer > 0f)
            {
                // [TODO] 째깍 소리 재생 (예: SoundManager.Play("Tick"))
                timer -= 1.0f;
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            // [단계 3] 2차 초기화 안내: 팝업 내용은 변경하고 창은 유지
            ShowPopup(msgReset);
            yield return CoroutineData.GetWaitForSeconds(resetPopupDuration);

            // [단계 4] 최종 리셋 (타이틀 씬 이동)
            Debug.Log("[Page_QnA] 타이틀로 초기화");
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReturnToTitle();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(GameConstants.Scene.Title);
            }
        }

        /// <summary> 팝업의 텍스트를 설정하고 활성화(페이드 인). </summary>
        private void ShowPopup(string message)
        {
            if (!popupCanvasGroup) return;
            
            if (popupText) popupText.text = message;
            
            // 이미 켜져 있다면 텍스트만 바뀌고 Alpha는 유지됨
            popupCanvasGroup.gameObject.SetActive(true);
            StartCoroutine(FadeGroup(popupCanvasGroup, popupCanvasGroup.alpha, 1f, 0.5f));
        }

        /// <summary> 페이지 진입 시 UI 요소들을 순차적으로 페이드 인. </summary>
        private IEnumerator ShowSequence()
        {
            // 페이지 전체 페이드 완료 대기
            if (canvasGroup) yield return new WaitUntil(() => canvasGroup.alpha >= 0.9f);

            yield return StartCoroutine(FadeGroup(questionGroup, 0f, 1f, 1.0f));
            yield return StartCoroutine(FadeGroup(answerGroup, 0f, 1f, 1.0f));
            yield return StartCoroutine(FadeGroup(descriptionGroup, 0f, 1f, 1.0f));

            _isInputEnabled = true;
        }

        /// <summary> CanvasGroup의 투명도를 조절하는 유틸리티 코루틴. </summary>
        private IEnumerator FadeGroup(CanvasGroup cg, float start, float end, float duration)
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

        /// <summary> CanvasGroup의 투명도를 즉시 설정. </summary>
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