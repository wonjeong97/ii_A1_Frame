using System;
using System.Collections;
using System.Text.RegularExpressions;
using My.Scripts.Core.Data;
using My.Scripts.Core.Pages;
using My.Scripts.Timelapse;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core
{
    /// <summary> 
    /// 레벨 설정 데이터의 공통 인터페이스입니다.
    /// 일반 레벨과 튜토리얼 레벨이 서로 다른 페이지 구성을 가지더라도, 공통된 페이지(1,2,3,4,6)에 접근하기 위해 사용합니다.
    /// </summary>
    public interface ILevelSetting
    {
        GridPageData Page1 { get; set; }
        QnAPageData Page2 { get; set; }
        CheckPageData Page3 { get; set; }
        TransitionPageData Page4 { get; set; }
        TransitionPageData Page6 { get; set; }
    }

    /// <summary> 
    /// 일반 레벨(Q1 ~ Q15)의 설정 데이터 구조입니다.
    /// JSON 파일과 1:1로 매핑됩니다.
    /// </summary>
    [Serializable]
    public class StandardLevelSetting : ILevelSetting
    {
        public GridPageData page1;
        public QnAPageData page2;
        public CheckPageData page3;
        public TransitionPageData page4;
        public TransitionPageData page6;
        
        public GridPageData Page1 { get => page1; set => page1 = value; }
        public QnAPageData Page2 { get => page2; set => page2 = value; }
        public CheckPageData Page3 { get => page3; set => page3 = value; }
        public TransitionPageData Page4 { get => page4; set => page4 = value; }
        public TransitionPageData Page6 { get => page6; set => page6 = value; }
    }

    /// <summary> 
    /// 튜토리얼 레벨의 설정 데이터 구조입니다.
    /// 일반 레벨보다 페이지 수가 더 많고 구조가 다르므로 별도 클래스로 관리합니다.
    /// </summary>
    [Serializable]
    public class TutorialLevelSetting : ILevelSetting
    {
        public GridPageData page1;
        public QnAPageData page2;
        public CheckPageData page3;
        public TransitionPageData page4;
        public TransitionPageData page5; 
        public TransitionPageData page6;
        public TransitionPageData page7;
        public TutorialPage8Data page8;

        public GridPageData Page1 { get => page1; set => page1 = value; }
        public QnAPageData Page2 { get => page2; set => page2 = value; }
        public CheckPageData Page3 { get => page3; set => page3 = value; }
        public TransitionPageData Page4 { get => page4; set => page4 = value; }
        public TransitionPageData Page6 { get => page6; set => page6 = value; }
    }

    /// <summary> 
    /// 개별 레벨(Q1~Q15 및 튜토리얼)의 전체 흐름을 관리하는 매니저입니다.
    /// JSON 데이터 로드, 페이지 간의 특수 전환 연출(Cover, Reveal 등), 그리고 최종 영상 변환 트리거를 담당합니다.
    /// </summary>
    public class LevelManager : BaseFlowManager
    {
        [Header("Level Settings")] 
        [SerializeField] private string levelID = "Q2"; // 현재 레벨 식별자 (JSON 파일명 매핑용)
        [SerializeField] private string nextSceneName = "00_Title"; 
        [SerializeField] private bool useFadeTransition = true; 

        [Header("Global Backgrounds")] 
        [SerializeField] private CanvasGroup globalBlackCanvasGroup; // 전환 연출용 전역 검은 배경
        [SerializeField] private Image globalWhiteBackground; // 전환 연출용 전역 흰 배경

        [Header("Camera Config")] 
        [SerializeField] private Material cameraMaskMaterial; 

        private bool _isTutorialMode; 

        protected override void LoadSettings()
        {
            InitializeGlobals();

            // 모든 레벨에서 공통으로 사용하는 텍스트나 설정을 로드합니다.
            // 이를 통해 각 레벨 JSON 파일의 중복 데이터를 줄입니다.
            var commonData = JsonLoader.Load<StandardLevelSetting>("JSON/PlayCommon");
            if (commonData == null)
            {
                Debug.LogError("[LevelManager] PlayCommon.json 로드 실패");
                return;
            }

            string path = _isTutorialMode ? "JSON/PlayTutorial" : $"JSON/Play{levelID}";

            // 튜토리얼과 일반 레벨의 데이터 구조가 다르므로 분기 처리
            if (_isTutorialMode)
            {
                var tSetting = JsonLoader.Load<TutorialLevelSetting>(path);
                
                // 개별 설정에 누락된 값이 있다면 공통 데이터로 채워 넣습니다.
                MergeCommonData(tSetting, commonData);

                SetCameraFileName(tSetting.Page3);
                ConfigureCameraPage(false);

                // 페이지 배열 인덱스에 맞춰 데이터 주입
                SetupPageData(0, tSetting.Page1);
                SetupPageData(1, tSetting.Page2);
                SetupPageData(2, tSetting.Page3);
                SetupPageData(3, tSetting.Page4);
                SetupPageData(5, tSetting.Page6);
                SetupPageData(6, tSetting.page7);
                SetupPageData(7, tSetting.page8);
            }
            else
            {
                var sSetting = JsonLoader.Load<StandardLevelSetting>(path);
                
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

            // 첫 번째 질문(Q1) 시작 시, 이전 플레이의 잔여 타임랩스 데이터가 있다면 정리합니다.
            if (string.Equals(levelID, "Q1", StringComparison.OrdinalIgnoreCase))
            {
                if (TimeLapseRecorder.Instance != null) TimeLapseRecorder.Instance.ClearRecordingData();
            }
        }

        private void SetupPageData(int index, object data)
        {
            if (pages != null && index >= 0 && index < pages.Length && pages[index] != null)
            {
                pages[index].SetupData(data);
            }
        }

        /// <summary>
        /// 레벨별 고유 설정이 없는 경우 공통 설정(PlayCommon)값으로 덮어씁니다.
        /// 데이터 입력 작업의 효율성을 높이기 위함입니다.
        /// </summary>
        private void MergeCommonData(ILevelSetting specific, StandardLevelSetting common)
        {
            // [Page 1: Grid]
            if (specific.Page1 == null) specific.Page1 = new GridPageData();
            if (common.Page1 != null)
            {
                // 값이 없거나(null) 비어있을 때만 공통 데이터를 사용하도록 변경.
                if (specific.Page1.descriptionText1 == null || string.IsNullOrEmpty(specific.Page1.descriptionText1.text)) 
                    specific.Page1.descriptionText1 = common.Page1.descriptionText1;
                
                if (specific.Page1.descriptionText2 == null || string.IsNullOrEmpty(specific.Page1.descriptionText2.text)) 
                    specific.Page1.descriptionText2 = common.Page1.descriptionText2;
                
                if (specific.Page1.descriptionText3 == null || string.IsNullOrEmpty(specific.Page1.descriptionText3.text)) 
                    specific.Page1.descriptionText3 = common.Page1.descriptionText3;
                
                if (string.IsNullOrEmpty(specific.Page1.warningMessage)) specific.Page1.warningMessage = common.Page1.warningMessage;
                if (string.IsNullOrEmpty(specific.Page1.resetMessage)) specific.Page1.resetMessage = common.Page1.resetMessage;
            }

            // [Page 2: QnA]
            if (common.Page2 != null)
            {
                if (specific.Page2.descriptionText == null || string.IsNullOrEmpty(specific.Page2.descriptionText.text)) specific.Page2.descriptionText = common.Page2.descriptionText;
                if (specific.Page2.answerTexts == null || specific.Page2.answerTexts.Length == 0) specific.Page2.answerTexts = common.Page2.answerTexts;
                
                if (string.IsNullOrEmpty(specific.Page2.warningMessage)) specific.Page2.warningMessage = common.Page2.warningMessage;
                if (string.IsNullOrEmpty(specific.Page2.resetMessage)) specific.Page2.resetMessage = common.Page2.resetMessage;
            }

            // [Page 3: Check]
            if (specific.Page3 == null) specific.Page3 = new CheckPageData();
            if (common.Page3 != null)
            {
                if (specific.Page3.nicknamePlayerA == null) specific.Page3.nicknamePlayerA = common.Page3.nicknamePlayerA;
                if (specific.Page3.nicknamePlayerB == null) specific.Page3.nicknamePlayerB = common.Page3.nicknamePlayerB;
                if (specific.Page3.waitText == null) specific.Page3.waitText = common.Page3.waitText;
                
                if (string.IsNullOrEmpty(specific.Page3.warningMessage)) specific.Page3.warningMessage = common.Page3.warningMessage;
                if (string.IsNullOrEmpty(specific.Page3.resetMessage)) specific.Page3.resetMessage = common.Page3.resetMessage;
            }

            // [Page 4: Transition]
            if (specific.Page4 == null) specific.Page4 = new TransitionPageData();
            if (common.Page4 != null)
            {
                if (specific.Page4.descriptionText == null) specific.Page4.descriptionText = common.Page4.descriptionText;
                
                if (string.IsNullOrEmpty(specific.Page4.warningMessage)) specific.Page4.warningMessage = common.Page4.warningMessage;
                if (string.IsNullOrEmpty(specific.Page4.resetMessage)) specific.Page4.resetMessage = common.Page4.resetMessage;
            }

            // [Page 6: Transition]
            if (specific.Page6 == null) specific.Page6 = new TransitionPageData();
            if (common.Page6 != null)
            {
                if (specific.Page6.descriptionText == null) specific.Page6.descriptionText = common.Page6.descriptionText;
            }
        }
        
        protected override void OnAllFinished()
        {
            // 마지막 레벨(Q15)이 끝나면 즉시 씬 이동을 하지 않고, 
            // 지금까지 녹화된 리얼타임 영상 변환을 수행한 뒤 이동합니다.
            if (string.Equals(levelID, "Q15", StringComparison.OrdinalIgnoreCase))
            {
                StartCoroutine(ProcessVideoAndFinish());
            }
            else
            {
                TransitionToNextScene();
            }
        }

        /// <summary>
        /// 현재 페이지에서 다음 페이지로 넘어갈 때의 연출을 제어합니다.
        /// 페이지 인덱스에 따라 서로 다른 트랜지션 효과(Cover, Reveal, Amjeon)를 적용합니다.
        /// </summary>
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
            
            // # TODO: 인덱스(3, 4) 하드코딩 제거 권장. PageType이나 Enum으로 상태를 확인하는 것이 안전함.
            // Page 4(안내) 진입 시, 곧 나올 Page 5(카메라)를 미리 예열하여 
            // 웹캠 초기화 딜레이로 인한 검은 화면이나 렉을 방지합니다.
            if (targetIndex == 3)
            {
                if (pages.Length > 4 && pages[4] is Page_Camera camPage)
                {
                    camPage.PreloadCamera(); 
                }
            }

            GamePage next = pages[targetIndex];
            bool handled = false;

            // 튜토리얼과 일반 모드의 페이지 구성이 다르므로 트랜지션 로직을 분기합니다.
            if (_isTutorialMode)
            {
                // [Tutorial Transition Logic]
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
                // [Standard Transition Logic]
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

            // 별도 트랜지션이 정의되지 않은 경우 기본 크로스 페이드 처리
            if (!handled)
            {
                if (current != null)
                {
                    yield return StartCoroutine(FadePage(current, 1f, 0f));
                    current.OnExit();
                    yield return CoroutineData.GetWaitForSeconds(0.5f);
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

        private void ConfigureCameraPage(bool save)
        {
            if (pages.Length > 4 && pages[4] is Page_Camera camPage)
            {
                bool isQ15 = string.Equals(levelID, "Q15", StringComparison.OrdinalIgnoreCase);
                
                camPage.Configure(save, cameraMaskMaterial, isQ15);
                
                // 레벨 ID를 전달하여 타임랩스 레코더가 리얼타임 녹화 여부를 결정하도록 합니다.
                camPage.SetLevelID(levelID);
            }
        }

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

        /// <summary> 
        /// 파일명으로 사용할 문자열에서 특수문자나 공백 등을 제거하여 안전하게 만듭니다. 
        /// </summary>
        private string SanitizeString(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            string clean = input.Replace("\n", "").Replace("\r", "").Replace("님", "").Trim();
            
            // 파일 시스템에서 허용하지 않는 문자 제거
            string invalidChars = Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            return Regex.Replace(clean, invalidRegStr, "");
        }

        /// <summary> 
        /// 모든 레벨 종료 후 리얼타임 영상 변환을 기다린 뒤 다음 씬으로 넘어갑니다.
        /// (Q15 종료 시 호출됨)
        /// </summary>
        private IEnumerator ProcessVideoAndFinish()
        {
            if (TimeLapseRecorder.Instance != null)
            {
                // 중복 변환 방지
                if (TimeLapseRecorder.Instance.IsConversionSuccessful)
                {
                    Debug.Log("[LevelManager] 이미 변환 완료됨. 인코딩을 스킵합니다.");
                    TransitionToNextScene();
                    yield break;
                }

                if (!TimeLapseRecorder.Instance.IsProcessing)
                {
                    Debug.Log("[LevelManager] 리얼타임(1배속) 영상 인코딩 시작");
                    TimeLapseRecorder.Instance.ConvertToRealtimeVideo();
                }

                // 변환 완료 대기 (최대 60초 타임아웃)
                float timeout = 60f, elapsed = 0f;
                while (TimeLapseRecorder.Instance.IsProcessing && elapsed < timeout)
                {
                    yield return CoroutineData.GetWaitForSeconds(0.5f);
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

        /// <summary> 페이지 진입 시 특정 데이터(트리거 정보)를 전달하여 로직을 실행합니다. </summary>
        private void HandleTrigger(GamePage page, int info)
        {
            // 예: Page3에서 누가 먼저 버튼을 눌렀는지(1 or 2)에 따라 불 켜는 연출 실행
            if (info != 0 && page is Page_Check checkPage) checkPage.ActivatePlayerCheck(info == 1);
        }

        /// <summary> 커버 트랜지션: 검은 화면이 덮이면서 페이지를 교체하고 다시 열립니다. </summary>
        private IEnumerator CoverTransition(GamePage current, GamePage next, int info)
        {
            if (globalBlackCanvasGroup != null)
                yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 0f, 1f, 0.5f));
            yield return CoroutineData.GetWaitForSeconds(0.5f);
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

        /// <summary> 리빌 트랜지션: 현재 페이지가 사라지면서 뒤에 있던 다음 페이지가 드러납니다. </summary>
        private IEnumerator RevealTransition(GamePage current, GamePage next, int info)
        {
            // 배경을 미리 깔아둠
            if (globalBlackCanvasGroup != null) globalBlackCanvasGroup.alpha = 1f;
            if (current)
            {
                // 현재 페이지 페이드 아웃
                yield return StartCoroutine(FadePage(current, 1f, 0f));
                current.OnExit();
            }

            if (next)
            {
                next.OnEnter();
                next.SetAlpha(0f);
                HandleTrigger(next, info);
                // 다음 페이지 페이드 인
                yield return StartCoroutine(FadePage(next, 0f, 1f));
            }

            // 배경 제거
            if (globalBlackCanvasGroup != null)
                yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 1f, 0f, 0.5f));
        }

        /// <summary> 암전 트랜지션: 화면이 완전히 어두워졌다가 다음 페이지로 밝아집니다. </summary>
        private IEnumerator AmjeonTransition(GamePage current, GamePage next, int info, bool enableWhiteBg = false)
        {
            // FadeManager를 통한 전체 화면 페이드 아웃
            if (FadeManager.Instance)
            {
                bool d = false;
                FadeManager.Instance.FadeOut(1f, () => d = true);
                while (!d) yield return null;
            }
            else yield return CoroutineData.GetWaitForSeconds(0.5f);

            if (current) current.OnExit();
            
            // 카메라 플래시 효과 등을 위해 흰 배경이 필요한 경우 처리
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

        /// <summary> 순차 트랜지션: 현재 페이지 퇴장 -> 대기 -> 다음 페이지 입장 </summary>
        private IEnumerator SequenceTransition(GamePage current, GamePage next, Image background, int info,
            float waitTime = 0f)
        {
            if (background) background.gameObject.SetActive(true);
            if (current)
            {
                yield return StartCoroutine(FadePage(current, 1f, 0f));
                current.OnExit();
            }

            if (waitTime > 0f) yield return CoroutineData.GetWaitForSeconds(waitTime);
            if (next)
            {
                next.OnEnter();
                next.SetAlpha(0f);
                HandleTrigger(next, info);
                yield return StartCoroutine(FadePage(next, 0f, 1f));
            }
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