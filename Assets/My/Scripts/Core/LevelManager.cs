using System;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Text; 
using VContainer; 
using My.Scripts.Core.Data;
using My.Scripts.Core.Pages;
using My.Scripts.Global;
using My.Scripts.Hardware;
using My.Scripts.Timelapse;
using My.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts.Core
{
    /// <summary>
    /// 단일 레벨(문항)의 페이지 흐름을 관리하고, 사용자 데이터 기반으로 UI를 초기화함.
    /// LevelDataLoader 인스턴스 주입을 통해 컨텍스트 액세스 에러를 완벽히 해결했습니다.
    /// </summary>
    public class LevelManager : BaseFlowManager
    {   
        [Header("Level Settings")]
        [SerializeField] private UserType levelType = UserType.A1; 
        [SerializeField] private string levelID = GameConstants.Level.Q1; 
        [SerializeField] private bool useFadeTransition = true; 

        [Header("Global Backgrounds")] 
        [SerializeField] private CanvasGroup globalBlackCanvasGroup; 
        [SerializeField] private Image globalWhiteBackground; 

        [Header("Camera Config")] 
        [SerializeField] private Material cameraMaskMaterial; 

        private bool _isTutorialMode; 
        private int _currentQuestionNumber;
        private bool _settingsLoaded;
        
        public int CurrentQuestionNumber => _currentQuestionNumber;
        
        private bool HasActiveSession => _sessionManager && _sessionManager.CurrentUserId != 0;

        private readonly static Regex NumberExtractorRegex = new Regex(@"\d+", RegexOptions.Compiled);

        // --- 의존성 주입 (DI) 변수 ---
        private IObjectResolver _resolver;
        private SessionManager _sessionManager;
        private GameManager _gameManager;
        private TimeLapseRecorder _timeLapseRecorder;
        private HueManager _hueManager;
        private FadeManager _fadeManager;
        private LevelDataLoader _levelDataLoader;
        
        [Inject]
        public void Construct(
            IObjectResolver resolver,
            SessionManager sessionManager,
            GameManager gameManager,
            TimeLapseRecorder timeLapseRecorder,
            HueManager hueManager,
            FadeManager fadeManager,
            LevelDataLoader levelDataLoader)
        {
            _resolver = resolver;
            _sessionManager = sessionManager;
            _gameManager = gameManager;
            _timeLapseRecorder = timeLapseRecorder;
            _hueManager = hueManager;
            _fadeManager = fadeManager;
            _levelDataLoader = levelDataLoader;
        }
        
        protected override void Start()
        {
            if (_resolver == null)
            {
                Debug.LogError("[LevelManager] IObjectResolver is required.");
                return;
            }
            if (pages != null)
            {
                foreach (GamePage page in pages)
                {
                    if (page) _resolver.Inject(page);
                }
            }
            LoadSettings();
            if (!_settingsLoaded) return;
            if (pages == null || pages.Length == 0)
            {
                Debug.LogWarning("[BaseFlowManager] pages 비어있음");
                return;
            }
            InitializePages();
            StartFlow();
        }
        
        protected override void OnDestroy()
        { 
            base.OnDestroy();
        }

        protected override void LoadSettings()
        {
            InitializeGlobals();
            _currentQuestionNumber = ParseLevelNumber(levelID);

            if (_isTutorialMode)
            {
                _settingsLoaded = LoadTutorialSettings();
            }
            else
            {
                _settingsLoaded = LoadStandardSettings();
            }
        }
        
        private bool LoadTutorialSettings()
        {
            TutorialLevelSetting tSetting = _levelDataLoader.LoadTutorialLevel();
            if (tSetting == null)
            {
                Debug.LogError("[LevelManager] _levelDataLoader.LoadTutorialLevel() returned null.");
                return false;
            }

            string nameA = GetPlayerNameOrDefault(true);
            string nameB = GetPlayerNameOrDefault(false);

            ReplaceNamesInQnAPage(tSetting.Page2, nameA, nameB);
            ReplaceNamesInTransitionPage(tSetting.Page4, nameA, nameB);
            ReplaceNamesInTransitionPage(tSetting.Page6, nameA, nameB);
            ReplaceNamesInTransitionPage(tSetting.Page7, nameA, nameB);
            ReplaceNamesInTutorialPage8(tSetting.Page8, nameA, nameB);

            PrepareCameraPage(false);

            SetupPageData(0, tSetting.Page1);
            SetupPageData(1, tSetting.Page2);
            SetupPageData(2, tSetting.Page4);
            SetupPageData(3, tSetting.Page7); 
            SetupPageData(4, tSetting.Page8); 
            
            return true;
        }
        
        private bool LoadStandardSettings()
        {
            UserType uType = HasActiveSession ? _sessionManager.CurrentUserType : levelType;
            StandardLevelSetting sSetting = _levelDataLoader.LoadStandardLevel(levelID, uType);
            
            if (sSetting == null)
            {
                Debug.LogError("[LevelManager] _levelDataLoader.LoadStandardLevel() returned null.");
                return false;
            }

            string nameA = GetPlayerNameOrDefault(true);
            string nameB = GetPlayerNameOrDefault(false);
            
            ReplaceNamesInQnAPage(sSetting.Page2, nameA, nameB);
            ReplaceNamesInTransitionPage(sSetting.Page4, nameA, nameB);
            ReplaceNamesInTransitionPage(sSetting.Page6, nameA, nameB);

            PrepareCameraPage(true);

            SetupPageData(0, sSetting.Page1);
            SetupPageData(1, sSetting.Page2);
            SetupPageData(2, sSetting.Page4); 
            SetupPageData(4, sSetting.Page6); 

            if (_hueManager && _currentQuestionNumber == 6)
            {
                _hueManager.InitRandomColors();
            }
            
            return true;
        }
        
        private void PrepareCameraPage(bool save)
        {
            SetCameraFileName();
            ConfigureCameraPage(save);
        }
       
        private void ReplaceNamesInTransitionPage(TransitionPageData pageData, string nameA, string nameB)
        {
            if (pageData == null) return;
            ReplaceTextSettingName(pageData.descriptionText, nameA, nameB);
            ReplaceTextSettingName(pageData.playerAName, nameA, nameB);
            ReplaceTextSettingName(pageData.playerBName, nameA, nameB);
        }

        private void ReplaceNamesInTutorialPage8(TutorialPage8Data pageData, string nameA, string nameB)
        {
            if (pageData == null) return;
            ReplaceTextSettingName(pageData.introText, nameA, nameB);
            ReplaceTextSettingName(pageData.countdownText, nameA, nameB);
            ReplaceTextSettingName(pageData.startText, nameA, nameB);
        }
        
        private string GetPlayerNameOrDefault(bool isPlayerA)
        {
            if (HasActiveSession)
            {
                string firstName = isPlayerA ? _sessionManager.PlayerAFirstName : _sessionManager.PlayerBFirstName;
                if (!string.IsNullOrWhiteSpace(firstName)) return firstName;
            }
            return isPlayerA ? "PlayerA" : "PlayerB";
        }

        private void ReplaceNamesInQnAPage(QnAPageData pageData, string nameA, string nameB)
        {
            if (pageData == null) return;

            if (pageData.nicknamePlayerA != null && !string.IsNullOrEmpty(pageData.nicknamePlayerA.text))
            {
                using var sb = ZString.CreateStringBuilder();
                sb.Append(pageData.nicknamePlayerA.text);
                sb.Replace("{nameA}", nameA);
                pageData.nicknamePlayerA.text = sb.ToString();
            }

            if (pageData.nicknamePlayerB != null && !string.IsNullOrEmpty(pageData.nicknamePlayerB.text))
            {
                using var sb = ZString.CreateStringBuilder();
                sb.Append(pageData.nicknamePlayerB.text);
                sb.Replace("{nameB}", nameB);
                pageData.nicknamePlayerB.text = sb.ToString();
            }
        }
        
        private void ReplaceTextSettingName(TextSetting setting, string nameA, string nameB)
        {
            if (setting != null && !string.IsNullOrEmpty(setting.text))
            {
                using var sb = ZString.CreateStringBuilder();
                sb.Append(setting.text);
                sb.Replace("{nameA}", nameA);
                sb.Replace("{nameB}", nameB);
                setting.text = sb.ToString();
            }
        }
        
        private int ParseLevelNumber(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            
            Match match = NumberExtractorRegex.Match(id);
            if (match.Success && int.TryParse(match.Value, out int num)) 
            {
                return num;
            }
            return 0;
        }

        private void InitializeGlobals()
        {
            if (globalBlackCanvasGroup)
            {
                globalBlackCanvasGroup.alpha = 0f;
                globalBlackCanvasGroup.blocksRaycasts = false;
                globalBlackCanvasGroup.gameObject.SetActive(false); 
            }

            if (globalWhiteBackground) globalWhiteBackground.gameObject.SetActive(false);

            _isTutorialMode = string.Equals(levelID, GameConstants.Level.Tutorial, StringComparison.OrdinalIgnoreCase);

            if (string.Equals(levelID, GameConstants.Level.Q1, StringComparison.OrdinalIgnoreCase))
            {
                if (_timeLapseRecorder) _timeLapseRecorder.ClearRecordingData();
            }
        }

        private void SetupPageData(int index, object data)
        {
            if (pages != null && index >= 0 && index < pages.Length && pages[index])
            {
                pages[index].SetupData(data);
            }
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

        protected override async UniTaskVoid TransitionAsync(int targetIndex, int info, CancellationToken token)
        {
            isTransitioning = true;
            try
            {
                GamePage current = (currentPageIndex >= 0 && currentPageIndex < pages.Length) ? pages[currentPageIndex] : null;
                
                if (targetIndex < 0 || targetIndex >= pages.Length) return;
                
                TryPreloadCamera(targetIndex);

                GamePage next = pages[targetIndex];
                
                bool handledSpecially = await TrySpecialTransitionAsync(current, next, currentPageIndex, targetIndex, info, token);

                if (!handledSpecially)
                {
                    await DefaultTransitionAsync(current, next, info, token);
                }

                currentPageIndex = targetIndex;
            }
            catch (OperationCanceledException) { }
            finally
            {
                isTransitioning = false;
            }
        }
        
        private void TryPreloadCamera(int targetIndex)
        {
            if (targetIndex == 2 && pages.Length > 3)
            {
                if (pages[3] is Page_Camera camPage)
                {
                    camPage.PreloadCamera();
                }
            }
        }
        
        private async UniTask<bool> TrySpecialTransitionAsync(GamePage current, GamePage next, int currIdx, int targetIdx, int info, CancellationToken token)
        {
            if (currIdx == 0 && targetIdx == 1) 
            {
                await CoverTransitionAsync(current, next, info, token);
                return true;
            }
            if (currIdx == 1 && targetIdx == 2) 
            {
                await RevealTransitionAsync(current, next, info, token);
                return true;
            }
            if (currIdx == 2 && targetIdx == 3) 
            {
                await AmjeonTransitionAsync(current, next, info, _isTutorialMode, token);
                return true;
            }
            if (_isTutorialMode && currIdx == 3 && targetIdx == 4) 
            {
                await SequenceTransitionAsync(current, next, globalWhiteBackground, info, 0.5f, token);
                return true;
            }
            return false;
        }

        private async UniTask DefaultTransitionAsync(GamePage current, GamePage next, int info, CancellationToken token)
        {
            if (current)
            {
                await FadePageAsync(current, 1f, 0f, 0.5f, token);
                current.OnExit();
            }

            if (next)
            {
                next.OnEnter();
                HandleTrigger(next, info);
                
                if (currentPageIndex == -1 && next is Page_Grid) 
                {
                    next.SetAlpha(1f);
                }
                else
                {
                    next.SetAlpha(0f);
                    await FadePageAsync(next, 0f, 1f, 0.5f, token);
                }
            }
        }
        
        private async UniTask CoverTransitionAsync(GamePage current, GamePage next, int info, CancellationToken token)
        {
            if (globalBlackCanvasGroup) 
            {
                globalBlackCanvasGroup.gameObject.SetActive(true);
                globalBlackCanvasGroup.blocksRaycasts = true;
                await globalBlackCanvasGroup.FadeAsync(0f, 1f, 0.5f, token);
            }
            
            if (current) current.OnExit();
            if (next) 
            { 
                next.OnEnter(); 
                next.SetAlpha(0f); 
                HandleTrigger(next, info); 
            }
            
            if (globalWhiteBackground) globalWhiteBackground.gameObject.SetActive(false); 

            if (next) await FadePageAsync(next, 0f, 1f, 0.5f, token);
            
            if (globalBlackCanvasGroup) 
            {
                await globalBlackCanvasGroup.FadeAsync(1f, 0f, 0.5f, token);
                globalBlackCanvasGroup.blocksRaycasts = false;
                globalBlackCanvasGroup.gameObject.SetActive(false);
            }
        }
        
        private async UniTask RevealTransitionAsync(GamePage current, GamePage next, int info, CancellationToken token)
        {
            if (globalBlackCanvasGroup)
            {
                globalBlackCanvasGroup.gameObject.SetActive(true);
                globalBlackCanvasGroup.blocksRaycasts = true;
                globalBlackCanvasGroup.alpha = 1f;
            }
            
            if (current) 
            { 
                await FadePageAsync(current, 1f, 0f, 0.5f, token); 
                current.OnExit(); 
            }
            
            if (globalWhiteBackground) globalWhiteBackground.gameObject.SetActive(false); 

            if (next) 
            { 
                next.OnEnter(); 
                next.SetAlpha(0f); 
                HandleTrigger(next, info); 
                await FadePageAsync(next, 0f, 1f, 0.5f, token); 
            }
            
            if (globalBlackCanvasGroup) 
            {
                await globalBlackCanvasGroup.FadeAsync(1f, 0f, 0.5f, token);
                globalBlackCanvasGroup.blocksRaycasts = false;
                globalBlackCanvasGroup.gameObject.SetActive(false);
            }
        }

        private async UniTask AmjeonTransitionAsync(GamePage current, GamePage next, int info, bool enableWhiteBg, CancellationToken token)
        {
            if (_fadeManager)
            {
                await _fadeManager.FadeOutAsync(0.25f, token);
            }
            else 
            {
                await UniTask.Delay(TimeSpan.FromSeconds(0.25), ignoreTimeScale: true, cancellationToken: token);
            }

            if (current) current.OnExit();
            
            if (enableWhiteBg && globalWhiteBackground)
            {
                globalWhiteBackground.gameObject.SetActive(true);
                globalWhiteBackground.SetAlpha(1f); 
            }
            
            if (next) 
            { 
                next.OnEnter(); 
                next.SetAlpha(1f); 
                HandleTrigger(next, info); 
            }
            
            if (_fadeManager) 
            {
                await _fadeManager.FadeInAsync(0.25f, token);
            }
        }

        private async UniTask SequenceTransitionAsync(GamePage current, GamePage next, Image background, int info, float waitTime, CancellationToken token)
        {
            if (background) background.gameObject.SetActive(true);
            
            if (current) 
            { 
                await FadePageAsync(current, 1f, 0f, 0.5f, token); 
                current.OnExit(); 
            }
            
            if (waitTime > 0f) 
            {
                await UniTask.Delay(TimeSpan.FromSeconds(waitTime), ignoreTimeScale: true, cancellationToken: token);
            }
            
            if (next) 
            { 
                next.OnEnter(); 
                next.SetAlpha(0f); 
                HandleTrigger(next, info); 
                await FadePageAsync(next, 0f, 1f, 0.5f, token); 
            }
            
            if (background) background.gameObject.SetActive(false);
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
            if (pages[3] is Page_Camera cameraPage)
            {
                string userIdStr = HasActiveSession ? _sessionManager.CurrentUserId.ToString() : "0";
                cameraPage.SetPhotoFilename(ZString.Format("{0}_{1}", userIdStr, levelID));
            }
        }

        private void MoveToNextLevelDynamic()
        {
            if (_isTutorialMode)
            {
                string firstScene = GetNextSceneName(1); 
                if (useFadeTransition && _gameManager) _gameManager.ChangeScene(firstScene);
                else SceneLoader.LoadAsync(firstScene).Forget();
                return;
            }

            int nextNum = _currentQuestionNumber + 1;
            string nextScene = GetNextSceneName(nextNum);

            if (useFadeTransition && _gameManager) _gameManager.ChangeScene(nextScene);
            else SceneLoader.LoadAsync(nextScene).Forget();
        }

        private string GetNextSceneName(int qNum)
        {
            if (qNum > 15) return GameConstants.Scene.Ending;
            return ZString.Format("Play_Q{0}", qNum);
        }
        
        private void HandleTrigger(GamePage page, int info)
        {
            if (info != 0 && page is ITriggerReceiver receiver)
            {
                receiver.ReceiveTrigger(info);
            }
        }
    }
}