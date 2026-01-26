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
    // [기존 데이터 클래스 유지]
    [Serializable]
    public class StandardLevelSetting
    {
        public GridPageData page1;
        public QnAPageData page2;
        public CheckPageData page3;
        public TransitionPageData page4;
        // Page 5: Camera (No Data)
        public TransitionPageData page6;
    }

    [Serializable]
    public class TutorialLevelSetting
    {
        public GridPageData page1;
        public QnAPageData page2;
        public CheckPageData page3;
        public TransitionPageData page4;
        // Page 5: Camera
        public TransitionPageData page6;
        public TransitionPageData page7;
    }

    // [수정] BaseFlowManager 상속
    public class LevelManager : BaseFlowManager
    {
        [Header("Level Settings")]
        [SerializeField] private string levelID = "Q2";
        [SerializeField] private string nextSceneName = "00_Title";
        [SerializeField] private bool useFadeTransition = true;

        [Header("Global Backgrounds")] 
        [SerializeField] private CanvasGroup globalBlackCanvasGroup;
        [SerializeField] private Image globalWhiteBackground;

        [Header("Camera Config")]
        [SerializeField] private Material cameraMaskMaterial;

        private bool _isTutorialMode;

        // Start는 BaseFlowManager에서 호출됨 -> 로드 -> 초기화 -> 시작

        protected override void LoadSettings()
        {
            // 초기 설정
            if (globalBlackCanvasGroup)
            {
                globalBlackCanvasGroup.gameObject.SetActive(true);
                globalBlackCanvasGroup.alpha = 0f;
                globalBlackCanvasGroup.blocksRaycasts = false;
            }
            if (globalWhiteBackground) globalWhiteBackground.gameObject.SetActive(false);

            _isTutorialMode = string.Equals(levelID, "Tutorial", StringComparison.OrdinalIgnoreCase);

            // Q1 타임랩스 초기화
            if (string.Equals(levelID, "Q1", StringComparison.OrdinalIgnoreCase))
            {
                if (TimeLapseRecorder.Instance != null) TimeLapseRecorder.Instance.ClearRecordingData();
            }

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
                if (tSetting != null)
                {
                    MergeCommonData(tSetting, commonData);
                    SetCameraFileName(tSetting.page3);
                    ConfigureCameraPage(false);

                    // [핵심] 제네릭 덕분에 setupData 호출이 매우 간결해짐
                    // 순서에 맞춰 데이터 주입 (null 체크는 GamePage 내부에서 안전하게 처리됨)
                    if (pages.Length > 0) pages[0].SetupData(tSetting.page1);
                    if (pages.Length > 1) pages[1].SetupData(tSetting.page2);
                    if (pages.Length > 2) pages[2].SetupData(tSetting.page3);
                    if (pages.Length > 3) pages[3].SetupData(tSetting.page4);
                    // Page 5 Camera는 데이터 없음
                    if (pages.Length > 5) pages[5].SetupData(tSetting.page6);
                    if (pages.Length > 6) pages[6].SetupData(tSetting.page7);
                }
            }
            else
            {
                var sSetting = JsonLoader.Load<StandardLevelSetting>(path);
                if (sSetting != null)
                {
                    MergeCommonData(sSetting, commonData);
                    SetCameraFileName(sSetting.page3);
                    ConfigureCameraPage(true);

                    if (pages.Length > 0) pages[0].SetupData(sSetting.page1);
                    if (pages.Length > 1) pages[1].SetupData(sSetting.page2);
                    if (pages.Length > 2) pages[2].SetupData(sSetting.page3);
                    if (pages.Length > 3) pages[3].SetupData(sSetting.page4);
                    if (pages.Length > 5) pages[5].SetupData(sSetting.page6);
                }
            }
        }

        // [구현] 모든 단계 종료 시
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

        // [오버라이드] LevelManager만의 특수한 전환 효과(Transition) 적용
