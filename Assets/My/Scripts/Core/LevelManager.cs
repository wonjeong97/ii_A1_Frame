using System;
using System.Collections;
using My.Scripts.Core.Data;
using My.Scripts.Core.Pages;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core
{
    // [Standard: Q1 ~ Q15 (6 Pages)]
    [Serializable]
    public class StandardLevelSetting
    {
        public GridPageData page1; // Grid
        public QnAPageData page2; // QnA
        public CheckPageData page3; // Check

        public TransitionPageData page4; // Ready

        // Page 5: Camera (No Data)
        public TransitionPageData page6; // End
    }

    // [Tutorial: 7 Pages]
    [Serializable]
    public class TutorialLevelSetting
    {
        public GridPageData page1;
        public QnAPageData page2;
        public CheckPageData page3;

        public TransitionPageData page4;

        // Page 5: Camera
        public TransitionPageData page6; // End 1
        public TransitionPageData page7; // End 2 (Next Intro)
    }

    public class LevelManager : MonoBehaviour
    {
        [Header("Level Settings")]
        [SerializeField] private string levelID = "Q2"; // Tutorial, Q1, Q2 ...
        [SerializeField] private string nextSceneName = "00_Title";
        [SerializeField] private bool useFadeTransition = true;

        [Header("Pages")] 
        [SerializeField] private GamePage[] pages;

        [Header("Global Backgrounds")] 
        [SerializeField] private CanvasGroup globalBlackCanvasGroup;
        [SerializeField] private Image globalWhiteBackground;

        [Header("Camera Config")] 
        [SerializeField] private Material cameraMaskMaterial; // Q1~Q15용 마스크

        private int _currentPageIndex = -1;
        private bool _isTransitioning;
        private bool _isTutorialMode;

        private void Start()
        {
            if (globalBlackCanvasGroup != null)
            {
                globalBlackCanvasGroup.gameObject.SetActive(true);
                globalBlackCanvasGroup.alpha = 0f;
                globalBlackCanvasGroup.blocksRaycasts = false;
            }

            if (globalWhiteBackground != null) globalWhiteBackground.gameObject.SetActive(false);

            _isTutorialMode = (levelID == "Tutorial");

            if (!LoadAndSetup())
            {
                Debug.LogError($"[LevelManager] Failed to initialize Level: {levelID}");
                return;
            }

            StartCoroutine(FlowRoutine());
        }

        private bool LoadAndSetup()
        {
            string path = _isTutorialMode ? "JSON/PlayTutorial" : $"JSON/Play{levelID}";

            // 공통: 카메라 설정
            int camIdx = 4; // Page 5 (Index 4)
            if (pages.Length > camIdx && pages[camIdx] is Page_Camera cam)
            {
                cam.SetPhotoFilename($"아영길동_{levelID}");

                if (_isTutorialMode) cam.Configure(shouldSave: false, maskMat: null);
                else cam.Configure(shouldSave: true, maskMat: cameraMaskMaterial);
            }

            if (_isTutorialMode)
            {
                var setting = JsonLoader.Load<TutorialLevelSetting>(path);
                if (setting == null) return false;

                if (pages.Length > 0) pages[0].SetupData(setting.page1);
                if (pages.Length > 1) pages[1].SetupData(setting.page2);
                if (pages.Length > 2) pages[2].SetupData(setting.page3);
                if (pages.Length > 3) pages[3].SetupData(setting.page4);
                if (pages.Length > 5) pages[5].SetupData(setting.page6);
                if (pages.Length > 6) pages[6].SetupData(setting.page7);
            }
            else
            {
                // Standard (Q1 ~ Q15)
                var setting = JsonLoader.Load<StandardLevelSetting>(path);
                if (setting == null) return false;

                if (pages.Length > 0) pages[0].SetupData(setting.page1);
                if (pages.Length > 1) pages[1].SetupData(setting.page2);
                if (pages.Length > 2) pages[2].SetupData(setting.page3);
                if (pages.Length > 3) pages[3].SetupData(setting.page4);
                if (pages.Length > 5) pages[5].SetupData(setting.page6);
            }

            return true;
        }

        private void InitializePages()
        {
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] == null) continue;
                pages[i].gameObject.SetActive(false);
                pages[i].SetAlpha(0f);
                int next = i + 1;
                pages[i].onStepComplete += (info) => OnPageComplete(next, info);
            }
        }

        private IEnumerator FlowRoutine()
        {
            InitializePages();
            yield return null;

            if (pages.Length > 0)
            {
                _currentPageIndex = 0;
                var firstPage = pages[0];
                if (firstPage != null)
                {
                    firstPage.OnEnter();
                    firstPage.SetAlpha(1f);
                }
            }
        }

        private void OnPageComplete(int nextIndex, int info)
        {
            if (nextIndex < pages.Length) TransitionToPage(nextIndex, info);
            else OnAllFinished();
        }

        private void OnAllFinished()
        {
            if (useFadeTransition && GameManager.Instance != null) GameManager.Instance.ChangeScene(nextSceneName);
            else SceneManager.LoadScene(nextSceneName);
        }

        private void TransitionToPage(int target, int info = 0)
        {
            if (_isTransitioning) return;
            StartCoroutine(TransitionStep(target, info));
        }

        private IEnumerator TransitionStep(int target, int info)
        {
            _isTransitioning = true;
            GamePage current = (_currentPageIndex >= 0) ? pages[_currentPageIndex] : null;
            GamePage next = pages[target];

            // --------------------------------------------------------------------------------
            // [Tutorial Mode] (7 Pages)
            // --------------------------------------------------------------------------------
            if (_isTutorialMode)
            {
                // P1 -> P2 (Cover)
                if (_currentPageIndex == 0 && target == 1)
                    yield return StartCoroutine(CoverTransition(current, next, info));

                // P2 -> P3 -> P4 (Reveal)
                else if ((_currentPageIndex == 1 && target == 2) || (_currentPageIndex == 2 && target == 3))
                    yield return StartCoroutine(RevealTransition(current, next, info));

                // P4 -> P5 (Amjeon)
                else if (_currentPageIndex == 3 && target == 4)
                    yield return StartCoroutine(AmjeonTransition(current, next, info));

                // P5(Camera) -> P6(End1) (Amjeon + WhiteBG On)
                else if (_currentPageIndex == 4 && target == 5)
                    yield return StartCoroutine(AmjeonTransition(current, next, info, enableWhiteBg: true));

                // P6(End1) -> P7(NewIntro) (Sequence on WhiteBG)
                else if (_currentPageIndex == 5 && target == 6)
                    yield return StartCoroutine(SequenceTransition(current, next, globalWhiteBackground, info, 0.5f));

                // 예외 경로(Fallback): 정의되지 않은 구간은 기본 페이드 전환
                else
                {
                    if (current)
                    {
                        yield return StartCoroutine(FadePage(current, 1f, 0f));
                        current.OnExit();
                    }

                    yield return new WaitForSeconds(0.5f);

                    _currentPageIndex = target;
                    if (next != null)
                    {
                        next.OnEnter();
                        next.SetAlpha(0f);
                        HandleTrigger(next, info);
                        yield return StartCoroutine(FadePage(next, 0f, 1f));
                    }
                }
            }
            // --------------------------------------------------------------------------------
            // [Standard Mode: Q1 ~ Q15] (6 Pages)
            // --------------------------------------------------------------------------------
            else
            {
                // P1(Grid) -> P2(QnA) (Cover)
                if (_currentPageIndex == 0 && target == 1)
                    yield return StartCoroutine(CoverTransition(current, next, info));

                // P2 -> P3 -> P4 (Reveal)
                else if ((_currentPageIndex == 1 && target == 2) || (_currentPageIndex == 2 && target == 3))
                    yield return StartCoroutine(RevealTransition(current, next, info));

                // P4 -> P5 -> P6 (Amjeon)
                else if ((_currentPageIndex == 3 && target == 4) || (_currentPageIndex == 4 && target == 5))
                    yield return StartCoroutine(AmjeonTransition(current, next, info));

                // Fallback
                else
                {
                    if (current)
                    {
                        yield return StartCoroutine(FadePage(current, 1f, 0f));
                        current.OnExit();
                    }

                    yield return new WaitForSeconds(0.5f);
                    _currentPageIndex = target;
                    if (next != null)
                    {
                        next.OnEnter();
                        next.SetAlpha(0f);
                        HandleTrigger(next, info);
                        yield return StartCoroutine(FadePage(next, 0f, 1f));
                    }
                }
            }

            _currentPageIndex = target;
            _isTransitioning = false;
        }

        // [Transitions]
        private IEnumerator CoverTransition(GamePage current, GamePage next, int info)
        {
            if (globalBlackCanvasGroup != null)
                yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 0f, 1f, 0.5f));
            yield return new WaitForSeconds(0.5f);

            if (current != null) current.OnExit();

            _currentPageIndex = Array.IndexOf(pages, next);
            if (next != null)
            {
                next.OnEnter();
                next.SetAlpha(0f);
                HandleTrigger(next, info);
            }

            if (next != null) yield return StartCoroutine(FadePage(next, 0f, 1f));
            if (globalBlackCanvasGroup != null)
                yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 1f, 0f, 0.5f));
        }

        private IEnumerator RevealTransition(GamePage current, GamePage next, int info)
        {
            if (globalBlackCanvasGroup != null) globalBlackCanvasGroup.alpha = 1f;
            if (current != null)
            {
                yield return StartCoroutine(FadePage(current, 1f, 0f));
                current.OnExit();
            }

            _currentPageIndex = Array.IndexOf(pages, next);
            if (next != null)
            {
                next.OnEnter();
                next.SetAlpha(0f);
                HandleTrigger(next, info);
                yield return StartCoroutine(FadePage(next, 0f, 1f));
            }

            if (globalBlackCanvasGroup != null)
                yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 1f, 0f, 0.5f));
        }

        private IEnumerator AmjeonTransition(GamePage current, GamePage next, int info, bool enableWhiteBg = false)
        {
            if (FadeManager.Instance)
            {
                bool d = false;
                FadeManager.Instance.FadeOut(1f, () => d = true);
                while (!d) yield return null;
            }
            else yield return new WaitForSeconds(0.5f);

            if (current) current.OnExit();
            _currentPageIndex = Array.IndexOf(pages, next);

            if (enableWhiteBg && globalWhiteBackground != null)
            {
                globalWhiteBackground.gameObject.SetActive(true);
                Color c = globalWhiteBackground.color;
                c.a = 1f;
                globalWhiteBackground.color = c;
            }

            if (next)
            {
                next.OnEnter();
                next.SetAlpha(1f);
                HandleTrigger(next, info);
            }

            if (FadeManager.Instance) FadeManager.Instance.FadeIn(1f);
        }

        private IEnumerator SequenceTransition(GamePage current, GamePage next, Image background, int info,
            float waitTime = 0f)
        {
            if (background != null) background.gameObject.SetActive(true);
            if (current != null)
            {
                yield return StartCoroutine(FadePage(current, 1f, 0f));
                current.OnExit();
            }

            if (waitTime > 0f) yield return new WaitForSeconds(waitTime);
            _currentPageIndex = Array.IndexOf(pages, next);
            if (next != null)
            {
                next.OnEnter();
                next.SetAlpha(0f);
                HandleTrigger(next, info);
                yield return StartCoroutine(FadePage(next, 0f, 1f));
            }
        }

        private void HandleTrigger(GamePage page, int info)
        {
            if (info == 0) return;
            if (page is Page_Check checkPage) checkPage.ActivatePlayerCheck(info == 1);
        }

        private IEnumerator FadePage(GamePage page, float s, float e)
        {
            if (!page) yield break;
            float t = 0f;
            page.SetAlpha(s);
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                page.SetAlpha(Mathf.Lerp(s, e, t / 0.5f));
                yield return null;
            }

            page.SetAlpha(e);
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float s, float e, float d)
        {
            if (!cg) yield break;
            float t = 0f;
            cg.alpha = s;
            while (t < d)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(s, e, t / d);
                yield return null;
            }

            cg.alpha = e;
        }
    }
}