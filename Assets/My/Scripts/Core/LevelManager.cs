using System;
using System.Collections;
using System.Text.RegularExpressions; 
using My.Scripts.Core.Data;
using My.Scripts.Core.Pages;
using My.Scripts.Global;
using My.Scripts.Hardware;
using My.Scripts.Timelapse;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core
{
    public class LevelManager : BaseFlowManager
    {   
        public static LevelManager Instance { get; private set; }
        
        [Header("Level Settings")]
        [SerializeField] private UserType levelType = UserType.A1; // 기본값 A1
        [SerializeField] private string levelID = GameConstants.Level.Q1; 
        [SerializeField] private bool useFadeTransition = true; 

        [Header("Global Backgrounds")] 
        [SerializeField] private CanvasGroup globalBlackCanvasGroup; 
        [SerializeField] private Image globalWhiteBackground; 

        [Header("Camera Config")] 
        [SerializeField] private Material cameraMaskMaterial; 

        private bool _isTutorialMode; 
        private int _currentQuestionNumber;
        
        public int CurrentQuestionNumber => _currentQuestionNumber;
        
        private bool HasActiveSession => SessionManager.Instance && SessionManager.Instance.CurrentUserId != 0;
        
        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }
        
        private void OnDestroy() { if (Instance == this) Instance = null; }

        protected override void LoadSettings()
        {
            InitializeGlobals();
            _currentQuestionNumber = ParseLevelNumber(levelID);

            if (_isTutorialMode)
            {
                TutorialLevelSetting tSetting = LevelDataLoader.LoadTutorialLevel();
                if (tSetting != null)
                {
                    string nameA = GetPlayerNameOrDefault(true);
                    string nameB = GetPlayerNameOrDefault(false);

                    if (tSetting.Page7 != null)
                    {
                        if (tSetting.Page7.playerAName != null && !string.IsNullOrEmpty(tSetting.Page7.playerAName.text))
                            tSetting.Page7.playerAName.text = tSetting.Page7.playerAName.text.Replace("{nameA}", nameA);
                            
                        if (tSetting.Page7.playerBName != null && !string.IsNullOrEmpty(tSetting.Page7.playerBName.text))
                            tSetting.Page7.playerBName.text = tSetting.Page7.playerBName.text.Replace("{nameB}", nameB);
                    }

                    ReplaceNamesInQnAPage(tSetting.Page2, nameA, nameB);
                    SetCameraFileName();
                    ConfigureCameraPage(false);

                    SetupPageData(0, tSetting.Page1);
                    SetupPageData(1, tSetting.Page2);
                    SetupPageData(2, tSetting.Page3); 
                    SetupPageData(4, tSetting.Page5); 
                    SetupPageData(5, tSetting.Page7); 
                    SetupPageData(6, tSetting.Page8); 
                }
            }
            else
            {
                UserType uType = HasActiveSession ? SessionManager.Instance.CurrentUserType : levelType;
                
                if (HasActiveSession && GameManager.Instance)
                {
                    string suffix = GameManager.Instance.GetLevelSuffix(_currentQuestionNumber);
                    if (!string.IsNullOrEmpty(suffix))
                    {
                        // "_A1" -> "A1" 형태로 변환하여 UserType 매핑
                        string typeStr = suffix.Replace("_", "");
                        if (Enum.TryParse(typeStr, out UserType mappedType))
                        {
                            uType = mappedType;
                        }
                    }
                }

                StandardLevelSetting sSetting = LevelDataLoader.LoadStandardLevel(levelID, uType);
                if (sSetting != null)
                {
                    string nameA = GetPlayerNameOrDefault(true);
                    string nameB = GetPlayerNameOrDefault(false);
                    ReplaceNamesInQnAPage(sSetting.Page2, nameA, nameB);

                    SetCameraFileName();
                    ConfigureCameraPage(true);

                    SetupPageData(0, sSetting.Page1);
                    SetupPageData(1, sSetting.Page2);
                    SetupPageData(2, sSetting.Page3); 
                    SetupPageData(4, sSetting.Page5); 
                }

                if (HueManager.Instance && _currentQuestionNumber == 6)
                {
                    HueManager.Instance.InitRandomColors();
                }
            }
        }
        
        private string GetPlayerNameOrDefault(bool isPlayerA)
        {
            if (HasActiveSession)
            {
                string firstName = isPlayerA ? SessionManager.Instance.PlayerAFirstName : SessionManager.Instance.PlayerBFirstName;
                if (!string.IsNullOrWhiteSpace(firstName)) return firstName;
            }
            return isPlayerA ? "PlayerA" : "PlayerB";
        }

        private void ReplaceNamesInQnAPage(QnAPageData page2, string nameA, string nameB)
        {
            if (page2 == null) return;
            if (page2.nicknamePlayerA != null && !string.IsNullOrEmpty(page2.nicknamePlayerA.text))
                page2.nicknamePlayerA.text = page2.nicknamePlayerA.text.Replace("{nameA}", nameA);
            if (page2.nicknamePlayerB != null && !string.IsNullOrEmpty(page2.nicknamePlayerB.text))
                page2.nicknamePlayerB.text = page2.nicknamePlayerB.text.Replace("{nameB}", nameB);
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

            _isTutorialMode = string.Equals(levelID, GameConstants.Level.Tutorial, StringComparison.OrdinalIgnoreCase);

            if (string.Equals(levelID, GameConstants.Level.Q1, StringComparison.OrdinalIgnoreCase))
            {
                if (TimeLapseRecorder.Instance) TimeLapseRecorder.Instance.ClearRecordingData();
            }
        }

        private void SetupPageData(int index, object data)
        {
            if (pages != null && index >= 0 && index < pages.Length && pages[index])
                pages[index].SetupData(data);
        }

        protected override void OnPageComplete(int currentIndex, int nextIndex, int info)
        {
            if (!_isTutorialMode && currentIndex == 3)
            {
                OnAllFinished();
                return;
            }

            base.OnPageComplete(currentIndex, nextIndex, info);
        }

        protected override void OnAllFinished()
        {
            MoveToNextLevelDynamic(); 
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
            
            if (targetIndex == 2 && pages.Length > 3 && pages[3] is Page_Camera camPage)
            {
                camPage.PreloadCamera(); 
            }

            GamePage next = pages[targetIndex];
            bool handled = false;

            if (_isTutorialMode)
            {
                if (currentPageIndex == 0 && targetIndex == 1) { yield return StartCoroutine(CoverTransition(current, next, info)); handled = true; } 
                else if (currentPageIndex == 1 && targetIndex == 2) { yield return StartCoroutine(RevealTransition(current, next, info)); handled = true; } 
                else if (currentPageIndex == 2 && targetIndex == 3) { yield return StartCoroutine(AmjeonTransition(current, next, info)); handled = true; } 
                else if (currentPageIndex == 3 && targetIndex == 4) { yield return StartCoroutine(AmjeonTransition(current, next, info, true)); handled = true; } 
                else if (currentPageIndex == 4 && targetIndex == 5) { yield return StartCoroutine(SequenceTransition(current, next, globalWhiteBackground, info, 0.5f)); handled = true; } 
            }
            else
            {
                if (currentPageIndex == 0 && targetIndex == 1) { yield return StartCoroutine(CoverTransition(current, next, info)); handled = true; } 
                else if (currentPageIndex == 1 && targetIndex == 2) { yield return StartCoroutine(RevealTransition(current, next, info)); handled = true; } 
                else if (currentPageIndex == 2 && targetIndex == 3) 
                { 
                    yield return StartCoroutine(AmjeonTransition(current, next, info)); handled = true; 
                }
            }

            if (!handled)
            {
                if (current)
                {
                    yield return StartCoroutine(FadePage(current, 1f, 0f));
                    current.OnExit();
                    yield return CoroutineData.GetWaitForSeconds(0.5f);
                }

                if (next)
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
            if (pages.Length > 3 && pages[3] is Page_Camera camPage)
            {
                bool isQ15 = string.Equals(levelID, GameConstants.Level.Q15, StringComparison.OrdinalIgnoreCase);
                camPage.Configure(save, cameraMaskMaterial, isQ15);
                camPage.SetLevelID(levelID);
            }
        }

        private void SetCameraFileName()
        {
            if (pages.Length <= 3) return;
            Page_Camera cameraPage = pages[3] as Page_Camera;
            if (!cameraPage) return;
            
            string userIdStr = HasActiveSession ? SessionManager.Instance.CurrentUserId.ToString() : "0";
            cameraPage.SetPhotoFilename($"{userIdStr}_{levelID}");
        }

        private void MoveToNextLevelDynamic()
        {
            if (_isTutorialMode)
            {
                string firstScene = GetNextSceneName(1); 
                if (useFadeTransition && GameManager.Instance) GameManager.Instance.ChangeScene(firstScene);
                else SceneManager.LoadScene(firstScene);
                return;
            }

            int nextNum = _currentQuestionNumber + 1;
            string nextScene = GetNextSceneName(nextNum);
            
            if (useFadeTransition && GameManager.Instance) GameManager.Instance.ChangeScene(nextScene);
            else SceneManager.LoadScene(nextScene);
        }

        private string GetNextSceneName(int qNum)
        {
            if (qNum > 15) return GameConstants.Scene.Ending;
            string suffix = GameManager.Instance ? GameManager.Instance.GetLevelSuffix(qNum) : "";
            return $"Play_Q{qNum}{suffix}"; 
        }
        
        private void HandleTrigger(GamePage page, int info)
        {
            if (info != 0 && page is ITriggerReceiver receiver)
            {
                receiver.ReceiveTrigger(info);
            }
        }

        private IEnumerator CoverTransition(GamePage current, GamePage next, int info)
        {
            if (globalBlackCanvasGroup) yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 0f, 1f, 0.5f));
            yield return CoroutineData.GetWaitForSeconds(0.5f);
            if (current) current.OnExit();
            if (next) { next.OnEnter(); next.SetAlpha(0f); HandleTrigger(next, info); }
            if (next) yield return StartCoroutine(FadePage(next, 0f, 1f));
            if (globalBlackCanvasGroup) yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 1f, 0f, 0.5f));
        }

        private IEnumerator RevealTransition(GamePage current, GamePage next, int info)
        {
            if (globalBlackCanvasGroup) globalBlackCanvasGroup.alpha = 1f;
            if (current) { yield return StartCoroutine(FadePage(current, 1f, 0f)); current.OnExit(); }
            if (next) { next.OnEnter(); next.SetAlpha(0f); HandleTrigger(next, info); yield return StartCoroutine(FadePage(next, 0f, 1f)); }
            if (globalBlackCanvasGroup) yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 1f, 0f, 0.5f));
        }

        private IEnumerator AmjeonTransition(GamePage current, GamePage next, int info, bool enableWhiteBg = false)
        {
            if (FadeManager.Instance)
            {
                bool d = false;
                FadeManager.Instance.FadeOut(0.25f, () => d = true);
                while (!d) yield return null;
            }
            else yield return CoroutineData.GetWaitForSeconds(0.25f);

            if (current) current.OnExit();
            if (enableWhiteBg && globalWhiteBackground)
            {
                globalWhiteBackground.gameObject.SetActive(true);
                Color c = globalWhiteBackground.color; c.a = 1f; globalWhiteBackground.color = c;
            }
            if (next) { next.OnEnter(); next.SetAlpha(1f); HandleTrigger(next, info); }
            if (FadeManager.Instance) FadeManager.Instance.FadeIn(0.25f);
        }

        private IEnumerator SequenceTransition(GamePage current, GamePage next, Image background, int info, float waitTime = 0f)
        {
            if (background) background.gameObject.SetActive(true);
            if (current) { yield return StartCoroutine(FadePage(current, 1f, 0f)); current.OnExit(); }
            if (waitTime > 0f) yield return CoroutineData.GetWaitForSeconds(waitTime);
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