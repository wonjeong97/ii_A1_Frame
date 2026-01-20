using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using My.Scripts._03_Play_Q1.Pages;
using My.Scripts.Global;
using Wonjeong.Utils;
using Wonjeong.UI;

namespace My.Scripts._03_Play_Q1
{
    [Serializable]
    public class PlayQ1Setting
    {
        public PlayQ1Page1Data page1;
        public PlayQ1Page2Data page2;
        public PlayQ1Page3Data page3;
        public PlayQ1Page4Data page4;
        public PlayQ1Page5Data page5;
        public PlayQ1Page6Data page6;
        public PlayQ1Page7Data page7;
    }
    
    public class PlayQ1Manager : MonoBehaviour
    {
        [Header("Pages Config")]
        [SerializeField] private PlayQ1PageBase[] pages;

        [Header("Global UI")]
        [SerializeField] private Image globalBackground;
        [SerializeField] private Image globalBlackBackground;

        private readonly string jsonFileName = GameConstants.Path.PlayQ1; 
        private PlayQ1Setting _setting;
        
        private int _currentPageIndex = -1;
        private bool _isTransitioning;
        private readonly float _fadeDuration = 0.5f;

        private void Start()
        {
            if (globalBlackBackground != null) 
                globalBlackBackground.gameObject.SetActive(false);

            if (pages == null || pages.Length == 0) return;

            if (!LoadSettings()) return;
            
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

            // 페이지별 데이터 세팅
            if (pages.Length > 0) pages[0].SetupData(_setting.page1);
            if (pages.Length > 1) pages[1].SetupData(_setting.page2);
            if (pages.Length > 2) pages[2].SetupData(_setting.page3);
            if (pages.Length > 3) pages[3].SetupData(_setting.page4);
            if (pages.Length > 4) pages[4].SetupData(_setting.page5);
            if (pages.Length > 5) pages[5].SetupData(_setting.page6);
            if (pages.Length > 6) pages[6].SetupData(_setting.page7);

            // ---------------------------------------------------------
            // [추가] 사진 파일명 설정 (임시 하드코딩)
            // ---------------------------------------------------------
            // 추후 API 연동 시 JSON 데이터 등을 활용하여 동적으로 변경할 예정
            // 현재는 "아영길동_Q1"으로 고정
            if (pages.Length > 5 && pages[5] is PlayQ1Page6Controller cameraPage)
            {
                string finalFileName = "아영길동_Q1"; 
                cameraPage.SetPhotoFilename(finalFileName);
                Debug.Log($"[PlayQ1Manager] Filename Set: {finalFileName}");
            }

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
            // Case 1: Page 1 -> 2 (Intro -> Game)
            // -----------------------------------------------------------------------
            if (_currentPageIndex == 0 && targetIndex == 1)
            {
                if (FadeManager.Instance != null)
                {
                    bool fadeDone = false;
                    FadeManager.Instance.FadeOut(1.0f, () => fadeDone = true);
                    while (!fadeDone) yield return null;
                }
                else yield return new WaitForSeconds(0.5f);

                if (currentPage != null) currentPage.OnExit();
                
                _currentPageIndex = targetIndex;
                var nextPage = pages[targetIndex];

                if (nextPage != null)
                {
                    nextPage.OnEnter();
                    nextPage.SetAlpha(1f); 
                }

                if (globalBlackBackground != null && globalBackground != null)
                {   
                    globalBackground.gameObject.SetActive(false);
                    globalBlackBackground.gameObject.SetActive(true);
                } 

                if (FadeManager.Instance != null) FadeManager.Instance.FadeIn(1.0f);
            }
            // -----------------------------------------------------------------------
            // Case 2: Page 2 -> 3 (Game -> Q&A) - Overlap
            // -----------------------------------------------------------------------
            else if (_currentPageIndex == 1 && targetIndex == 2)
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
            // Case 4: Page 5 -> 6 (Ready -> Camera) - FadeManager
            // -----------------------------------------------------------------------
            else if (_currentPageIndex == 4 && targetIndex == 5)
            {
                if (FadeManager.Instance != null)
                {
                    bool fadeDone = false;
                    FadeManager.Instance.FadeOut(1.0f, () => fadeDone = true);
                    while (!fadeDone) yield return null;
                }
                else yield return new WaitForSeconds(0.5f);

                if (currentPage != null) currentPage.OnExit();
                
                _currentPageIndex = targetIndex;
                var nextPage = pages[targetIndex];

                if (nextPage != null)
                {
                    nextPage.OnEnter();
                    nextPage.SetAlpha(1f); 
                    HandleTriggerInfo(nextPage, triggerInfo);
                }

                if (FadeManager.Instance != null) FadeManager.Instance.FadeIn(1.0f);
                else yield return StartCoroutine(FadePage(nextPage, 0f, 1f));
            }
            // -----------------------------------------------------------------------
            // Case 5: Page 6 -> 7 (Camera -> End) - Pre-faded
            // -----------------------------------------------------------------------
            else if (_currentPageIndex == 5 && targetIndex == 6)
            {
                if (currentPage != null) currentPage.OnExit();

                _currentPageIndex = targetIndex;
                var nextPage = pages[targetIndex];

                if (nextPage != null)
                {
                    nextPage.OnEnter();
                    nextPage.SetAlpha(1f); 
                    HandleTriggerInfo(nextPage, triggerInfo);
                }

                if (FadeManager.Instance != null) FadeManager.Instance.FadeIn(1.0f);
                else yield return StartCoroutine(FadePage(nextPage, 0f, 1f));
            }
            // -----------------------------------------------------------------------
            // Default: Sequential
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

        private void HandleTriggerInfo(PlayQ1PageBase page, int triggerInfo)
        {
            if (triggerInfo == 0) return;

            if (page is PlayQ1Page4Controller p4)
            {
                if (triggerInfo == 1) p4.ActivatePlayerCheck(true);  
                else if (triggerInfo == 2) p4.ActivatePlayerCheck(false); 
            }
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