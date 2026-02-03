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
    public class TutorialPage1Data
    {
        public TextSetting descriptionText;
        
        public string warningMessage; 
        public string resetMessage;   
    }

    /// <summary> 튜토리얼 1페이지 컨트롤러 (엔터 키 대기 + 리셋 팝업) </summary>
    public class TutorialPage1Controller : GamePage<TutorialPage1Data>
    {
        [Header("Page 1 UI")]
        [SerializeField] private Text descriptionText; // 설명 텍스트

        [Header("Popup References")]
        [SerializeField] private CanvasGroup popupCanvasGroup; // Popup_Root
        [SerializeField] private Text popupText; // 팝업 텍스트

        [Header("Popup Settings")]
        [SerializeField] private float warningDuration = 3f; 
        [SerializeField] private float resetPopupDuration = 3f;

        // 내부 로직 변수
        private string _msgWarning;
        private string _msgReset;
        
        private float _inactivityThreshold = 20f; 
        private float _countdownDuration = 10f;   
        
        private float _currentIdleTime = 0f;
        private bool _isResetSequenceActive = false;
        private Coroutine _resetSequenceRoutine;

        private void Start()
        {
            // Settings.json 로드
            var settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);
            if (settings != null)
            {
                _inactivityThreshold = settings.warningTime;
                
                float calculatedDuration = settings.resetTime - settings.warningTime - warningDuration;
                if (calculatedDuration > 0) _countdownDuration = calculatedDuration;
            }
        }

        /// <summary> 데이터 설정 </summary>
        protected override void SetupData(TutorialPage1Data data)
        {
            if (descriptionText) 
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            }

            // 메시지 적용
            if (!string.IsNullOrEmpty(data.warningMessage)) _msgWarning = data.warningMessage;
            if (!string.IsNullOrEmpty(data.resetMessage)) _msgReset = data.resetMessage;
        }

        public override void OnEnter()
        {
            base.OnEnter(); 

            // 상태 초기화
            StopResetSequence();
            _currentIdleTime = 0f;

            // 텍스트 페이드 인 연출 (기존 로직 유지)
            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = 0f;
                descriptionText.color = c;
                StartCoroutine(FadeInTextRoutine());
            }
        }

        public override void OnExit()
        {
            base.OnExit();
            StopResetSequence();
        }

        private void Update()
        {
            // 1. 입력 감지
            if (Input.anyKey || Input.touchCount > 0)
            {
                // 리셋 중단
                if (_isResetSequenceActive || _currentIdleTime > 0f)
                {
                    StopResetSequence();
                }

                // Enter 키 입력 시 성공 처리 (기존 로직)
                if (Input.GetKeyDown(KeyCode.Return))
                {
                    CompleteStep(); 
                }
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

        /// <summary> 기존 텍스트 페이드 인 연출 </summary>
        private IEnumerator FadeInTextRoutine()
        {
            float duration = 1.0f;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float alpha = Mathf.Clamp01(timer / duration);

                if (descriptionText)
                {
                    Color c = descriptionText.color;
                    c.a = alpha;
                    descriptionText.color = c;
                }
                yield return null;
            }

            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = 1f;
                descriptionText.color = c;
            }
        }

        // --- 리셋 로직 (Page 3와 동일) ---

        private void StartResetSequence()
        {
            if (_isResetSequenceActive) return;
            _isResetSequenceActive = true;
            _resetSequenceRoutine = StartCoroutine(ResetProcessRoutine());
        }

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

        private IEnumerator ResetProcessRoutine()
        {
            Debug.Log("[TutorialPage1] 리셋 시퀀스 시작");

            // [1단계] 경고
            ShowPopup(_msgWarning);
            yield return CoroutineData.GetWaitForSeconds(warningDuration); 

            // [2단계] 카운트다운
            float timer = _countdownDuration;
            while (timer > 0f)
            {
                timer -= 1.0f;
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            // [3단계] 초기화 안내
            ShowPopup(_msgReset);
            yield return CoroutineData.GetWaitForSeconds(resetPopupDuration);

            // [4단계] 리셋
            if (GameManager.Instance != null) GameManager.Instance.ReturnToTitle();
            else SceneManager.LoadScene(GameConstants.Scene.Title);
        }

        private void ShowPopup(string message)
        {
            if (!popupCanvasGroup) return;

            if (popupText) popupText.text = message;
            
            popupCanvasGroup.gameObject.SetActive(true);
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