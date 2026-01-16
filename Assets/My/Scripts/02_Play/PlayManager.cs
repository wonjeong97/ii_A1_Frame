using System;
using System.Collections;
using My.Scripts._02_Play.Pages;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wonjeong.Utils; // FadeManager 접근용
using Wonjeong.UI;

namespace My.Scripts._02_Play
{
    [Serializable]
    public class PlaySetting
    {
        public PlayPage1Data page1;
        public PlayPage2Data page2;
        public PlayPage3Data page3;
        public PlayPage4Data page4;
        public PlayPage5Data page5;
    }
    
    public class PlayManager : MonoBehaviour
    {
        [Header("Pages Config")]
        [SerializeField] private PlayPageBase[] pages;

        private readonly string jsonFileName = GameConstants.Path.Play; 
        private PlaySetting _setting;
        
        private int _currentPageIndex = -1;
        private bool _isTransitioning;
        private readonly float _fadeDuration = 0.5f;

        private void Start()
        {
            LoadSettings();
            InitializePages();
            StartCoroutine(StartPlayFlow());
        }
        
        private IEnumerator StartPlayFlow()
        {
            yield return null; 
            if (pages != null && pages.Length > 0)
            {
                _currentPageIndex = 0;
                var firstPage = pages[0];
                if (firstPage == null) yield break;
                
                firstPage.SetAlpha(1f);
                firstPage.OnEnter();
            }
        }

        private void LoadSettings()
        {
            _setting = JsonLoader.Load<PlaySetting>(jsonFileName);
            if (_setting == null) return;

            if (pages.Length > 0) pages[0].SetupData(_setting.page1);
            if (pages.Length > 1) pages[1].SetupData(_setting.page2);
            if (pages.Length > 2) pages[2].SetupData(_setting.page3);
            if (pages.Length > 3) pages[3].SetupData(_setting.page4);
            if (pages.Length > 4) pages[4].SetupData(_setting.page5);
        }

        private void InitializePages()
        {
            for (int i = 0; i < pages.Length; i++)
            {
                var page = pages[i];
                if (page != null)
                {
                    page.gameObject.SetActive(false);
                    page.SetAlpha(0f);
                    
                    int nextIndex = i + 1;
                    page.OnStepComplete += (triggerInfo) => OnPageComplete(nextIndex, triggerInfo);
                }
            }
        }

        private void OnPageComplete(int nextIndex, int triggerInfo)
        {
            if (nextIndex < pages.Length)
            {
                TransitionToPage(nextIndex, triggerInfo);
            }
            else
            {
                OnAllPagesFinished();
            }
        }

        private void OnAllPagesFinished()
        {
            Debug.Log("[PlayManager] 모든 Play 페이지 종료.");
            if (GameManager.Instance != null)
                GameManager.Instance.ReturnToTitle();
            else
                SceneManager.LoadScene(GameConstants.Scene.Title);
        }

        private void TransitionToPage(int targetIndex, int triggerInfo = 0)
        {
            if (_isTransitioning) return;
            if (targetIndex < 0 || targetIndex >= pages.Length) return;
            StartCoroutine(TransitionRoutine(targetIndex, triggerInfo));
        }

        private IEnumerator TransitionRoutine(int targetIndex, int triggerInfo)
        {
            _isTransitioning = true;

            PlayPageBase currentPage = null;
            if (_currentPageIndex >= 0 && _currentPageIndex < pages.Length)
                currentPage = pages[_currentPageIndex];

            // -----------------------------------------------------------------------
            // Case 1: Page 1 -> 2 (Index 0 -> 1)
            // 배경 깜빡임 방지용 Overlap (동시 페이드)
            // -----------------------------------------------------------------------
            if (_currentPageIndex == 0 && targetIndex == 1)
            {
                _currentPageIndex = targetIndex;
                var nextPage = pages[targetIndex];
                
                if (nextPage != null)
                {
                    nextPage.OnEnter();
                    nextPage.SetAlpha(0f);
                    HandleTriggerInfo(nextPage, triggerInfo);

                    yield return StartCoroutine(FadePage(nextPage, 0f, 1f));
                }

                if (currentPage != null)
                {
                    yield return StartCoroutine(FadePage(currentPage, 1f, 0f));
                    currentPage.OnExit();
                }
            }
            // -----------------------------------------------------------------------
            // Case 3: Page 4 -> 5 (Index 3 -> 4) 
            // [요청사항] FadeManager 사용 (전체 화면 검게 전환)
            // -----------------------------------------------------------------------
            else if (_currentPageIndex == 3 && targetIndex == 4)
            {
                // 1. Fade Manager: Fade Out (검은 화면)
                if (FadeManager.Instance != null)
                {
                    bool fadeDone = false;
                    FadeManager.Instance.FadeOut(1.0f, () => fadeDone = true);
                    while (!fadeDone) yield return null;
                }
                else
                {
                    // Fallback (FadeManager 없을 시)
                    yield return new WaitForSeconds(0.5f);
                }

                // 2. 페이지 교체 (Page 4 Off -> Page 5 On)
                if (currentPage != null) currentPage.OnExit();
                
                _currentPageIndex = targetIndex;
                var nextPage = pages[targetIndex];

                if (nextPage != null)
                {
                    nextPage.OnEnter();
                    nextPage.SetAlpha(1f); // 검은 화면 뒤에서 이미 보이도록 설정
                    HandleTriggerInfo(nextPage, triggerInfo);
                }

                // 3. Fade Manager: Fade In (화면 밝아짐)
                if (FadeManager.Instance != null)
                {
                    FadeManager.Instance.FadeIn(1.0f);
                }
            }
            // -----------------------------------------------------------------------
            // Case 2: 그 외 일반 전환 (2->3, 3->4 등)
            // 순차 전환: Out(0.5s) -> Wait(1s) -> In(0.5s)
            // -----------------------------------------------------------------------
            else
            {
                if (currentPage != null)
                {
                    yield return StartCoroutine(FadePage(currentPage, 1f, 0f));
                    currentPage.OnExit();
                }

                yield return new WaitForSeconds(1.0f);

                _currentPageIndex = targetIndex;
                var nextPage = pages[targetIndex];

                if (nextPage != null)
                {
                    nextPage.OnEnter();
                    nextPage.SetAlpha(0f);
                    HandleTriggerInfo(nextPage, triggerInfo);

                    yield return StartCoroutine(FadePage(nextPage, 0f, 1f));
                }
            }

            _isTransitioning = false;
        }

        private void HandleTriggerInfo(PlayPageBase page, int triggerInfo)
        {
            if (triggerInfo == 0) return;

            if (page is PlayPage3Controller p3)
            {
                if (triggerInfo == 1) p3.ActivatePlayerCheck(true);  
                else if (triggerInfo == 2) p3.ActivatePlayerCheck(false); 
            }
        }

        private IEnumerator FadePage(PlayPageBase page, float start, float end)
        {
            float timer = 0f;
            page.SetAlpha(start);
            while (timer < _fadeDuration)
            {
                timer += Time.deltaTime;
                page.SetAlpha(Mathf.Lerp(start, end, timer / _fadeDuration));
                yield return null;
            }
            page.SetAlpha(end);
        }
    }
}