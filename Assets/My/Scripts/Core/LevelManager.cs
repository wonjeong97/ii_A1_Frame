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
    /// <summary> 레벨 설정 데이터 인터페이스 </summary>
    public interface ILevelSetting
    {
        GridPageData Page1 { get; set; }
        QnAPageData Page2 { get; set; }
        CheckPageData Page3 { get; set; }
        TransitionPageData Page4 { get; set; }
        TransitionPageData Page6 { get; set; }
    }

    /// <summary> 일반 레벨 설정 데이터 </summary>
    [Serializable]
    public class StandardLevelSetting : ILevelSetting
    {
        public GridPageData page1;
        public QnAPageData page2;
        public CheckPageData page3;
        public TransitionPageData page4;
        public TransitionPageData page6;

        public GridPageData Page1
        {
            get => page1;
            set => page1 = value;
        }

        public QnAPageData Page2
        {
            get => page2;
            set => page2 = value;
        }

        public CheckPageData Page3
        {
            get => page3;
            set => page3 = value;
        }

        public TransitionPageData Page4
        {
            get => page4;
            set => page4 = value;
        }

        public TransitionPageData Page6
        {
            get => page6;
            set => page6 = value;
        }
    }

    /// <summary> 튜토리얼 레벨 설정 데이터 </summary>
    [Serializable]
    public class TutorialLevelSetting : ILevelSetting
    {
        public GridPageData page1;
        public QnAPageData page2;
        public CheckPageData page3;
        public TransitionPageData page4;
        public TransitionPageData page6;
        public TransitionPageData page7;

        public GridPageData Page1
        {
            get => page1;
            set => page1 = value;
        }

        public QnAPageData Page2
        {
            get => page2;
            set => page2 = value;
        }

        public CheckPageData Page3
        {
            get => page3;
            set => page3 = value;
        }

        public TransitionPageData Page4
        {
            get => page4;
            set => page4 = value;
        }

        public TransitionPageData Page6
        {
            get => page6;
            set => page6 = value;
        }
    }

    /// <summary> Q1~Q15 및 튜토리얼 씬 핵심 매니저 </summary>
    public class LevelManager : BaseFlowManager
    {
        [Header("Level Settings")] 
        [SerializeField] private string levelID = "Q2"; // 레벨 식별자
        [SerializeField] private string nextSceneName = "00_Title"; // 다음 씬 이름
        [SerializeField] private bool useFadeTransition = true; // 페이드 전환 사용 여부

        [Header("Global Backgrounds")] 
        [SerializeField] private CanvasGroup globalBlackCanvasGroup; // 전역 검은 배경
        [SerializeField] private Image globalWhiteBackground; // 전역 흰 배경

        [Header("Camera Config")] 
        [SerializeField] private Material cameraMaskMaterial; // 카메라 마스킹 재질

        private bool _isTutorialMode; // 튜토리얼 모드 여부

        /// <summary> 데이터 로드 및 설정 </summary>
        protected override void LoadSettings()
        {
            InitializeGlobals();

            // 공통 데이터 로드
            var commonData = JsonLoader.Load<StandardLevelSetting>("JSON/PlayCommon");
            if (commonData == null)
            {
                Debug.LogError("[LevelManager] PlayCommon.json 로드 실패");
                return;
            }

            string path = _isTutorialMode ? "JSON/PlayTutorial" : $"JSON/Play{levelID}";

            if (_isTutorialMode)
            {
                var tSetting = JsonLoader.Load<TutorialLevelSetting>(path);
                if (tSetting == null)
                {
                    Debug.LogError($"[LevelManager] {path}.json 로드 실패");
                    return;
                }
                
                MergeCommonData(tSetting, commonData);
                SetCameraFileName(tSetting.Page3);
                ConfigureCameraPage(false);

                SetupPageData(0, tSetting.Page1);
                SetupPageData(1, tSetting.Page2);
                SetupPageData(2, tSetting.Page3);
                SetupPageData(3, tSetting.Page4);
                SetupPageData(5, tSetting.Page6);
                SetupPageData(6, tSetting.page7);
            }
            else
            {
                var sSetting = JsonLoader.Load<StandardLevelSetting>(path);
                if (sSetting == null)
                {
                    Debug.LogError($"[LevelManager] {path}.json 로드 실패");
                    return;
                }
                
                MergeCommonData(sSetting, commonData);
                SetCameraFileName(sSetting.Page3);
                ConfigureCameraPage(true);

                SetupPageData(0, sSetting.Page1);
                SetupPageData(1, sSetting.Page2);
                SetupPageData(2, sSetting.Page3);
                SetupPageData(3, sSetting.Page4);
                SetupPageData(5, sSetting.Page6);
            }
        }

        /// <summary> 전역 객체 초기화 </summary>
        private void InitializeGlobals()
        {
            if (globalBlackCanvasGroup)
            {
                globalBlackCanvasGroup.gameObject.SetActive(true);
                globalBlackCanvasGroup.alpha = 0f;
                globalBlackCanvasGroup.blocksRaycasts = false;
            }

            if (globalWhiteBackground) globalWhiteBackground.gameObject.SetActive(false);

            _isTutorialMode = string.Equals(levelID, "Tutorial", StringComparison.OrdinalIgnoreCase);

            if (string.Equals(levelID, "Q1", StringComparison.OrdinalIgnoreCase))
            {
                if (TimeLapseRecorder.Instance != null) TimeLapseRecorder.Instance.ClearRecordingData();
            }
        }

        /// <summary> 페이지 데이터 주입 </summary>
        private void SetupPageData(int index, object data)
        {
            if (pages != null && index >= 0 && index < pages.Length && pages[index] != null)
            {
                pages[index].SetupData(data);
            }
        }

        /// <summary> 공통 데이터 병합 </summary>
        private void MergeCommonData(ILevelSetting specific, StandardLevelSetting common)
        {
            if (specific.Page1 == null) specific.Page1 = new GridPageData();
            if (common.Page1 != null)
            {
                specific.Page1.descriptionText1 = common.Page1.descriptionText1;
                specific.Page1.descriptionText2 = common.Page1.descriptionText2;
                specific.Page1.descriptionText3 = common.Page1.descriptionText3;
            }

            if (specific.Page2 == null) specific.Page2 = new QnAPageData();
            if (common.Page2 != null)
            {
                specific.Page2.descriptionText = common.Page2.descriptionText;
                specific.Page2.answerTexts = common.Page2.answerTexts;
            }

            if (specific.Page3 == null) specific.Page3 = new CheckPageData();
            if (common.Page3 != null)
            {
                specific.Page3.nicknamePlayerA = common.Page3.nicknamePlayerA;
                specific.Page3.nicknamePlayerB = common.Page3.nicknamePlayerB;
            }

            if (specific.Page4 == null) specific.Page4 = new TransitionPageData();
            if (common.Page4 != null)
            {
                specific.Page4.descriptionText = common.Page4.descriptionText;
            }

            if (specific.Page6 == null) specific.Page6 = new TransitionPageData();
            if (common.Page6 != null)
            {
                specific.Page6.descriptionText = common.Page6.descriptionText;
            }
        }
        
        /// <summary> 전체 종료 처리 </summary>
        protected override void OnAllFinished()
        {
            if (string.Equals(levelID, "Q15", StringComparison.OrdinalIgnoreCase))
            {
                StartCoroutine(ProcessVideoAndFinish());
            }
            else
            {
                TransitionToNextScene();
            }
        }

        /// <summary> 페이지 전환 연출 </summary>
        protected override IEnumerator TransitionRoutine(int targetIndex, int info)
        {
            isTransitioning = true;
            GamePage current = (currentPageIndex >= 0 && currentPageIndex < pages.Length)
                ? pages[currentPageIndex]
                : null;
            if (targetIndex < 0 || targetIndex >= pages.Length)
            {
                isTransitioning = false;
                yield break;
            }

            GamePage next = pages[targetIndex];
            bool handled = false;

            if (_isTutorialMode)
            {
                if (currentPageIndex == 0 && targetIndex == 1)
                {
                    yield return StartCoroutine(CoverTransition(current, next, info));
                    handled = true;
                }
                else if ((currentPageIndex == 1 && targetIndex == 2) || (currentPageIndex == 2 && targetIndex == 3))
                {
                    yield return StartCoroutine(RevealTransition(current, next, info));
                    handled = true;
                }
                else if (currentPageIndex == 3 && targetIndex == 4)
                {
                    yield return StartCoroutine(AmjeonTransition(current, next, info));
                    handled = true;
                }
                else if (currentPageIndex == 4 && targetIndex == 5)
                {
                    yield return StartCoroutine(AmjeonTransition(current, next, info, true));
                    handled = true;
                }
                else if (currentPageIndex == 5 && targetIndex == 6)
                {
                    yield return StartCoroutine(SequenceTransition(current, next, globalWhiteBackground, info, 0.5f));
                    handled = true;
                }
            }
            else
            {
                if (currentPageIndex == 0 && targetIndex == 1)
                {
                    yield return StartCoroutine(CoverTransition(current, next, info));
                    handled = true;
                }
                else if ((currentPageIndex == 1 && targetIndex == 2) || (currentPageIndex == 2 && targetIndex == 3))
                {
                    yield return StartCoroutine(RevealTransition(current, next, info));
                    handled = true;
                }
                else if ((currentPageIndex == 3 && targetIndex == 4) || (currentPageIndex == 4 && targetIndex == 5))
                {
                    yield return StartCoroutine(AmjeonTransition(current, next, info));
                    handled = true;
                }
            }

            if (!handled)
            {
                if (current != null)
                {
                    yield return StartCoroutine(FadePage(current, 1f, 0f));
                    current.OnExit();
                    yield return new WaitForSeconds(0.5f);
                }

                if (next != null)
                {
                    next.OnEnter();
                    HandleTrigger(next, info);
                    if (currentPageIndex == -1 && next is Page_Grid) next.SetAlpha(1f);
                    else
                    {
                        next.SetAlpha(0f);
                        yield return StartCoroutine(FadePage(next, 0f, 1f));
                    }
                }
            }

            currentPageIndex = targetIndex;
            isTransitioning = false;
        }

        /// <summary> 카메라 페이지 설정 </summary>
        private void ConfigureCameraPage(bool save)
        {
            if (pages.Length > 4 && pages[4] is Page_Camera camPage) camPage.Configure(save, cameraMaskMaterial);
        }

        /// <summary> 카메라 파일명 설정 </summary>
        private void SetCameraFileName(CheckPageData checkPageData)
        {
            if (checkPageData == null || pages.Length <= 4) return;
            var cameraPage = pages[4] as Page_Camera;
            if (cameraPage == null) return;
            string nameA = !string.IsNullOrEmpty(checkPageData.nicknamePlayerA?.text)
                ? checkPageData.nicknamePlayerA.text
                : "PlayerA";
            string nameB = !string.IsNullOrEmpty(checkPageData.nicknamePlayerB?.text)
                ? checkPageData.nicknamePlayerB.text
                : "PlayerB";
            nameA = SanitizeString(nameA);
            nameB = SanitizeString(nameB);
            cameraPage.SetPhotoFilename($"{nameA}{nameB}_{levelID}");
        }

        /// <summary> 문자열 특수문자 제거 </summary>
        private string SanitizeString(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            string clean = input.Replace("\n", "").Replace("\r", "").Replace("님", "").Trim();
            string invalidChars = Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            return Regex.Replace(clean, invalidRegStr, "");
        }

        /// <summary> 영상 변환 대기 및 종료 </summary>
        private IEnumerator ProcessVideoAndFinish()
        {
            if (TimeLapseRecorder.Instance != null)
            {
                TimeLapseRecorder.Instance.ConvertToVideo();
                yield return null;
                float timeout = 60f, elapsed = 0f;
                while (TimeLapseRecorder.Instance.IsProcessing && elapsed < timeout)
                {
                    Debug.Log("[LevelManager] 영상 변환 중...");
                    yield return new WaitForSeconds(0.5f);
                    elapsed += 0.5f;
                }
            }

            TransitionToNextScene();
        }

        /// <summary> 다음 씬 이동 </summary>
        private void TransitionToNextScene()
        {
            if (useFadeTransition && GameManager.Instance != null) GameManager.Instance.ChangeScene(nextSceneName);
            else SceneManager.LoadScene(nextSceneName);
        }

        /// <summary> 페이지 트리거 처리 </summary>
        private void HandleTrigger(GamePage page, int info)
        {
            if (info != 0 && page is Page_Check checkPage) checkPage.ActivatePlayerCheck(info == 1);
        }

        /// <summary> 커버 전환 연출 </summary>
        private IEnumerator CoverTransition(GamePage current, GamePage next, int info)
        {
            if (globalBlackCanvasGroup != null)
                yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 0f, 1f, 0.5f));
            yield return new WaitForSeconds(0.5f);
            if (current) current.OnExit();
            if (next)
            {
                next.OnEnter();
                next.SetAlpha(0f);
                HandleTrigger(next, info);
            }

            if (next) yield return StartCoroutine(FadePage(next, 0f, 1f));
            if (globalBlackCanvasGroup != null)
                yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 1f, 0f, 0.5f));
        }

        /// <summary> 리빌 전환 연출 </summary>
        private IEnumerator RevealTransition(GamePage current, GamePage next, int info)
        {
            if (globalBlackCanvasGroup != null) globalBlackCanvasGroup.alpha = 1f;
            if (current)
            {
                yield return StartCoroutine(FadePage(current, 1f, 0f));
                current.OnExit();
            }

            if (next)
            {
                next.OnEnter();
                next.SetAlpha(0f);
                HandleTrigger(next, info);
                yield return StartCoroutine(FadePage(next, 0f, 1f));
            }

            if (globalBlackCanvasGroup != null)
                yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 1f, 0f, 0.5f));
        }

        /// <summary> 암전 전환 연출 </summary>
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
            if (enableWhiteBg && globalWhiteBackground)
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

        /// <summary> 순차 전환 연출 </summary>
        private IEnumerator SequenceTransition(GamePage current, GamePage next, Image background, int info,
            float waitTime = 0f)
        {
            if (background) background.gameObject.SetActive(true);
            if (current)
            {
                yield return StartCoroutine(FadePage(current, 1f, 0f));
                current.OnExit();
            }

            if (waitTime > 0f) yield return new WaitForSeconds(waitTime);
            if (next)
            {
                next.OnEnter();
                next.SetAlpha(0f);
                HandleTrigger(next, info);
                yield return StartCoroutine(FadePage(next, 0f, 1f));
            }
        }

        /// <summary> 캔버스 그룹 페이드 </summary>
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