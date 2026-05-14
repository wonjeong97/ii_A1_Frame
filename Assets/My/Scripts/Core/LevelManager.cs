using System;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
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
    /// </summary>
    public class LevelManager : BaseFlowManager
    {   
        public static LevelManager Instance { get; private set; }
        private readonly static System.Text.StringBuilder NameBuilder = new System.Text.StringBuilder(256);
        
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
        
        public int CurrentQuestionNumber => _currentQuestionNumber;
        
        private bool HasActiveSession => SessionManager.Instance && SessionManager.Instance.CurrentUserId != 0;
        
        private void Awake()
        {
            if (Instance && Instance != this) 
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

        /// <summary>
        /// 레벨 번호를 파싱하고, 튜토리얼 여부 및 세션 정보에 따라 적절한 JSON 데이터를 로드함.
        /// 분기문을 메서드로 추출하여 가독성과 유지보수성을 높임.
        /// </summary>
        protected override void LoadSettings()
        {
            InitializeGlobals();
            _currentQuestionNumber = ParseLevelNumber(levelID);

            if (_isTutorialMode)
            {
                LoadTutorialSettings();
            }
            else
            {
                LoadStandardSettings();
            }
        }
        
        /// <summary>
        /// 튜토리얼 전용 설정 데이터를 로드하고 각 페이지에 주입함.
        /// </summary>
        private void LoadTutorialSettings()
        {
            TutorialLevelSetting tSetting = LevelDataLoader.LoadTutorialLevel();
            if (tSetting == null)
            {
                Debug.LogWarning("TutorialLevelSetting 데이터를 로드하지 못함.");
                return;
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
        }
        
        /// <summary>
        /// 일반 문항 전용 설정 데이터를 로드하고 각 페이지에 주입함.
        /// </summary>
        private void LoadStandardSettings()
        {
            UserType uType = HasActiveSession ? SessionManager.Instance.CurrentUserType : levelType;
            StandardLevelSetting sSetting = LevelDataLoader.LoadStandardLevel(levelID, uType);
            
            if (sSetting == null)
            {
                Debug.LogWarning("StandardLevelSetting 데이터를 로드하지 못함.");
                return;
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

            if (HueManager.Instance && _currentQuestionNumber == 6)
            {
                HueManager.Instance.InitRandomColors();
            }
        }
        
        /// <summary>
        /// 카메라 파일명을 구성하고 저장 설정을 페이지에 적용함.
        /// </summary>
        private void PrepareCameraPage(bool save)
        {
            SetCameraFileName();
            ConfigureCameraPage(save);
        }
       
        /// <summary>
        /// TransitionPage의 텍스트에 포함된 이름 포맷 태그를 런타임 가비지 없이 치환함.
        /// </summary>
        private void ReplaceNamesInTransitionPage(TransitionPageData pageData, string nameA, string nameB)
        {
            if (pageData == null) return;
            ReplaceTextSettingName(pageData.descriptionText, nameA, nameB);
            ReplaceTextSettingName(pageData.playerAName, nameA, nameB);
            ReplaceTextSettingName(pageData.playerBName, nameA, nameB);
        }

        /// <summary>
        /// TutorialPage8의 텍스트에 포함된 이름 포맷 태그를 런타임 가비지 없이 치환함.
        /// </summary>
        private void ReplaceNamesInTutorialPage8(TutorialPage8Data pageData, string nameA, string nameB)
        {
            if (pageData == null) return;
            ReplaceTextSettingName(pageData.introText, nameA, nameB);
            ReplaceTextSettingName(pageData.countdownText, nameA, nameB);
            ReplaceTextSettingName(pageData.startText, nameA, nameB);
        }
        
        /// <summary>
        /// 세션에 저장된 플레이어 이름을 가져오며, 누락 시 기본값을 반환함.
        /// </summary>
        private string GetPlayerNameOrDefault(bool isPlayerA)
        {
            if (HasActiveSession)
            {
                string firstName = isPlayerA ? SessionManager.Instance.PlayerAFirstName : SessionManager.Instance.PlayerBFirstName;
                if (!string.IsNullOrWhiteSpace(firstName)) return firstName;
            }
            return isPlayerA ? "PlayerA" : "PlayerB";
        }

        /// <summary>
        /// QnA 페이지의 닉네임 텍스트 포맷 태그를 런타임 가비지 없이 개별 치환함.
        /// </summary>
        private void ReplaceNamesInQnAPage(QnAPageData pageData, string nameA, string nameB)
        {
            if (pageData == null) return;

            if (pageData.nicknamePlayerA != null && !string.IsNullOrEmpty(pageData.nicknamePlayerA.text))
            {
                NameBuilder.Clear();
                NameBuilder.Append(pageData.nicknamePlayerA.text);
                NameBuilder.Replace("{nameA}", nameA);
                pageData.nicknamePlayerA.text = NameBuilder.ToString();
            }

            if (pageData.nicknamePlayerB != null && !string.IsNullOrEmpty(pageData.nicknamePlayerB.text))
            {
                NameBuilder.Clear();
                NameBuilder.Append(pageData.nicknamePlayerB.text);
                NameBuilder.Replace("{nameB}", nameB);
                pageData.nicknamePlayerB.text = NameBuilder.ToString();
            }
        }
        
        /// <summary>
        /// StringBuilder를 재사용하여 TextSetting 내부의 문자열을 치환함.
        /// </summary>
        private void ReplaceTextSettingName(TextSetting setting, string nameA, string nameB)
        {
            if (setting != null && !string.IsNullOrEmpty(setting.text))
            {
                NameBuilder.Clear();
                NameBuilder.Append(setting.text);
                NameBuilder.Replace("{nameA}", nameA);
                NameBuilder.Replace("{nameB}", nameB);
                setting.text = NameBuilder.ToString();
            }
        }
        
        /// <summary>
        /// 씬 이름 식별자에서 정규식을 이용해 정수형 문항 번호를 추출함.
        /// </summary>
        private int ParseLevelNumber(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            
            Match match = Regex.Match(id, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int num)) 
            {
                return num;
            }
            return 0;
        }

        /// <summary>
        /// 트랜지션에 사용할 글로벌 캔버스 그룹 및 타임랩스 기록 상태를 초기화함.
        /// </summary>
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

        /// <summary>
        /// 인덱스에 해당하는 페이지 객체에 파싱된 데이터를 주입함.
        /// </summary>
        private void SetupPageData(int index, object data)
        {
            if (pages != null && index >= 0 && index < pages.Length && pages[index])
            {
                pages[index].SetupData(data);
            }
        }

        /// <summary>
        /// 단일 페이지 완료 시 다음 페이지 인덱스로 흐름을 제어함.
        /// </summary>
        protected override void OnPageComplete(int currentIndex, int nextIndex, int info)
        {
            if (!_isTutorialMode && currentIndex == 3)
            {
                OnAllFinished();
                return;
            }

            base.OnPageComplete(currentIndex, nextIndex, info);
        }

        /// <summary>
        /// 해당 문항의 모든 페이지 시퀀스가 완료되었을 때 다음 씬으로 동적 이동함.
        /// </summary>
        protected override void OnAllFinished()
        {
            MoveToNextLevelDynamic(); 
        }

        /// <summary>
        /// 페이지 간의 페이드 및 암전 등 트랜지션 효과를 UniTask로 처리함.
        /// </summary>
        protected override async UniTaskVoid TransitionAsync(int targetIndex, int info, CancellationToken token)
        {
            isTransitioning = true;
            try
            {
                GamePage current = (currentPageIndex >= 0 && currentPageIndex < pages.Length) ? pages[currentPageIndex] : null;
                
                if (targetIndex < 0 || targetIndex >= pages.Length) return;
                
                TryPreloadCamera(targetIndex);

                GamePage next = pages[targetIndex];
                
                // 특수 전환에 매칭되지 않으면 기본 전환 수행
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
        
        /// <summary>
        /// 대상 인덱스에 따라 카메라 페이지를 미리 로드함.
        /// </summary>
        private void TryPreloadCamera(int targetIndex)
        {
            if (targetIndex == 2 && pages.Length > 3)
            {
                Page_Camera camPage = pages[3] as Page_Camera;
                if (camPage)
                {
                    camPage.PreloadCamera();
                }
            }
        }
        
        /// <summary>
        /// 현재와 다음 인덱스에 맞는 특수 전환 비동기 루틴을 수행함.
        /// </summary>
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
                await UniTask.Delay(TimeSpan.FromSeconds(0.5), cancellationToken: token);
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
            if (globalBlackCanvasGroup) await UIFadeUtility.FadeCanvasGroupAsync(globalBlackCanvasGroup, 0f, 1f, 0.5f, token);
            await UniTask.Delay(TimeSpan.FromSeconds(0.5), cancellationToken: token);
            if (current) current.OnExit();
            if (next) 
            { 
                next.OnEnter(); 
                next.SetAlpha(0f); 
                HandleTrigger(next, info); 
            }
            if (next) await FadePageAsync(next, 0f, 1f, 0.5f, token);
            if (globalBlackCanvasGroup) await UIFadeUtility.FadeCanvasGroupAsync(globalBlackCanvasGroup, 1f, 0f, 0.5f, token);
        }
        
        private async UniTask RevealTransitionAsync(GamePage current, GamePage next, int info, CancellationToken token)
        {
            if (globalBlackCanvasGroup) globalBlackCanvasGroup.alpha = 1f;
            if (current) 
            { 
                await FadePageAsync(current, 1f, 0f, 0.5f, token); 
                current.OnExit(); 
            }
            if (next) 
            { 
                next.OnEnter(); 
                next.SetAlpha(0f); 
                HandleTrigger(next, info); 
                await FadePageAsync(next, 0f, 1f, 0.5f, token); 
            }
            if (globalBlackCanvasGroup) await UIFadeUtility.FadeCanvasGroupAsync(globalBlackCanvasGroup, 1f, 0f, 0.5f, token);
        }

        private async UniTask AmjeonTransitionAsync(GamePage current, GamePage next, int info, bool enableWhiteBg, CancellationToken token)
        {
            if (FadeManager.Instance)
            {
                bool d = false;
                FadeManager.Instance.FadeOut(0.25f, () => d = true);
                await UniTask.WaitUntil(() => d, cancellationToken: token);
            }
            else await UniTask.Delay(TimeSpan.FromSeconds(0.25), cancellationToken: token);

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
            
            if (FadeManager.Instance) FadeManager.Instance.FadeIn(0.25f);
        }

        private async UniTask SequenceTransitionAsync(GamePage current, GamePage next, Image background, int info, float waitTime, CancellationToken token)
        {
            if (background) background.gameObject.SetActive(true);
            if (current) 
            { 
                await FadePageAsync(current, 1f, 0f, 0.5f, token); 
                current.OnExit(); 
            }
            if (waitTime > 0f) await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: token);
            if (next) 
            { 
                next.OnEnter(); 
                next.SetAlpha(0f); 
                HandleTrigger(next, info); 
                await FadePageAsync(next, 0f, 1f, 0.5f, token); 
            }
        }

        /// <summary>
        /// 카메라 페이지(인덱스 3)의 저장 모드 및 마스킹 매터리얼을 설정함.
        /// </summary>
        private void ConfigureCameraPage(bool save)
        {
            if (pages.Length > 3 && pages[3] is Page_Camera camPage)
            {
                bool isQ15 = string.Equals(levelID, GameConstants.Level.Q15, StringComparison.OrdinalIgnoreCase);
                camPage.Configure(save, cameraMaskMaterial, isQ15);
                camPage.SetLevelID(levelID);
            }
        }

        /// <summary>
        /// 카메라 캡처 이미지의 로컬 저장소 파일명을 동적으로 구성함.
        /// </summary>
        private void SetCameraFileName()
        {
            if (pages.Length <= 3) return;
            Page_Camera cameraPage = pages[3] as Page_Camera;
            if (!cameraPage) return;
            
            string userIdStr = HasActiveSession ? SessionManager.Instance.CurrentUserId.ToString() : "0";
            // ex: userIdStr="123", levelID="Q15" -> "123_Q15"
            cameraPage.SetPhotoFilename($"{userIdStr}_{levelID}");
        }

        /// <summary>
        /// 문항 시퀀스 완료 후 다음 씬 이름을 계산하여 씬 전환을 수행함.
        /// </summary>
        private void MoveToNextLevelDynamic()
        {
            if (_isTutorialMode)
            {
                string firstScene = GetNextSceneName(1); 
                if (useFadeTransition && GameManager.Instance) GameManager.Instance.ChangeScene(firstScene);
                else SceneLoader.LoadAsync(firstScene).Forget();
                return;
            }

            int nextNum = _currentQuestionNumber + 1;
            string nextScene = GetNextSceneName(nextNum);

            if (useFadeTransition && GameManager.Instance) GameManager.Instance.ChangeScene(nextScene);
            else SceneLoader.LoadAsync(nextScene).Forget();
        }

        /// <summary>
        /// 문항 번호와 사용자 타입을 조합하여 로드해야 할 유니티 씬의 이름을 생성함.
        /// </summary>
        private string GetNextSceneName(int qNum)
        {
            if (qNum > 15) return GameConstants.Scene.Ending;
            return $"Play_Q{qNum}";
        }
        
        /// <summary>
        /// 페이지 진입 시점에 추가적인 트리거 정보를 전달함.
        /// </summary>
        private void HandleTrigger(GamePage page, int info)
        {
            if (info != 0 && page is ITriggerReceiver receiver)
            {
                receiver.ReceiveTrigger(info);
            }
        }
    }
}