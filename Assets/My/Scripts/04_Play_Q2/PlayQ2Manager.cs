using System;
using System.Collections;
using My.Scripts._04_Play_Q2.Pages;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._04_Play_Q2
{
    [Serializable]
    public class PlayQ2Setting
    {
        public PlayQ2Page1Data page1;
        public PlayQ2Page2Data page2;
        public PlayQ2Page3Data page3;
        public PlayQ2Page4Data page4;
        public PlayQ2Page5Data page5; // Camera (Masking)
        public PlayQ2Page6Data page6; // Ending
    }

    public class PlayQ2Manager : MonoBehaviour
    {
        [Header("Pages Config")] [SerializeField]
        private PlayQ2PageBase[] pages;

        private readonly string jsonFileName = GameConstants.Path.PlayQ2;
        private PlayQ2Setting _setting;

        private int _currentPageIndex = -1;
        private bool _isTransitioning;
        private readonly float _fadeDuration = 0.5f;

        private void Start()
        {
            if (pages == null || pages.Length == 0) return;
            if (!LoadSettings()) return;

            InitializePages();
            StartCoroutine(StartPlayFlow());
        }

        private IEnumerator StartPlayFlow()
        {
            yield return null;
            if (pages.Length > 0)
            {
                TransitionToPage(0);
            }
        }

        private bool LoadSettings()
        {
            _setting = JsonLoader.Load<PlayQ2Setting>(jsonFileName);
            if (_setting == null)
            {
                Debug.LogWarning($"[PlayQ2Manager] JSON Load Failed: {jsonFileName}");
                return false;
            }

            if (pages.Length > 0) pages[0].SetupData(_setting.page1);
            if (pages.Length > 1) pages[1].SetupData(_setting.page2);
            if (pages.Length > 2) pages[2].SetupData(_setting.page3);
            if (pages.Length > 3) pages[3].SetupData(_setting.page4);
            if (pages.Length > 4) pages[4].SetupData(_setting.page5);
            if (pages.Length > 5) pages[5].SetupData(_setting.page6);

            // 카메라 파일명 설정
            if (pages.Length > 4 && pages[4] is PlayQ2Page5Controller cameraPage)
            {
                cameraPage.SetPhotoFilename("아영길동_Q2");
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
            if (nextIndex < pages.Length) TransitionToPage(nextIndex, triggerInfo);
            else OnAllPagesFinished();
        }

        private void OnAllPagesFinished()
        {
            Debug.Log("[PlayQ2Manager] 종료. Title로 이동.");
            if (GameManager.Instance != null) GameManager.Instance.ReturnToTitle();
            else SceneManager.LoadScene(GameConstants.Scene.Title);
        }

        private void TransitionToPage(int targetIndex, int triggerInfo = 0)
        {
            if (_isTransitioning) return;
            StartCoroutine(TransitionRoutine(targetIndex, triggerInfo));
        }

        private IEnumerator TransitionRoutine(int targetIndex, int triggerInfo)
        {
            _isTransitioning = true;
            PlayQ2PageBase currentPage = (_currentPageIndex >= 0 && _currentPageIndex < pages.Length)
                ? pages[_currentPageIndex]
                : null;

            // 1. P1 -> P2 (Intro -> Game): Overlap
            if (_currentPageIndex == 0 && targetIndex == 1)
            {
                _currentPageIndex = targetIndex;
                var nextPage = pages[targetIndex];
                if (nextPage)
                {
                    nextPage.OnEnter();
                    nextPage.SetAlpha(0f);
                    HandleTriggerInfo(nextPage, triggerInfo);
                    yield return StartCoroutine(FadePage(nextPage, 0f, 1f));
                }

                if (currentPage)
                {
                    yield return StartCoroutine(FadePage(currentPage, 1f, 0f));
                    currentPage.OnExit();
                }
            }
            // 2. P4 -> P5 (Button -> Camera): Fade Black
            else if (_currentPageIndex == 3 && targetIndex == 4)
            {
                if (FadeManager.Instance)
                {
                    bool d = false;
                    FadeManager.Instance.FadeOut(1f, () => d = true);
                    while (!d) yield return null;
                }
                else yield return new WaitForSeconds(0.5f);

                if (currentPage) currentPage.OnExit();

                _currentPageIndex = targetIndex;
                var nextPage = pages[targetIndex];
                if (nextPage)
                {
                    nextPage.OnEnter();
                    nextPage.SetAlpha(1f);
                    HandleTriggerInfo(nextPage, triggerInfo);
                }

                if (FadeManager.Instance) FadeManager.Instance.FadeIn(1f);
            }
            // 3. P5 -> P6 (Camera -> Ending): Already Faded Out by Camera
            else if (_currentPageIndex == 4 && targetIndex == 5)
                else if (_currentPageIndex == 4 && targetIndex == 5)
            {
                if (currentPage) currentPage.OnExit();
                _currentPageIndex = targetIndex;
                var nextPage = pages[targetIndex];
                if (nextPage)
                {
                    nextPage.OnEnter();
                    HandleTriggerInfo(nextPage, triggerInfo);
                    if (FadeManager.Instance)
                    {
                        nextPage.SetAlpha(1f);
                        FadeManager.Instance.FadeIn(1f);
                    }
                    else
                    {
                        nextPage.SetAlpha(0f);
                        yield return StartCoroutine(FadePage(nextPage, 0f, 1f));
                    }
                }
            }
            // 4. Default Sequential
            else
            {
                if (currentPage)
                {
                    yield return StartCoroutine(FadePage(currentPage, 1f, 0f));
                    currentPage.OnExit();
                }

                yield return new WaitForSeconds(1.0f);
                _currentPageIndex = targetIndex;
                var nextPage = pages[targetIndex];
                if (nextPage)
                {
                    nextPage.OnEnter();
                    nextPage.SetAlpha(0f);
                    HandleTriggerInfo(nextPage, triggerInfo);
                    yield return StartCoroutine(FadePage(nextPage, 0f, 1f));
                }
            }

            _isTransitioning = false;
        }

        private void HandleTriggerInfo(PlayQ2PageBase page, int info)
        {
            if (info == 0) return;
            if (page is PlayQ2Page3Controller p3)
            {
                if (info == 1) p3.ActivatePlayerCheck(true);
                else if (info == 2) p3.ActivatePlayerCheck(false);
            }
        }

        private IEnumerator FadePage(PlayQ2PageBase page, float s, float e)
        {
            float t = 0f;
            page.SetAlpha(s);
            while (t < _fadeDuration)
            {
                t += Time.deltaTime;
                page.SetAlpha(Mathf.Lerp(s, e, t / _fadeDuration));
                yield return null;
            }

            page.SetAlpha(e);
        }
    }
}