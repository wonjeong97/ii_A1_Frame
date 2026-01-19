using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using My.Scripts._03_Play_Q1.Pages;
using My.Scripts.Global;
using Wonjeong.Utils;
using Wonjeong.UI;

namespace My.Scripts._03_Play_Q1
{
    // [임시] 아직 생성되지 않은 페이지용 데이터 클래스
    [Serializable] public class PlayQ1Page3Data { }
    [Serializable] public class PlayQ1Page4Data { }
    [Serializable] public class PlayQ1Page5Data { }
    [Serializable] public class PlayQ1Page6Data { }

    [Serializable]
    public class PlayQ1Setting
    {
        public PlayQ1Page1Data page1;
        public PlayQ1Page2Data page2;
        public PlayQ1Page3Data page3;
        public PlayQ1Page4Data page4;
        public PlayQ1Page5Data page5;
        public PlayQ1Page6Data page6;
    }
    
    public class PlayQ1Manager : MonoBehaviour
    {
        [Header("Pages Config")]
        [SerializeField] private PlayQ1PageBase[] pages;

        private readonly string jsonFileName = GameConstants.Path.PlayQ1; 
        private PlayQ1Setting _setting;
        
        private int _currentPageIndex = -1;
        private bool _isTransitioning;
        private readonly float _fadeDuration = 0.5f;

        private void Start()
        {
            if (pages == null || pages.Length == 0)
            {
                Debug.LogWarning("[PlayQ1Manager] pages가 비어 있습니다.");
                return;
            }

            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] == null)
                {
                    Debug.LogWarning($"[PlayQ1Manager] pages[{i}]가 비어 있습니다.");
                    return;
                }
            }

            if (!LoadSettings())
            {
                return;
            }
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
                if (firstPage != null)
                {
                    firstPage.SetAlpha(1f);
                    firstPage.OnEnter();
                }
            }
        }

        private bool LoadSettings()
        {
            _setting = JsonLoader.Load<PlayQ1Setting>(jsonFileName);
            if (_setting == null)
            {
                Debug.LogWarning($"[PlayQ1Manager] JSON 로드 실패: {jsonFileName}");
                return false;
            }

            if (pages.Length > 0) pages[0].SetupData(_setting.page1);
            if (pages.Length > 1) pages[1].SetupData(_setting.page2);
            if (pages.Length > 2) pages[2].SetupData(_setting.page3);
            if (pages.Length > 3) pages[3].SetupData(_setting.page4);
            if (pages.Length > 4) pages[4].SetupData(_setting.page5);
            if (pages.Length > 5) pages[5].SetupData(_setting.page6);
            return true;
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
                    page.onStepComplete += (triggerInfo) => OnPageComplete(nextIndex, triggerInfo);
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
            Debug.Log("[PlayQ1Manager] 모든 페이지 종료.");
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

            PlayQ1PageBase currentPage = null;
            if (_currentPageIndex >= 0 && _currentPageIndex < pages.Length)
                currentPage = pages[_currentPageIndex];

            // -----------------------------------------------------------------------
            // Case 1: Page 1 -> 2
            // FadeManager를 사용한 전환 (FadeOut -> Swap -> FadeIn)
            // -----------------------------------------------------------------------
            if (_currentPageIndex == 0 && targetIndex == 1)
            {
                // 1. Fade Out (화면 어두워짐)
                if (FadeManager.Instance != null)
                {
                    bool fadeDone = false;
                    FadeManager.Instance.FadeOut(1.0f, () => fadeDone = true);
                    while (!fadeDone) yield return null;
                }
                else
                {
                    yield return new WaitForSeconds(0.5f);
                }

                // 2. 페이지 교체
                if (currentPage != null) currentPage.OnExit();
                
                _currentPageIndex = targetIndex;
                var nextPage = pages[targetIndex];

                if (nextPage != null)
                {
                    nextPage.OnEnter();
                    nextPage.SetAlpha(1f); // 검은 화면 뒤에서 보이도록 설정
                    // HandleTriggerInfo(nextPage, triggerInfo);
                }

                // 3. Fade In (화면 밝아짐)
                if (FadeManager.Instance != null)
                {
                    FadeManager.Instance.FadeIn(1.0f);
                }
            }
            // -----------------------------------------------------------------------
            // Case 3: Page 4 -> 5 (FadeManager Out -> Swap -> In)
            // -----------------------------------------------------------------------
            else if (_currentPageIndex == 3 && targetIndex == 4)
            {
                if (FadeManager.Instance != null)
                {
                    bool fadeDone = false;
                    FadeManager.Instance.FadeOut(1.0f, () => fadeDone = true);
                    while (!fadeDone) yield return null;
                }
                else
                {
                    yield return new WaitForSeconds(0.5f);
                }

                if (currentPage != null) currentPage.OnExit();
                
                _currentPageIndex = targetIndex;
                var nextPage = pages[targetIndex];

                if (nextPage != null)
                {
                    nextPage.OnEnter();
                    nextPage.SetAlpha(1f);
                    // HandleTriggerInfo(nextPage, triggerInfo);
                }

                if (FadeManager.Instance != null)
                {
                    FadeManager.Instance.FadeIn(1.0f);
                }
            }
            // -----------------------------------------------------------------------
            // Case 4: Page 5 -> 6 (Page 5 종료 시 화면 어두움 -> 교체 -> FadeIn)
            // -----------------------------------------------------------------------
            else if (_currentPageIndex == 4 && targetIndex == 5)
            {
                if (currentPage != null) currentPage.OnExit();

                _currentPageIndex = targetIndex;
                var nextPage = pages[targetIndex];

                if (nextPage != null)
                {
                    nextPage.OnEnter();
                    nextPage.SetAlpha(1f); 
                    // HandleTriggerInfo(nextPage, triggerInfo);
                }

                if (FadeManager.Instance != null)
                {
                    FadeManager.Instance.FadeIn(1.0f);
                }
                else
                {
                    yield return StartCoroutine(FadePage(nextPage, 0f, 1f));
                }
            }
            // -----------------------------------------------------------------------
            // Default: Sequential (Out -> Wait -> In)
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
                    // HandleTriggerInfo(nextPage, triggerInfo);

                    yield return StartCoroutine(FadePage(nextPage, 0f, 1f));
                }
            }

            _isTransitioning = false;
        }

        private IEnumerator FadePage(PlayQ1PageBase page, float start, float end)
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