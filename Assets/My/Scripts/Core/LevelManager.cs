using System;
using System.Collections;
using System.Text.RegularExpressions;
using My.Scripts.Core.Data;
using My.Scripts.Core.Pages;
using My.Scripts.Global;
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
        // 미사용 필드 page5 제거 (index 4는 CameraPage로 예약됨)
        public TransitionPageData page6;
        public TransitionPageData page7;
        public TutorialPage8Data page8;

        public GridPageData Page1 { get => page1; set => page1 = value; }
        public QnAPageData Page2 { get => page2; set => page2 = value; }
        public CheckPageData Page3 { get => page3; set => page3 = value; }
        public TransitionPageData Page4 { get => page4; set => page4 = value; }
        public TransitionPageData Page6 { get => page6; set => page6 = value; }
        public TransitionPageData Page7 { get => page7; set => page7 = value; }
        public TutorialPage8Data Page8 { get => page8; set => page8 = value; }
    }

    /// <summary> 
    /// 개별 레벨(Q1~Q15 및 튜토리얼)의 전체 흐름을 관리하는 매니저입니다.
    /// JSON 데이터 로드, 페이지 간의 특수 전환 연출, 그리고 최종 영상 변환 트리거를 담당합니다.
    /// </summary>
    public class LevelManager : BaseFlowManager
    {   
        public static LevelManager Instance { get; private set; }
        
        [Header("Level Settings")]
        [SerializeField] private UserType levelType = UserType.A;
        [Tooltip("현재 레벨의 ID (예: Q1, Q2...). 숫자 파싱 및 JSON 로드에 사용됩니다.")]
        [SerializeField] private string levelID = "Q1"; 
        
        [Tooltip("페이드 효과 사용 여부")]
        [SerializeField] private bool useFadeTransition = true; 

        [Header("Global Backgrounds")] 
        [SerializeField] private CanvasGroup globalBlackCanvasGroup; // 전환 연출용 전역 검은 배경
        [SerializeField] private Image globalWhiteBackground; // 전환 연출용 전역 흰 배경

        [Header("Camera Config")] 
        [SerializeField] private Material cameraMaskMaterial; 

        private bool _isTutorialMode; 
        private int _currentQuestionNumber;
        
        public int CurrentQuestionNumber => _currentQuestionNumber;
        
        private void Awake()
        {
            Instance = this;
        }

        protected override void LoadSettings()
        {
            InitializeGlobals();
            
            // 1. 현재 레벨의 숫자 파싱 (Q1 -> 1)
            _currentQuestionNumber = ParseLevelNumber(levelID);

            // 2. 현재 레벨에 맞는 접미사 가져오기 (예: _A, _B)
            string suffix = "";
            if (!_isTutorialMode && GameManager.Instance != null)
            {
                suffix = GameManager.Instance.GetLevelSuffix(_currentQuestionNumber);
            }

            // 3. 공통 데이터 로드
            var commonData = JsonLoader.Load<StandardLevelSetting>("JSON/PlayCommon");
            
            // 4. 경로 생성 및 로드
            string path;
            if (_isTutorialMode)
            {
                path = "JSON/PlayTutorial"; 
                
                var tSetting = JsonLoader.Load<TutorialLevelSetting>(path);
                
                if (tSetting == null)
                {
                    Debug.LogError($"[LevelManager] 튜토리얼 데이터를 찾을 수 없습니다: {path}");
                    return;
                }

                MergeCommonData(tSetting, commonData);
                SetCameraFileName(tSetting.Page3);
                ConfigureCameraPage(false);

                // SetupPageData ... (생략)
                SetupPageData(0, tSetting.Page1);
                SetupPageData(1, tSetting.Page2);
                SetupPageData(2, tSetting.Page3);
                SetupPageData(3, tSetting.Page4);
                SetupPageData(5, tSetting.Page6);
                SetupPageData(6, tSetting.Page7);
                SetupPageData(7, tSetting.Page8);
            }
            else
            {
                string typeStr = levelType.ToString(); // "A", "B"...
                path = $"JSON/{typeStr}/Play{levelID}_{typeStr}";
                
                var sSetting = JsonLoader.Load<StandardLevelSetting>(path);
                
                if (sSetting == null)
                {
                    Debug.LogError($"[LevelManager] 레벨 데이터를 찾을 수 없습니다: {path}");
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
        
        /// <summary> 레벨 ID 문자열에서 숫자만 추출 </summary>
        private int ParseLevelNumber(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            string numStr = Regex.Replace(id, "[^0-9]", "");
            if (int.TryParse(numStr, out int num)) return num;
            return 0;
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
            if (specific.Page2 == null) specific.Page2 = new QnAPageData();
            
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
        
        /// <summary>
        /// 모든 페이지(단계)가 끝났을 때 호출됩니다.
        /// </summary>
        protected override void OnAllFinished()
        {
            // 마지막 레벨(Q15)이 끝나면 영상 변환 후 엔딩으로 이동
            // 그 외에는 계산된 다음 레벨로 이동
            if (string.Equals(levelID, "Q15", StringComparison.OrdinalIgnoreCase))
            {
                StartCoroutine(ProcessVideoAndFinish());
            }
            else
            {
                MoveToNextLevelDynamic(); 
            }
        }

        // ... (TransitionRoutine, ConfigureCameraPage, SetCameraFileName, SanitizeString 등은 기존과 동일) ...
        /// <summary>
        /// 현재 페이지에서 다음 페이지로 넘어갈 때의 연출을 제어합니다.
        /// </summary>
        protected override IEnumerator TransitionRoutine(int targetIndex, int info)
        {
            isTransitioning = true;
            GamePage current = (currentPageIndex >= 0 && currentPageIndex < pages.Length) ? pages[currentPageIndex] : null;
            
            if (targetIndex < 0 || targetIndex >= pages.Length)
            {
                isTransitioning = false;
                yield break;
            }
            
            // Page 4(안내) 진입 시, 곧 나올 Page 5(카메라)를 미리 예열
            if (targetIndex == 3)
            {
                if (pages.Length > 4 && pages[4] is Page_Camera camPage)
                {
                    camPage.PreloadCamera(); 
                }
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
                camPage.SetLevelID(levelID);
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

        /// <summary> 
        /// 모든 레벨 종료 후 리얼타임 영상 변환을 기다린 뒤 다음 씬으로 넘어갑니다.
        /// (Q15 종료 시 호출됨)
        /// </summary>
        private IEnumerator ProcessVideoAndFinish()
        {
            if (TimeLapseRecorder.Instance != null)
            {
                if (TimeLapseRecorder.Instance.IsConversionSuccessful)
                {
                    Debug.Log("[LevelManager] 이미 변환 완료됨. 인코딩을 스킵합니다.");
                    MoveToNextLevelDynamic();
                    yield break;
                }

                if (!TimeLapseRecorder.Instance.IsRealtimeProcessing)
                {
                    Debug.Log("[LevelManager] 리얼타임(1배속) 영상 인코딩 시작");
                    TimeLapseRecorder.Instance.ConvertToRealtimeVideo();
                }

                float timeout = 60f, elapsed = 0f;
                while (TimeLapseRecorder.Instance.IsRealtimeProcessing && elapsed < timeout)
                {
                    yield return CoroutineData.GetWaitForSeconds(0.5f);
                    elapsed += 0.5f;
                }

                if (TimeLapseRecorder.Instance.IsRealtimeProcessing)
                {
                    Debug.LogWarning("[LevelManager] 리얼타임 변환 타임아웃. 플래그를 강제 리셋합니다.");
                    TimeLapseRecorder.Instance.ResetRealtimeProcessing();
                }
            }

            MoveToNextLevelDynamic();
        }

        /// <summary>
        /// 현재 상태(튜토리얼 여부, 현재 질문 번호)와 유저 타입을 고려하여 다음 씬으로 이동합니다.
        /// </summary>
        private void MoveToNextLevelDynamic()
        {
            if (_isTutorialMode)
            {
                // 튜토리얼 종료 -> Q1 시작
                // Q1은 타입 분기가 없거나 A타입을 따르므로 GetNextSceneName(1) 호출
                string firstScene = GetNextSceneName(1); 
                
                if (useFadeTransition && GameManager.Instance != null) 
                    GameManager.Instance.ChangeScene(firstScene);
                else 
                    SceneManager.LoadScene(firstScene);
                
                return;
            }

            // 현재가 Q1이면 다음은 Q2
            int nextNum = _currentQuestionNumber + 1;
            
            // 다음 씬 이름 계산 (PlayQ2_A, PlayQ4_B 등)
            string nextScene = GetNextSceneName(nextNum);
            
            Debug.Log($"[LevelManager] Moving to next level: {nextScene}");

            if (useFadeTransition && GameManager.Instance != null) 
                GameManager.Instance.ChangeScene(nextScene);
            else 
                SceneManager.LoadScene(nextScene);
        }

        /// <summary> 
        /// 질문 번호와 유저 타입에 기반해 씬 이름 생성 
        /// <para>GameManager.GetLevelSuffix를 통해 _A, _B, _C 등을 받아옵니다.</para>
        /// </summary>
        private string GetNextSceneName(int qNum)
        {
            // 질문 번호가 15를 넘어가면 엔딩으로
            if (qNum > 15) return GameConstants.Scene.Ending;

            // GameManager에서 현재 유저 타입과 질문 번호에 맞는 접미사 획득
            // 예: B유형이면서 4번 질문이면 "_B" 반환
            string suffix = "";
            if (GameManager.Instance != null)
            {
                suffix = GameManager.Instance.GetLevelSuffix(qNum);
            }

            // [수정] 씬 이름 포맷을 "Play_Q{n}{suffix}"로 변경 (예: Play_Q1_A)
            return $"Play_Q{qNum}{suffix}"; 
        }
        
        // ... (나머지 헬퍼 함수들: HandleTrigger, CoverTransition 등은 기존 코드 그대로 유지) ...
        private void HandleTrigger(GamePage page, int info)
        {
            if (info != 0 && page is Page_Check checkPage) checkPage.ActivatePlayerCheck(info == 1);
        }

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

        private IEnumerator AmjeonTransition(GamePage current, GamePage next, int info, bool enableWhiteBg = false)
        {
            if (FadeManager.Instance)
            {
                bool d = false;
                FadeManager.Instance.FadeOut(1f, () => d = true);
                while (!d) yield return null;
            }
            else yield return CoroutineData.GetWaitForSeconds(0.5f);

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

        private IEnumerator SequenceTransition(GamePage current, GamePage next, Image background, int info, float waitTime = 0f)
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