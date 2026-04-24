using System;
using System.Collections;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using My.Scripts.Core.Data;
using My.Scripts.Core.Pages;
using My.Scripts.Global;
using My.Scripts.Hardware;
using My.Scripts.Timelapse;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core
{
    /// <summary>
    /// 단일 레벨(문항)의 페이지 흐름을 관리하고, 사용자 데이터 기반으로 UI를 초기화함.
    /// </summary>
    public class LevelManager : BaseFlowManager
    {   
        public static LevelManager Instance { get; private set; }
        
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
        /// </summary>
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

                    ReplaceNamesInQnAPage(tSetting.Page2, nameA, nameB);
                    ReplaceNamesInTransitionPage(tSetting.Page4, nameA, nameB);
                    ReplaceNamesInTransitionPage(tSetting.Page6, nameA, nameB);
                    ReplaceNamesInTransitionPage(tSetting.Page7, nameA, nameB);
                    ReplaceNamesInTutorialPage8(tSetting.Page8, nameA, nameB);

                    SetCameraFileName();
                    ConfigureCameraPage(false);

                    SetupPageData(0, tSetting.Page1);
                    SetupPageData(1, tSetting.Page2);
                    SetupPageData(2, tSetting.Page4);
                    SetupPageData(3, tSetting.Page7); 
                    SetupPageData(4, tSetting.Page8); 
                }
                else
                {
                    Debug.LogWarning("TutorialLevelSetting 데이터를 로드하지 못함.");
                }
            }
            else
            {
                UserType uType = HasActiveSession ? SessionManager.Instance.CurrentUserType : levelType;
                StandardLevelSetting sSetting = LevelDataLoader.LoadStandardLevel(levelID, uType);
                if (sSetting != null)
                {
                    string nameA = GetPlayerNameOrDefault(true);
                    string nameB = GetPlayerNameOrDefault(false);
                    
                    ReplaceNamesInQnAPage(sSetting.Page2, nameA, nameB);
                    ReplaceNamesInTransitionPage(sSetting.Page4, nameA, nameB);
                    ReplaceNamesInTransitionPage(sSetting.Page6, nameA, nameB);

                    SetCameraFileName();
                    ConfigureCameraPage(true);

                    SetupPageData(0, sSetting.Page1);
                    SetupPageData(1, sSetting.Page2);
                    SetupPageData(2, sSetting.Page4); 
                    SetupPageData(4, sSetting.Page6); 
                }
                else
                {
                    Debug.LogWarning("StandardLevelSetting 데이터를 로드하지 못함.");
                }

                if (HueManager.Instance && _currentQuestionNumber == 6)
                {
                    HueManager.Instance.InitRandomColors();
                }
            }
        }
       
        /// <summary>
        /// TransitionPage(4, 5, 7)의 텍스트에 포함된 이름 포맷 태그를 실제 플레이어 닉네임으로 치환함.
        /// </summary>
        private void ReplaceNamesInTransitionPage(TransitionPageData pageData, string nameA, string nameB)
        {
            if (pageData == null) return;

            // # TODO: string.Replace 연속 호출은 문자열 재할당을 유발하므로, 성능 크리티컬 구간이라면 StringBuilder 포맷팅으로 최적화 필요.
            if (pageData.descriptionText != null && !string.IsNullOrEmpty(pageData.descriptionText.text))
            {
                pageData.descriptionText.text = pageData.descriptionText.text.Replace("{nameA}", nameA).Replace("{nameB}", nameB);
            }

            if (pageData.playerAName != null && !string.IsNullOrEmpty(pageData.playerAName.text))
            {
                pageData.playerAName.text = pageData.playerAName.text.Replace("{nameA}", nameA).Replace("{nameB}", nameB);
            }

            if (pageData.playerBName != null && !string.IsNullOrEmpty(pageData.playerBName.text))
            {
                pageData.playerBName.text = pageData.playerBName.text.Replace("{nameA}", nameA).Replace("{nameB}", nameB);
            }
        }

        /// <summary>
        /// TutorialPage8의 텍스트에 포함된 이름 포맷 태그를 실제 플레이어 닉네임으로 치환함.
        /// </summary>
        private void ReplaceNamesInTutorialPage8(TutorialPage8Data pageData, string nameA, string nameB)
        {
            if (pageData == null) return;

            if (pageData.introText != null && !string.IsNullOrEmpty(pageData.introText.text))
            {
                pageData.introText.text = pageData.introText.text.Replace("{nameA}", nameA).Replace("{nameB}", nameB);
            }

            if (pageData.countdownText != null && !string.IsNullOrEmpty(pageData.countdownText.text))
            {
                pageData.countdownText.text = pageData.countdownText.text.Replace("{nameA}", nameA).Replace("{nameB}", nameB);
            }

            if (pageData.startText != null && !string.IsNullOrEmpty(pageData.startText.text))
            {
                pageData.startText.text = pageData.startText.text.Replace("{nameA}", nameA).Replace("{nameB}", nameB);
            }
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
        /// QnA 페이지의 닉네임 텍스트 포맷 태그를 치환함.
        /// </summary>
        private void ReplaceNamesInQnAPage(QnAPageData page2, string nameA, string nameB)
        {
            if (page2 == null) return;
            
            if (page2.nicknamePlayerA != null && !string.IsNullOrEmpty(page2.nicknamePlayerA.text))
            {
                page2.nicknamePlayerA.text = page2.nicknamePlayerA.text.Replace("{nameA}", nameA);
            }
            if (page2.nicknamePlayerB != null && !string.IsNullOrEmpty(page2.nicknamePlayerB.text))
            {
                page2.nicknamePlayerB.text = page2.nicknamePlayerB.text.Replace("{nameB}", nameB);
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
        /// 페이지 간의 페이드 및 암전 등 트랜지션 효과를 코루틴으로 처리함.
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
            
            if (targetIndex == 2 && pages.Length > 3 && pages[3] is Page_Camera camPage)
            {
                camPage.PreloadCamera(); 
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
                else if (currentPageIndex == 1 && targetIndex == 2) 
                { 
                    yield return StartCoroutine(RevealTransition(current, next, info)); 
                    handled = true; 
                } 
                else if (currentPageIndex == 2 && targetIndex == 3) 
                { 
                    yield return StartCoroutine(AmjeonTransition(current, next, info, true)); 
                    handled = true; 
                } 
                else if (currentPageIndex == 3 && targetIndex == 4) 
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
                else if (currentPageIndex == 1 && targetIndex == 2) 
                { 
                    yield return StartCoroutine(RevealTransition(current, next, info)); 
                    handled = true; 
                } 
                else if (currentPageIndex == 2 && targetIndex == 3) 
                { 
                    yield return StartCoroutine(AmjeonTransition(current, next, info)); 
                    handled = true; 
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
                    
                    if (currentPageIndex == -1 && next is Page_Grid) 
                    {
                        next.SetAlpha(1f);
                    }
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

        /// <summary>
        /// 글로벌 블랙 캔버스를 덮은 상태에서 페이지를 전환함.
        /// </summary>
        private IEnumerator CoverTransition(GamePage current, GamePage next, int info)
        {
            if (globalBlackCanvasGroup) yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 0f, 1f, 0.5f));
            yield return CoroutineData.GetWaitForSeconds(0.5f);
            if (current) current.OnExit();
            if (next) 
            { 
                next.OnEnter(); 
                next.SetAlpha(0f); 
                HandleTrigger(next, info); 
            }
            if (next) yield return StartCoroutine(FadePage(next, 0f, 1f));
            if (globalBlackCanvasGroup) yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 1f, 0f, 0.5f));
        }

        /// <summary>
        /// 글로벌 블랙 캔버스가 덮여 있는 상태에서 시작하여 페이지를 노출함.
        /// </summary>
        private IEnumerator RevealTransition(GamePage current, GamePage next, int info)
        {
            if (globalBlackCanvasGroup) globalBlackCanvasGroup.alpha = 1f;
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
            if (globalBlackCanvasGroup) yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 1f, 0f, 0.5f));
        }

        /// <summary>
        /// 전체 화면 암전(FadeManager)을 활용하여 페이지를 전환함.
        /// </summary>
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

        /// <summary>
        /// 백그라운드 이미지를 활성화한 상태에서 순차적인 페이드 교차를 수행함.
        /// </summary>
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

        /// <summary>
        /// 대상 캔버스 그룹의 알파값을 설정된 시간 동안 선형 보간함.
        /// </summary>
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