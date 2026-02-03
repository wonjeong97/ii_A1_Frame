using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core; 
using My.Scripts.Global; // GameManager 접근용
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;
using UnityEngine.SceneManagement;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage4Data
    {
        public TextSetting nicknamePlayerA;
        public TextSetting nicknamePlayerB;
        
        public string warningMessage; 
        public string resetMessage;   
    }

    /// <summary> 튜토리얼 4페이지 컨트롤러 (플레이어 점등 체크 + 리셋 팝업) </summary>
    public class TutorialPage4Controller : GamePage<TutorialPage4Data>
    {
        [Header("Page 4 UI")]
        [SerializeField] private Text nicknameA; 
        [SerializeField] private Text nicknameB; 
        [SerializeField] private Image imgBackA; 
        [SerializeField] private Image imgLightA; 
        [SerializeField] private Image imgBackB; 
        [SerializeField] private Image imgLightB; 

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

        private bool isLightOnA; 
        private bool isLightOnB; 
        private bool _completionStarted; 

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

        /// <summary> 데이터 설정 (닉네임 및 메시지 적용) </summary>
        protected override void SetupData(TutorialPage4Data data)
        {
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);

            // 메시지 적용
            if (!string.IsNullOrEmpty(data.warningMessage)) _msgWarning = data.warningMessage;
            if (!string.IsNullOrEmpty(data.resetMessage)) _msgReset = data.resetMessage;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            // 상태 리셋
            isLightOnA = false;
            isLightOnB = false;
            _completionStarted = false;
            
            StopResetSequence();
            _currentIdleTime = 0f;
            
            // 이미지 초기화
            SetImageAlpha(imgBackA, 1f);
            SetImageAlpha(imgLightA, 0f);
            SetImageAlpha(imgBackB, 1f);
            SetImageAlpha(imgLightB, 0f);
        }

        public override void OnExit()
        {
            base.OnExit();
            StopResetSequence();
        }

        private void Update()
        {
            if (_completionStarted) return; // 완료 시퀀스 중이면 무시

            // 1. 입력 감지
            if (Input.anyKey || Input.touchCount > 0)
            {
                // 리셋 중단
                if (_isResetSequenceActive || _currentIdleTime > 0f)
                {
                    StopResetSequence();
                }

                // 테스트용 키 입력 (기존 로직)
                if (Input.GetKeyDown(KeyCode.Alpha1)) ActivatePlayerCheck(true);
                if (Input.GetKeyDown(KeyCode.Alpha2)) ActivatePlayerCheck(false);
            }
            else
            {
                // 2. 비활성 시간 누적 (완료되지 않았을 때만)
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

        /// <summary> 플레이어 체크 활성화 (외부 호출 가능) </summary>
        public void ActivatePlayerCheck(bool isPlayerA)
        {
            // 외부(Manager 등)에서 호출될 경우를 대비해 여기서도 리셋 중단 처리
            if (_isResetSequenceActive || _currentIdleTime > 0f)
            {
                StopResetSequence();
            }

            if (isPlayerA)
            {
                if (isLightOnA) return; 
                isLightOnA = true;
                StartCoroutine(TransitionCheckImage(imgBackA, imgLightA));
            }
            else
            {
                if (isLightOnB) return;
                isLightOnB = true;
                StartCoroutine(TransitionCheckImage(imgBackB, imgLightB));
            }
            
            if (isLightOnA && isLightOnB)
            {
                if (!_completionStarted)
                {
                    _completionStarted = true;
                    StartCoroutine(WaitAndComplete());
                }
            }
        }

        // --- 리셋 로직 (Page 1, 3와 동일) ---

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
            Debug.Log("[TutorialPage4] 리셋 시퀀스 시작");

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

        // --- 기존 연출 로직 ---

        private IEnumerator WaitAndComplete()
        {
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            CompleteStep(); 
        }

        private IEnumerator TransitionCheckImage(Image backImage, Image lightImage)
        {
            if (backImage == null || lightImage == null) yield break;

            float timer = 0f;
            float duration = 0.3f;
            
            Color backColor = backImage.color;
            Color lightColor = lightImage.color;
            
            lightColor.a = 0f;
            lightImage.color = lightColor;
            lightImage.gameObject.SetActive(true);

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float progress = timer / duration;
                
                backColor.a = Mathf.Lerp(1f, 0f, progress);
                backImage.color = backColor;
                lightColor.a = Mathf.Lerp(0f, 1f, progress);
                lightImage.color = lightColor;
                
                yield return null;
            }
            
            backColor.a = 0f;
            backImage.color = backColor;
            lightColor.a = 1f;
            lightImage.color = lightColor;
        }

        private void SetImageAlpha(Image img, float alpha)
        {
            if (img == null) return;
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }
}