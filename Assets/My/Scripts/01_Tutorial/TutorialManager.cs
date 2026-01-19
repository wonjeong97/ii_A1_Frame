using System;
using System.Collections;
using My.Scripts._01_Tutorial.Pages;
using My.Scripts.Global;
using UnityEngine;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial
{
    // JSON 전체 구조
    [Serializable]
    public class TutorialSetting
    {
        public TutorialPage1Data page1;
        public TutorialPage2Data page2;
        public TutorialPage3Data page3;
        public TutorialPage4Data page4;
        public TutorialPage5Data page5;
        public TutorialPage6Data page6;
    }

    public class TutorialManager : MonoBehaviour
    {
        [Header("Pages Config")]
        [SerializeField] private TutorialPageBase[] pages;

        private readonly string jsonFileName = GameConstants.Path.Tutorial;
        private TutorialSetting _setting;
        
        private int _currentPageIndex = -1;
        private bool _isTransitioning;
        private readonly float _fadeDuration = 0.5f;

        private void Start()
        {
            LoadSettings();
            InitializePages();
            
            StartCoroutine(StartTutorialFlow());
        }
        
        private IEnumerator StartTutorialFlow()
        {
            yield return null; 
            if (pages.Length > 0)
            {
                TransitionToPage(0);
            }
        }

        private void LoadSettings()
        {
            _setting = JsonLoader.Load<TutorialSetting>(jsonFileName);
            if (_setting == null)
            {
                Debug.LogError($"[TutorialManager] JSON Load Failed: {jsonFileName}");
                return;
            }

            // 페이지별 데이터 주입
            if (pages.Length > 0) pages[0].SetupData(_setting.page1);
            if (pages.Length > 1) pages[1].SetupData(_setting.page2);
            if (pages.Length > 2) pages[2].SetupData(_setting.page3);
            if (pages.Length > 3) pages[3].SetupData(_setting.page4);
            if (pages.Length > 4) pages[4].SetupData(_setting.page5);
            if (pages.Length > 5) pages[5].SetupData(_setting.page6);
        }

        private void InitializePages()
        {
            for (int i = 0; i < pages.Length; i++)
            {
                var page = pages[i];
                if (page != null)
                {
                    page.OnExit();
                    page.SetAlpha(0f);
                    
                    // 각 페이지 완료 시 다음 페이지로 이동
                    int nextIndex = i + 1;
                    page.OnStepComplete += (triggerInfo) => OnPageComplete(nextIndex, triggerInfo);
                }
            }
        }

        // 페이지 완료 시 호출되는 함수
        private void OnPageComplete(int nextIndex, int triggerInfo)
        {
            if (nextIndex < pages.Length)
            {
                // 다음 튜토리얼 페이지로 이동
                TransitionToPage(nextIndex, triggerInfo);
            }
            else
            {   
                // ---------------------------------------------------------
                // 모든 튜토리얼 종료 -> 화면 페이드 아웃 후 Game 씬 로드
                // ---------------------------------------------------------
                if (FadeManager.Instance != null)
                {
                    // 1초 동안 페이드 아웃
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.ChangeScene(GameConstants.Scene.PlayTutorial);
                    }
                }
                else
                {
                    Debug.LogWarning("FadeManager가 없습니다. 즉시 종료 처리합니다.");
                    // GameManager.Instance.ReturnToTitle();
                }
            }
        }

        private void TransitionToPage(int targetIndex, int triggerInfo = 0)
        {
            if (_isTransitioning) return;
            StartCoroutine(TransitionRoutine(targetIndex, triggerInfo));
        }

        private IEnumerator TransitionRoutine(int targetIndex, int triggerInfo)
        {
            _isTransitioning = true;

            // 1. 현재 페이지 Fade Out (페이지 UI만 투명해짐)
            if (_currentPageIndex >= 0 && _currentPageIndex < pages.Length)
            {
                var currentPage = pages[_currentPageIndex];
                yield return StartCoroutine(FadePage(currentPage, 1f, 0f));
                currentPage.OnExit();
            }

            // 2. 다음 페이지 준비
            _currentPageIndex = targetIndex;
            var nextPage = pages[targetIndex];

            nextPage.OnEnter();
            HandleTriggerInfo(nextPage, triggerInfo);

            // 3. 다음 페이지 Fade In
            yield return StartCoroutine(FadePage(nextPage, 0f, 1f));

            _isTransitioning = false;
        }

        private IEnumerator FadePage(TutorialPageBase page, float start, float end)
        {
            float timer = 0f;
            page.SetAlpha(start);
            
            while (timer < _fadeDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / _fadeDuration;
                page.SetAlpha(Mathf.Lerp(start, end, progress));
                yield return null;
            }
            page.SetAlpha(end);
        }

        private void HandleTriggerInfo(TutorialPageBase page, int triggerInfo)
        {
            if (triggerInfo == 0) return;

            // Page 3 불 켜기 로직 등 특수 처리
            if (page is TutorialPage3Controller p3)
            {
                if (triggerInfo == 1) p3.ActivatePlayerCheck(true);
                else if (triggerInfo == 2) p3.ActivatePlayerCheck(false);
            }
        }
    }
}