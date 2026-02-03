using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using My.Scripts.Global;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;
using UnityEngine.SceneManagement;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage3Data
    {
        public TextSetting descriptionText;
        public TextSetting nicknamePlayerA;
        public TextSetting nicknamePlayerB;
        
        public string warningMessage; 
        public string resetMessage;   
    }

    /// <summary> 튜토리얼 3페이지 컨트롤러 (휠 조작 유도 및 리셋 팝업) </summary>
    public class TutorialPage3Controller : GamePage<TutorialPage3Data>
    {
        [Header("Page 3 UI")]
        [SerializeField] private Text descriptionText; // 설명 텍스트
        [SerializeField] private Text nicknameA; // 플레이어 A 닉네임
        [SerializeField] private Text nicknameB; // 플레이어 B 닉네임

        [Header("Popup References")]
        [SerializeField] private CanvasGroup popupCanvasGroup; // Popup_Root의 캔버스 그룹
        [SerializeField] private Text popupText; // 팝업 내부 텍스트

        [Header("Popup Settings")]
        [SerializeField] private float warningDuration = 3f; // 1차 경고 표시 시간
        [SerializeField] private float resetPopupDuration = 3f; // 2차 리셋 안내 표시 시간

        // 내부 로직 변수
        private string _msgWarning;
        private string _msgReset;
        
        private float _inactivityThreshold = 20f; // Settings에서 로드
        private float _countdownDuration = 10f;   // Settings 기반 계산
        
        private float _currentIdleTime;
        private bool _isResetSequenceActive;
        private Coroutine _resetSequenceRoutine;

        private void Start()
        {
            // Settings.json에서 시간 값 로드
            var settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);
            if (settings != null)
            {
                _inactivityThreshold = settings.warningTime;
                
                // 전체 리셋 시간 - 경고 시작 시간 - 팝업 지속 시간 = 카운트다운 시간
                float calculatedDuration = settings.resetTime - settings.warningTime - warningDuration;
                if (calculatedDuration > 0) _countdownDuration = calculatedDuration;
            }
        }

        /// <summary> 데이터 설정 (UI 텍스트 및 메시지 적용) </summary>
        protected override void SetupData(TutorialPage3Data data)
        {
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);

            // JSON 메시지 적용
            if (!string.IsNullOrEmpty(data.warningMessage)) _msgWarning = data.warningMessage;
            if (!string.IsNullOrEmpty(data.resetMessage)) _msgReset = data.resetMessage;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            StopResetSequence(); // 초기화
            _currentIdleTime = 0f;
        }

        public override void OnExit()
        {
            base.OnExit();
            StopResetSequence();
        }

        private void Update()
        {
            // 1. 입력 감지 (아무 키나 터치)
            if (Input.anyKey || Input.touchCount > 0)
            {
                if (_isResetSequenceActive || _currentIdleTime > 0f)
                {
                    StopResetSequence(); // 리셋 중단
                }

                // 숫자키 입력 처리
                if (Input.GetKeyDown(KeyCode.Alpha1)) CompleteStep(1); 
                else if (Input.GetKeyDown(KeyCode.Alpha2)) CompleteStep(2); 
            }
            else
            {
                // 2. 비활성 시간 누적
                if (!_isResetSequenceActive)
                {
                    _currentIdleTime += Time.deltaTime;
                    if (_currentIdleTime >= _inactivityThreshold)
                    {
                        StartResetSequence();
                    }
                }
            }
        }

        /// <summary> 리셋 시퀀스 시작 </summary>
        private void StartResetSequence()
        {
            if (_isResetSequenceActive) return;
            _isResetSequenceActive = true;
            _resetSequenceRoutine = StartCoroutine(ResetProcessRoutine());
        }

        /// <summary> 리셋 시퀀스 중단 및 팝업 닫기 </summary>
        private void StopResetSequence()
        {
            _isResetSequenceActive = false;
            _currentIdleTime = 0f;
            
            if (_resetSequenceRoutine != null) StopCoroutine(_resetSequenceRoutine);
            
            if (popupCanvasGroup)
            {
                popupCanvasGroup.alpha = 0f;
                popupCanvasGroup.gameObject.SetActive(false);
            }
        }

        /// <summary> 단계별 리셋 진행 코루틴 </summary>
        private IEnumerator ResetProcessRoutine()
        {
            Debug.Log("[TutorialPage3] 리셋 시퀀스 시작");

            // [1단계] 경고 팝업 ("휠을 돌려주세요")
            ShowPopup(_msgWarning);
            yield return CoroutineData.GetWaitForSeconds(warningDuration); // 3초 유지

            // [2단계] 카운트다운 (팝업 켜진 상태로 대기)
            Debug.Log("[TutorialPage3] 카운트다운 시작");
            float timer = _countdownDuration;
            while (timer > 0f)
            {
                timer -= 1.0f;
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            // [3단계] 리셋 안내 팝업 ("초기화됩니다") - 텍스트 교체
            ShowPopup(_msgReset);
            yield return CoroutineData.GetWaitForSeconds(resetPopupDuration); // 3초 유지

            // [4단계] 타이틀로 이동
            Debug.Log("[TutorialPage3] 타이틀로 이동");
            if (GameManager.Instance != null) GameManager.Instance.ReturnToTitle();
            else SceneManager.LoadScene(GameConstants.Scene.Title);
        }

        /// <summary> 팝업 표시 (페이드 인 및 텍스트 설정) </summary>
        private void ShowPopup(string message)
        {
            if (!popupCanvasGroup) return;

            if (popupText) popupText.text = message;
            
            popupCanvasGroup.gameObject.SetActive(true);
            // 이미 켜져 있으면 Alpha 1 유지, 꺼져 있으면 페이드 인
            StartCoroutine(FadeGroup(popupCanvasGroup, popupCanvasGroup.alpha, 1f, 0.5f));
        }

        private IEnumerator FadeGroup(CanvasGroup cg, float start, float end, float duration)
        {
            float t = 0f;
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