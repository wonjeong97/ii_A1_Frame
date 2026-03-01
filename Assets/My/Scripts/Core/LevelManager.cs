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
    public interface ILevelSetting
    {
        GridPageData Page1 { get; set; }
        QnAPageData Page2 { get; set; }
        CheckPageData Page3 { get; set; }
        TransitionPageData Page4 { get; set; }
        TransitionPageData Page6 { get; set; }
    }

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

    [Serializable]
    public class TutorialLevelSetting : ILevelSetting
    {
        public GridPageData page1;
        public QnAPageData page2;
        public CheckPageData page3;
        public TransitionPageData page4;
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

    public class LevelManager : BaseFlowManager
    {   
        public static LevelManager Instance { get; private set; }
        
        [Header("Level Settings")]
        [SerializeField] private UserType levelType = UserType.A;
        [SerializeField] private string levelID = "Q1"; 
        [SerializeField] private bool useFadeTransition = true; 

        [Header("Global Backgrounds")] 
        [SerializeField] private CanvasGroup globalBlackCanvasGroup; 
        [SerializeField] private Image globalWhiteBackground; 

        [Header("Camera Config")] 
        [SerializeField] private Material cameraMaskMaterial; 

        private bool _isTutorialMode; 
        private int _currentQuestionNumber;
        
        public int CurrentQuestionNumber => _currentQuestionNumber;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        protected override void LoadSettings()
        {
            InitializeGlobals();
            
            _currentQuestionNumber = ParseLevelNumber(levelID);

            string suffix = "";
            if (!_isTutorialMode && GameManager.Instance != null)
            {
                suffix = GameManager.Instance.GetLevelSuffix(_currentQuestionNumber);
            }

            var commonData = JsonLoader.Load<StandardLevelSetting>("JSON/PlayCommon");
            
            // [추가] GameManager에서 동적 이름 가져오기
            string nameA = "PlayerA";
            string nameB = "PlayerB";
            if (GameManager.Instance != null)
            {
                nameA = GameManager.Instance.PlayerALastName;
                nameB = GameManager.Instance.PlayerBLastName;
            }

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

                // [추가] 튜토리얼 7페이지 (TransitionPageData) 이름 치환
                if (tSetting.Page7 != null)
                {
                    if (tSetting.Page7.playerAName != null && !string.IsNullOrEmpty(tSetting.Page7.playerAName.text))
                        tSetting.Page7.playerAName.text = tSetting.Page7.playerAName.text.Replace("{nameA}", nameA);
                        
                    if (tSetting.Page7.playerBName != null && !string.IsNullOrEmpty(tSetting.Page7.playerBName.text))
                        tSetting.Page7.playerBName.text = tSetting.Page7.playerBName.text.Replace("{nameB}", nameB);
                }

                // [추가] 공통 3페이지 (CheckPageData) 이름 치환
                ReplaceNamesInCheckPage(tSetting.Page3, nameA, nameB);

                SetCameraFileName(tSetting.Page3);
                ConfigureCameraPage(false);

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
                string typeStr = levelType.ToString(); 
                path = $"JSON/{typeStr}/Play{levelID}_{typeStr}";
                
                var sSetting = JsonLoader.Load<StandardLevelSetting>(path);
                
                if (sSetting == null)
                {
                    Debug.LogError($"[LevelManager] 레벨 데이터를 찾을 수 없습니다: {path}");
                    return;
                }

                MergeCommonData(sSetting, commonData);

                // [추가] 공통 3페이지 (CheckPageData) 이름 치환
                ReplaceNamesInCheckPage(sSetting.Page3, nameA, nameB);

                SetCameraFileName(sSetting.Page3);
                ConfigureCameraPage(true);

                SetupPageData(0, sSetting.Page1);
                SetupPageData(1, sSetting.Page2);
                SetupPageData(2, sSetting.Page3);
                SetupPageData(3, sSetting.Page4);
                SetupPageData(5, sSetting.Page6);
            }
        }

        private void ReplaceNamesInCheckPage(CheckPageData page3, string nameA, string nameB)
        {
            if (page3 == null) return;
    
            if (page3.nicknamePlayerA != null && !string.IsNullOrEmpty(page3.nicknamePlayerA.text))
            {
                page3.nicknamePlayerA.text = page3.nicknamePlayerA.text.Replace("{nameA}", nameA);
            }
    
            if (page3.nicknamePlayerB != null && !string.IsNullOrEmpty(page3.nicknamePlayerB.text))
            {
                page3.nicknamePlayerB.text = page3.nicknamePlayerB.text.Replace("{nameB}", nameB);
            }
        }
        
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

        private void MergeCommonData(ILevelSetting specific, StandardLevelSetting common)
        {
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

            if (specific.Page2 == null) specific.Page2 = new QnAPageData();
            if (common.Page2 != null)
            {
                if (specific.Page2.descriptionText == null || string.IsNullOrEmpty(specific.Page2.descriptionText.text)) specific.Page2.descriptionText = common.Page2.descriptionText;
                if (specific.Page2.answerTexts == null || specific.Page2.answerTexts.Length == 0) specific.Page2.answerTexts = common.Page2.answerTexts;
                
                if (string.IsNullOrEmpty(specific.Page2.warningMessage)) specific.Page2.warningMessage = common.Page2.warningMessage;
                if (string.IsNullOrEmpty(specific.Page2.resetMessage)) specific.Page2.resetMessage = common.Page2.resetMessage;
            }

            if (specific.Page3 == null) specific.Page3 = new CheckPageData();
            if (common.Page3 != null)
            {
                if (specific.Page3.nicknamePlayerA == null) specific.Page3.nicknamePlayerA = common.Page3.nicknamePlayerA;
                if (specific.Page3.nicknamePlayerB == null) specific.Page3.nicknamePlayerB = common.Page3.nicknamePlayerB;
                if (specific.Page3.waitText == null) specific.Page3.waitText = common.Page3.waitText;
                
                if (string.IsNullOrEmpty(specific.Page3.warningMessage)) specific.Page3.warningMessage = common.Page3.warningMessage;
                if (string.IsNullOrEmpty(specific.Page3.resetMessage)) specific.Page3.resetMessage = common.Page3.resetMessage;
            }

            if (specific.Page4 == null) specific.Page4 = new TransitionPageData();
            if (common.Page4 != null)
            {
                if (specific.Page4.descriptionText == null) specific.Page4.descriptionText = common.Page4.descriptionText;
                if (string.IsNullOrEmpty(specific.Page4.warningMessage)) specific.Page4.warningMessage = common.Page4.warningMessage;
                if (string.IsNullOrEmpty(specific.Page4.resetMessage)) specific.Page4.resetMessage = common.Page4.resetMessage;
            }

            if (specific.Page6 == null) specific.Page6 = new TransitionPageData();
            if (common.Page6 != null)
            {
                if (specific.Page6.descriptionText == null) specific.Page6.descriptionText = common.Page6.descriptionText;
            }
        }
        
        protected override void OnAllFinished()
        {
            if (string.Equals(levelID, "Q15", StringComparison.OrdinalIgnoreCase))
            {
                StartCoroutine(ProcessVideoAndFinish());
            }
            else
            {
                MoveToNextLevelDynamic(); 
            }
        }

        protected override IEnumerator TransitionRoutine(int targetIndex, int info)
        {
            isTransitioning = true;
            GamePage current = (currentPageIndex >= 0 && currentPageIndex < pages.Length) ? pages[currentPageIndex] : null;
            
            if (targetIndex < 0 || targetIndex >= pages.Length)
            {
                isTransitioning = false;
                yield break;
            }
            
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
            if (pages.Length <= 4) return;
            Page_Camera cameraPage = pages[4] as Page_Camera;
            if (cameraPage == null) return;
            
            string nameA = "PlayerA";
            string nameB = "PlayerB";

            if (GameManager.Instance != null)
            {
                nameA = GameManager.Instance.PlayerALastName;
                nameB = GameManager.Instance.PlayerBLastName;
            }
            
            nameA = SanitizeString(nameA);
            nameB = SanitizeString(nameB);
            
            cameraPage.SetPhotoFilename($"{nameA}{nameB}_{levelID}");
        }

        private string SanitizeString(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            
            string clean = input.Replace("\n", "").Replace("\r", "").Trim();
            string invalidChars = Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            return Regex.Replace(clean, invalidRegStr, "");
        }

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

        private void MoveToNextLevelDynamic()
        {
            if (_isTutorialMode)
            {
                string firstScene = GetNextSceneName(1); 
                
                if (useFadeTransition && GameManager.Instance != null) 
                    GameManager.Instance.ChangeScene(firstScene);
                else 
                    SceneManager.LoadScene(firstScene);
                
                return;
            }

            int nextNum = _currentQuestionNumber + 1;
            string nextScene = GetNextSceneName(nextNum);
            
            Debug.Log($"[LevelManager] Moving to next level: {nextScene}");

            if (useFadeTransition && GameManager.Instance != null) 
                GameManager.Instance.ChangeScene(nextScene);
            else 
                SceneManager.LoadScene(nextScene);
        }

        private string GetNextSceneName(int qNum)
        {
            if (qNum > 15) return GameConstants.Scene.Ending;

            string suffix = "";
            if (GameManager.Instance != null)
            {
                suffix = GameManager.Instance.GetLevelSuffix(qNum);
            }

            return $"Play_Q{qNum}{suffix}"; 
        }
        
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