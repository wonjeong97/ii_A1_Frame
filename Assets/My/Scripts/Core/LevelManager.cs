using System;
using System.Collections;
using System.Text.RegularExpressions;
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

            _isTutorialMode = string.Equals(levelID, "Tutorial", StringComparison.OrdinalIgnoreCase);

            // Q1 시작 시 타임랩스 데이터 초기화
            if (string.Equals(levelID, "Q1", StringComparison.OrdinalIgnoreCase))
            {
                if (TimeLapseRecorder.Instance != null)
                {
                    TimeLapseRecorder.Instance.ClearRecordingData();
                }
            }

            if (!LoadAndSetup())
            {
                Debug.LogError($"[LevelManager] Failed to initialize Level: {levelID}");
                return;
            }

            StartCoroutine(FlowRoutine());
        }

        private bool LoadAndSetup()
        {
            // 1. 공통 데이터 로드
            var commonData = JsonLoader.Load<StandardLevelSetting>("JSON/PlayCommon");
            if (commonData == null)
            {
                Debug.LogError("[LevelManager] Failed to load PlayCommon.json");
                return false; 
            }

            string path = _isTutorialMode ? "JSON/PlayTutorial" : $"JSON/Play{levelID}";

            if (_isTutorialMode)
            {
                var tutorialSetting = JsonLoader.Load<TutorialLevelSetting>(path);
                if (tutorialSetting == null) return false;

                MergeCommonData(tutorialSetting, commonData);
                SetCameraFileName(tutorialSetting.page3);
                if (pages.Length > 4 && pages[4] is Page_Camera camPageTut)
                {
                    camPageTut.Configure(false, cameraMaskMaterial);
                    Debug.Log("[LevelManager] Configured Camera for Tutorial: Save=False");
                }

                if (pages.Length > 0) pages[0].SetupData(tutorialSetting.page1);
                if (pages.Length > 1) pages[1].SetupData(tutorialSetting.page2);
                if (pages.Length > 2) pages[2].SetupData(tutorialSetting.page3);
                if (pages.Length > 3) pages[3].SetupData(tutorialSetting.page4);
                if (pages.Length > 5) pages[5].SetupData(tutorialSetting.page6);
                if (pages.Length > 6) pages[6].SetupData(tutorialSetting.page7);
            }
            else
            {
                var levelSetting = JsonLoader.Load<StandardLevelSetting>(path);
                if (levelSetting == null) return false;

                MergeCommonData(levelSetting, commonData);
                SetCameraFileName(levelSetting.page3);
                if (pages.Length > 4 && pages[4] is Page_Camera camPage)
                {
                    camPage.Configure(true, cameraMaskMaterial);
                    Debug.Log($"[LevelManager] Configured Camera for Level {levelID}: Save=True");
                }

                if (pages.Length > 0) pages[0].SetupData(levelSetting.page1);
                if (pages.Length > 1) pages[1].SetupData(levelSetting.page2);
                if (pages.Length > 2) pages[2].SetupData(levelSetting.page3);
                if (pages.Length > 3) pages[3].SetupData(levelSetting.page4);
                if (pages.Length > 5) pages[5].SetupData(levelSetting.page6);
            }

            return true;
        }

        private void MergeCommonData(TutorialLevelSetting specific, StandardLevelSetting common)
        {
            if (specific.page1 == null) specific.page1 = new GridPageData();
            if (common.page1 != null)
            {
                specific.page1.descriptionText1 = common.page1.descriptionText1;
                specific.page1.descriptionText2 = common.page1.descriptionText2;
                specific.page1.descriptionText3 = common.page1.descriptionText3;
            }

            if (specific.page2 == null) specific.page2 = new QnAPageData();
            if (common.page2 != null)
            {
                specific.page2.descriptionText = common.page2.descriptionText;
                specific.page2.answerTexts = common.page2.answerTexts;
            }

            if (specific.page3 == null) specific.page3 = new CheckPageData();
            if (common.page3 != null)
            {
                specific.page3.nicknamePlayerA = common.page3.nicknamePlayerA;
                specific.page3.nicknamePlayerB = common.page3.nicknamePlayerB;
            }

            if (specific.page4 == null) specific.page4 = new TransitionPageData();
            if (common.page4 != null)
            {
                specific.page4.descriptionText = common.page4.descriptionText;
            }

            if (specific.page6 == null) specific.page6 = new TransitionPageData();
            if (common.page6 != null)
            {
                specific.page6.descriptionText = common.page6.descriptionText;
            }
        }

        private void MergeCommonData(StandardLevelSetting specific, StandardLevelSetting common)
        {
            if (specific.page1 == null) specific.page1 = new GridPageData();
            if (common.page1 != null)
            {
                specific.page1.descriptionText1 = common.page1.descriptionText1;
                specific.page1.descriptionText2 = common.page1.descriptionText2;
                specific.page1.descriptionText3 = common.page1.descriptionText3;
            }

            if (specific.page2 == null) specific.page2 = new QnAPageData();
            if (common.page2 != null)
            {
                specific.page2.descriptionText = common.page2.descriptionText;
                specific.page2.answerTexts = common.page2.answerTexts;
            }

            if (specific.page3 == null) specific.page3 = new CheckPageData();
            if (common.page3 != null)
            {
                specific.page3.nicknamePlayerA = common.page3.nicknamePlayerA;
                specific.page3.nicknamePlayerB = common.page3.nicknamePlayerB;
            }

            if (specific.page4 == null) specific.page4 = new TransitionPageData();
            if (common.page4 != null)
            {
                specific.page4.descriptionText = common.page4.descriptionText;
            }

            if (specific.page6 == null) specific.page6 = new TransitionPageData();
            if (common.page6 != null)
            {
                specific.page6.descriptionText = common.page6.descriptionText;
            }
        }

        private void SetCameraFileName(CheckPageData checkPageData)
        {
            if (checkPageData == null || pages.Length <= 4) return;
            var cameraPage = pages[4] as Page_Camera;
            if (cameraPage == null) return;
            
            string nameA = !string.IsNullOrEmpty(checkPageData.nicknamePlayerA?.text) ? checkPageData.nicknamePlayerA.text : "PlayerA";
            string nameB = !string.IsNullOrEmpty(checkPageData.nicknamePlayerB?.text) ? checkPageData.nicknamePlayerB.text : "PlayerB";
            
            nameA = SanitizeString(nameA);
            nameB = SanitizeString(nameB);
            
            string fileName = $"{nameA}{nameB}_{levelID}";
            cameraPage.SetPhotoFilename(fileName);
        }

        private string SanitizeString(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            string clean = input.Replace("\n", "").Replace("\r", "").Replace("님", "").Trim();
            string invalidChars = Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            return Regex.Replace(clean, invalidRegStr, "");
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
            if (string.Equals(levelID, "Q15", StringComparison.OrdinalIgnoreCase))
            {
                // Q15라면 바로 넘어가지 않고, 영상 변환 대기 코루틴 실행
                StartCoroutine(ProcessVideoAndFinish());
            }
            else
            {
                // 그 외 레벨은 즉시 다음 씬으로
                TransitionToNextScene();
            }
        }

        private IEnumerator ProcessVideoAndFinish()
        {
            if (TimeLapseRecorder.Instance != null)
            {
                // 변환 요청
                TimeLapseRecorder.Instance.ConvertToVideo();

                // 상태 갱신 대기 (1프레임)
                yield return null;
                
                float timeout = 60f;
                float elapsed = 0f;
                
                // 변환 중(Processing)이라면 씬 전환을 멈추고 대기
                while (TimeLapseRecorder.Instance.IsProcessing && elapsed < timeout)
                {
                    // 필요하다면 여기에 "영상 생성 중..." 같은 UI를 띄울 수도 있음
                    Debug.Log("[LevelManager] 영상 변환 중... (씬 전환 대기)");
                    yield return new WaitForSeconds(0.5f);
                    elapsed += 0.5f;
                }

                if (elapsed >= timeout)
                {
                    Debug.LogWarning("[LevelManager] 영상 변환 타임아웃. 씬 전환을 진행합니다.");
                }

                Debug.Log("[LevelManager] 영상 변환 완료 또는 종료. 씬 전환을 시작합니다.");
            }

            // 대기 종료 후 씬 전환
            TransitionToNextScene();
        }

        private void TransitionToNextScene()
        {
            if (useFadeTransition && GameManager.Instance != null)
                GameManager.Instance.ChangeScene(nextSceneName);
            else
                SceneManager.LoadScene(nextSceneName);
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

            if (_isTutorialMode)
            {
                if (_currentPageIndex == 0 && target == 1)
                    yield return StartCoroutine(CoverTransition(current, next, info));
                else if ((_currentPageIndex == 1 && target == 2) || (_currentPageIndex == 2 && target == 3))
                    yield return StartCoroutine(RevealTransition(current, next, info));
                else if (_currentPageIndex == 3 && target == 4)
                    yield return StartCoroutine(AmjeonTransition(current, next, info));
                else if (_currentPageIndex == 4 && target == 5)
                    yield return StartCoroutine(AmjeonTransition(current, next, info, enableWhiteBg: true));
                else if (_currentPageIndex == 5 && target == 6)
                    yield return StartCoroutine(SequenceTransition(current, next, globalWhiteBackground, info, 0.5f));
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
            else
            {
                if (_currentPageIndex == 0 && target == 1)
                    yield return StartCoroutine(CoverTransition(current, next, info));
                else if ((_currentPageIndex == 1 && target == 2) || (_currentPageIndex == 2 && target == 3))
                    yield return StartCoroutine(RevealTransition(current, next, info));
                else if ((_currentPageIndex == 3 && target == 4) || (_currentPageIndex == 4 && target == 5))
                    yield return StartCoroutine(AmjeonTransition(current, next, info));
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