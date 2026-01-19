using System;
using System.Collections;
using My.Scripts._02_Play_Tutorial.Pages;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._02_Play_Tutorial
{
    [Serializable]
    public class PlaySetting
    {
        public PlayTutorialPage1Data page1;
        public PlayTutorialPage2Data page2;
        public PlayTutorialPage3Data page3;
        public PlayTutorialPage4Data page4;
        public PlayTutorialPage5Data page5;
        public PlayTutorialPage6Data page6;
    }
    
    public class PlayTutorialManager : MonoBehaviour
    {
        [Header("Pages Config")]
        [SerializeField] private PlayTutorialPageBase[] pages;

        private readonly string jsonFileName = GameConstants.Path.PlayTutorial; 
        private PlaySetting _setting;
        
        private int _currentPageIndex = -1;
        private bool _isTransitioning;
        private readonly float _fadeDuration = 0.5f;

        private void Start()
        {
            if (pages == null || pages.Length == 0)
            {
                Debug.LogWarning("[PlayTutorialManager] pages가 비어 있습니다.");
                return;
            }

            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] == null)
                {
                    Debug.LogWarning($"[PlayTutorialManager] pages[{i}]가 비어 있습니다.");
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
                if (firstPage == null) yield break;
                
                firstPage.SetAlpha(1f);
                firstPage.OnEnter();
            }
        }

        private bool LoadSettings()
        {
            _setting = JsonLoader.Load<PlaySetting>(jsonFileName);
            if (_setting == null)
            {
                Debug.LogWarning($"[PlayTutorialManager] JSON 로드 실패: {jsonFileName}");
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
            Debug.Log("[PlayManager] 모든 Play Tutorial 페이지 종료.");
            SceneManager.LoadScene(GameConstants.Scene.PlayQ1);
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

            PlayTutorialPageBase currentTutorialPage = null;
            if (_currentPageIndex >= 0 && _currentPageIndex < pages.Length)
                currentTutorialPage = pages[_currentPageIndex];

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

                if (currentTutorialPage != null)
                {
                    yield return StartCoroutine(FadePage(currentTutorialPage, 1f, 0f));
                    currentTutorialPage.OnExit();
                }
            }
            // -----------------------------------------------------------------------
            // Case 3: Page 4 -> 5 (Index 3 -> 4) 
            // [검은 화면 전환] FadeManager 사용
            // -----------------------------------------------------------------------
            else if (_currentPageIndex == 3 && targetIndex == 4)
            {
                // 1. Fade Out (검은 화면)
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
                if (currentTutorialPage != null) currentTutorialPage.OnExit();
                
                _currentPageIndex = targetIndex;
                var nextPage = pages[targetIndex];

                if (nextPage != null)
                {
                    nextPage.OnEnter();
                    nextPage.SetAlpha(1f); 
                    HandleTriggerInfo(nextPage, triggerInfo);
                }

                // 3. Fade In
                if (FadeManager.Instance != null)
                {
                    FadeManager.Instance.FadeIn(1.0f);
                }
            }
            // -----------------------------------------------------------------------
            // [추가] Case 4: Page 5 -> 6 (Index 4 -> 5)
            // Page 5가 끝날 때 이미 FadeManager로 화면이 어두워진 상태입니다.
            // -----------------------------------------------------------------------
            else if (_currentPageIndex == 4 && targetIndex == 5)
            {
                // 1. 이전 페이지(5) 종료
                // (Page 5 Controller에서 이미 FadeOut을 호출하고 완료한 상태)
                if (currentTutorialPage != null) currentTutorialPage.OnExit();

                // 2. 다음 페이지(6) 준비
                _currentPageIndex = targetIndex;
                var nextPage = pages[targetIndex];

                if (nextPage != null)
                {
                    nextPage.OnEnter();
                    // 검은 화면 뒤에서 컨텐츠가 보일 준비를 해야 하므로 Alpha 1
                    nextPage.SetAlpha(1f); 
                    HandleTriggerInfo(nextPage, triggerInfo);
                }

                // 3. 화면 밝히기 (Fade In)
                if (FadeManager.Instance != null)
                {
                    FadeManager.Instance.FadeIn(1.0f);
                }
                else if (nextPage != null)
                {
                    // Fallback: FadeManager가 없으면 캔버스 그룹 페이드인
                    yield return StartCoroutine(FadePage(nextPage, 0f, 1f));
                }
            }
            // -----------------------------------------------------------------------
            // Case 2: 그 외 일반 전환 (2->3 등)
            // 순차 전환: Out -> Wait -> In
            // -----------------------------------------------------------------------
            else
            {
                if (currentTutorialPage != null)
                {
                    yield return StartCoroutine(FadePage(currentTutorialPage, 1f, 0f));
                    currentTutorialPage.OnExit();
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

        private void HandleTriggerInfo(PlayTutorialPageBase tutorialPage, int triggerInfo)
        {
            if (triggerInfo == 0) return;

            if (tutorialPage is PlayTutorialPage3Controller p3)
            {
                if (triggerInfo == 1) p3.ActivatePlayerCheck(true);  
                else if (triggerInfo == 2) p3.ActivatePlayerCheck(false); 
            }
        }

        private IEnumerator FadePage(PlayTutorialPageBase tutorialPage, float start, float end)
        {
            float timer = 0f;
            tutorialPage.SetAlpha(start);
            while (timer < _fadeDuration)
            {
                timer += Time.deltaTime;
                tutorialPage.SetAlpha(Mathf.Lerp(start, end, timer / _fadeDuration));
                yield return null;
            }
            tutorialPage.SetAlpha(end);
        }
    }
}