protected override IEnumerator TransitionRoutine(int targetIndex, int info)
        {
            isTransitioning = true;
            GamePage current = (currentPageIndex >= 0 && currentPageIndex < pages.Length) ? pages[currentPageIndex] : null;
            GamePage next = (targetIndex < pages.Length) ? pages[targetIndex] : null;

            bool handled = false;

            // 튜토리얼 및 일반 모드 특수 연출 체크
            if (_isTutorialMode)
            {
                if (currentPageIndex == 0 && targetIndex == 1) { yield return StartCoroutine(CoverTransition(current, next, info)); handled = true; }
                else if ((currentPageIndex == 1 && targetIndex == 2) || (currentPageIndex == 2 && targetIndex == 3)) { yield return StartCoroutine(RevealTransition(current, next, info)); handled = true; }
                else if (currentPageIndex == 3 && targetIndex == 4) { yield return StartCoroutine(AmjeonTransition(current, next, info)); handled = true; }
                else if (currentPageIndex == 4 && targetIndex == 5) { yield return StartCoroutine(AmjeonTransition(current, next, info, true)); handled = true; }
                else if (currentPageIndex == 5 && targetIndex == 6) { yield return StartCoroutine(SequenceTransition(current, next, globalWhiteBackground, info, 0.5f)); handled = true; }
            }
            else
            {
                if (currentPageIndex == 0 && targetIndex == 1) { yield return StartCoroutine(CoverTransition(current, next, info)); handled = true; }
                else if ((currentPageIndex == 1 && targetIndex == 2) || (currentPageIndex == 2 && targetIndex == 3)) { yield return StartCoroutine(RevealTransition(current, next, info)); handled = true; }
                else if ((currentPageIndex == 3 && targetIndex == 4) || (currentPageIndex == 4 && targetIndex == 5)) { yield return StartCoroutine(AmjeonTransition(current, next, info)); handled = true; }
            }

            // 기본 페이드 처리
            if (!handled)
            {
                // [1] 현재 페이지 퇴장 (있을 경우만)
                if (current != null) 
                { 
                    yield return StartCoroutine(FadePage(current, 1f, 0f)); 
                    current.OnExit(); 
                    yield return new WaitForSeconds(0.5f); // 페이지 간 간격 (퇴장할 때만)
                }
                
                // [2] 다음 페이지 등장
                if (next != null)
                {
                    next.OnEnter();
                    HandleTrigger(next, info);

                    // [핵심 수정] 첫 진입(Grid)인 경우 페이드 없이 즉시 Alpha 1로 설정
                    // 조건: 현재 페이지가 없음(-1) AND 다음 페이지가 Page_Grid임
                    if (currentPageIndex == -1 && next is Page_Grid)
                    {
                        next.SetAlpha(1f);
                    }
                    else
                    {
                        // 그 외의 경우는 정상적으로 페이드 인
                        next.SetAlpha(0f);
                        yield return StartCoroutine(FadePage(next, 0f, 1f));
                    }
                }
            }
            
            currentPageIndex = targetIndex;
            isTransitioning = false;
        }

        // ---------------- Helper Methods (기존 로직 유지) ---------------- //

        private void ConfigureCameraPage(bool save)
        {
            if (pages.Length > 4 && pages[4] is Page_Camera camPage)
            {
                camPage.Configure(save, cameraMaskMaterial);
            }
        }

        private void MergeCommonData(TutorialLevelSetting specific, StandardLevelSetting common)
        {
            if (specific.page1 == null) specific.page1 = new GridPageData();
            if (common.page1 != null) { specific.page1.descriptionText1 = common.page1.descriptionText1; specific.page1.descriptionText2 = common.page1.descriptionText2; specific.page1.descriptionText3 = common.page1.descriptionText3; }
            if (specific.page2 == null) specific.page2 = new QnAPageData();
            if (common.page2 != null) { specific.page2.descriptionText = common.page2.descriptionText; specific.page2.answerTexts = common.page2.answerTexts; }
            if (specific.page3 == null) specific.page3 = new CheckPageData();
            if (common.page3 != null) { specific.page3.nicknamePlayerA = common.page3.nicknamePlayerA; specific.page3.nicknamePlayerB = common.page3.nicknamePlayerB; }
            if (specific.page4 == null) specific.page4 = new TransitionPageData();
            if (common.page4 != null) { specific.page4.descriptionText = common.page4.descriptionText; }
            if (specific.page6 == null) specific.page6 = new TransitionPageData();
            if (common.page6 != null) { specific.page6.descriptionText = common.page6.descriptionText; }
        }
        
        // Standard용 오버로딩
        private void MergeCommonData(StandardLevelSetting specific, StandardLevelSetting common)
        {
             // (위와 동일한 로직, 타입만 다름) - 기존 코드 복사 사용
            if (specific.page1 == null) specific.page1 = new GridPageData();
            if (common.page1 != null) { specific.page1.descriptionText1 = common.page1.descriptionText1; specific.page1.descriptionText2 = common.page1.descriptionText2; specific.page1.descriptionText3 = common.page1.descriptionText3; }
            if (specific.page2 == null) specific.page2 = new QnAPageData();
            if (common.page2 != null) { specific.page2.descriptionText = common.page2.descriptionText; specific.page2.answerTexts = common.page2.answerTexts; }
            if (specific.page3 == null) specific.page3 = new CheckPageData();
            if (common.page3 != null) { specific.page3.nicknamePlayerA = common.page3.nicknamePlayerA; specific.page3.nicknamePlayerB = common.page3.nicknamePlayerB; }
            if (specific.page4 == null) specific.page4 = new TransitionPageData();
            if (common.page4 != null) { specific.page4.descriptionText = common.page4.descriptionText; }
            if (specific.page6 == null) specific.page6 = new TransitionPageData();
            if (common.page6 != null) { specific.page6.descriptionText = common.page6.descriptionText; }
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
            cameraPage.SetPhotoFilename($"{nameA}{nameB}_{levelID}");
        }

        private string SanitizeString(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            string clean = input.Replace("\n", "").Replace("\r", "").Replace("님", "").Trim();
            string invalidChars = Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            return Regex.Replace(clean, invalidRegStr, "");
        }

        // Q15용 타임랩스 대기 로직
        private IEnumerator ProcessVideoAndFinish()
        {
            if (TimeLapseRecorder.Instance != null)
            {
                TimeLapseRecorder.Instance.ConvertToVideo();
                yield return null;
                float timeout = 60f, elapsed = 0f;
                while (TimeLapseRecorder.Instance.IsProcessing && elapsed < timeout)
                {
                    Debug.Log("[LevelManager] 영상 변환 중... (씬 전환 대기)");
                    yield return new WaitForSeconds(0.5f);
                    elapsed += 0.5f;
                }
            }
            TransitionToNextScene();
        }

        private void TransitionToNextScene()
        {
            if (useFadeTransition && GameManager.Instance != null) GameManager.Instance.ChangeScene(nextSceneName);
            else SceneManager.LoadScene(nextSceneName);
        }

        private void HandleTrigger(GamePage page, int info)
        {
            if (info != 0 && page is Page_Check checkPage) checkPage.ActivatePlayerCheck(info == 1);
        }

        // --------------- Transition Effects --------------- //
        private IEnumerator CoverTransition(GamePage current, GamePage next, int info)
        {
            if (globalBlackCanvasGroup != null) yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 0f, 1f, 0.5f));
            yield return new WaitForSeconds(0.5f);
            if (current) current.OnExit();
            if (next) { next.OnEnter(); next.SetAlpha(0f); HandleTrigger(next, info); }
            if (next) yield return StartCoroutine(FadePage(next, 0f, 1f));
            if (globalBlackCanvasGroup != null) yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 1f, 0f, 0.5f));
        }

        private IEnumerator RevealTransition(GamePage current, GamePage next, int info)
        {
            if (globalBlackCanvasGroup != null) globalBlackCanvasGroup.alpha = 1f;
            if (current) { yield return StartCoroutine(FadePage(current, 1f, 0f)); current.OnExit(); }
            if (next) { next.OnEnter(); next.SetAlpha(0f); HandleTrigger(next, info); yield return StartCoroutine(FadePage(next, 0f, 1f)); }
            if (globalBlackCanvasGroup != null) yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 1f, 0f, 0.5f));
        }

        private IEnumerator AmjeonTransition(GamePage current, GamePage next, int info, bool enableWhiteBg = false)
        {
            if (FadeManager.Instance) { bool d = false; FadeManager.Instance.FadeOut(1f, () => d = true); while (!d) yield return null; }
            else yield return new WaitForSeconds(0.5f);
            if (current) current.OnExit();
            if (enableWhiteBg && globalWhiteBackground) { globalWhiteBackground.gameObject.SetActive(true); Color c = globalWhiteBackground.color; c.a = 1f; globalWhiteBackground.color = c; }
            if (next) { next.OnEnter(); next.SetAlpha(1f); HandleTrigger(next, info); }
            if (FadeManager.Instance) FadeManager.Instance.FadeIn(1f);
        }

        private IEnumerator SequenceTransition(GamePage current, GamePage next, Image background, int info, float waitTime = 0f)
        {
            if (background) background.gameObject.SetActive(true);
            if (current) { yield return StartCoroutine(FadePage(current, 1f, 0f)); current.OnExit(); }
            if (waitTime > 0f) yield return new WaitForSeconds(waitTime);
            if (next) { next.OnEnter(); next.SetAlpha(0f); HandleTrigger(next, info); yield return StartCoroutine(FadePage(next, 0f, 1f)); }
        }
        
        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float s, float e, float d)
        {
            if (!cg) yield break;
            float t = 0f; cg.alpha = s;
            while (t < d) { t += Time.deltaTime; cg.alpha = Mathf.Lerp(s, e, t / d); yield return null; }
            cg.alpha = e;
        }
    }
}