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
    /// 단일 레벨(Q1~Q15, Tutorial)의 전체 생명주기와 페이지 전환 흐름을 제어하는 싱글톤 매니저입니다.
    /// JSON 데이터를 로드하여 각 하위 페이지에 주입하고, 페이지 간의 특수 전환 연출(페이드, 암전 등)을 조율합니다.
    /// </summary>
    public class LevelManager : BaseFlowManager
    {   
        public static LevelManager Instance { get; private set; }
        
        [Header("Level Settings")]
        [SerializeField] private UserType levelType = UserType.A;
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
        
        /// <summary> 세션 매니저 존재 및 유효 유저 여부를 확인하는 프로퍼티. 런타임 NullReferenceException을 방지합니다. </summary>
        private bool HasActiveSession =>  SessionManager.Instance && SessionManager.Instance.CurrentUserId != 0;
        
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
        /// 현재 레벨 모드(Tutorial vs Standard)에 맞춰 JSON 설정 데이터를 로드하고, 각 페이지 컨트롤러에 분배합니다.
        /// 동적 텍스트(이름 치환) 및 카메라 설정도 함께 초기화합니다.
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

                    // 텍스트 템플릿의 플레이어 이름 플레이스홀더({name})를 실제 세션 이름으로 치환
                    if (tSetting.Page7 != null)
                    {
                        if (tSetting.Page7.playerAName != null && !string.IsNullOrEmpty(tSetting.Page7.playerAName.text))
                            tSetting.Page7.playerAName.text = tSetting.Page7.playerAName.text.Replace("{nameA}", nameA);
                            
                        if (tSetting.Page7.playerBName != null && !string.IsNullOrEmpty(tSetting.Page7.playerBName.text))
                            tSetting.Page7.playerBName.text = tSetting.Page7.playerBName.text.Replace("{nameB}", nameB);
                    }

                    ReplaceNamesInCheckPage(tSetting.Page3, nameA, nameB);
                    SetCameraFileName();
                    ConfigureCameraPage(false);

                    SetupPageData(0, tSetting.Page1);
                    SetupPageData(1, tSetting.Page2);
                    SetupPageData(2, tSetting.Page3);
                    SetupPageData(3, tSetting.Page4);
                    SetupPageData(5, tSetting.Page6);
                    SetupPageData(6, tSetting.Page7);
                    SetupPageData(7, tSetting.Page8);
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
                    ReplaceNamesInCheckPage(sSetting.Page3, nameA, nameB);

                    SetCameraFileName();
                    ConfigureCameraPage(true);

                    SetupPageData(0, sSetting.Page1);
                    SetupPageData(1, sSetting.Page2);
                    SetupPageData(2, sSetting.Page3);
                    SetupPageData(3, sSetting.Page4);
                    SetupPageData(5, sSetting.Page6);
                }
            }
        }
        
        /// <summary> 세션 데이터가 없을 경우 표시할 기본 Fallback 닉네임을 반환합니다. </summary>
        private string GetPlayerNameOrDefault(bool isPlayerA)
        {
            if (HasActiveSession)
            {
                string lastName = isPlayerA ? SessionManager.Instance.PlayerAFirstName : SessionManager.Instance.PlayerBFirstName;
                if (!string.IsNullOrWhiteSpace(lastName)) return lastName;
            }
            return isPlayerA ? "PlayerA" : "PlayerB";
        }

        /// <summary> 체크 페이지(Page3)의 안내 문구 내 플레이스홀더를 실제 이름으로 치환합니다. </summary>
        private void ReplaceNamesInCheckPage(CheckPageData page3, string nameA, string nameB)
        {
            if (page3 == null) return;
    
            if (page3.nicknamePlayerA != null && !string.IsNullOrEmpty(page3.nicknamePlayerA.text))
                page3.nicknamePlayerA.text = page3.nicknamePlayerA.text.Replace("{nameA}", nameA);
    
            if (page3.nicknamePlayerB != null && !string.IsNullOrEmpty(page3.nicknamePlayerB.text))
                page3.nicknamePlayerB.text = page3.nicknamePlayerB.text.Replace("{nameB}", nameB);
        }
        
        /// <summary> 레벨 ID 문자열("Q1", "Q15" 등)에서 숫자만 추출하여 정수로 파싱합니다. </summary>
        private int ParseLevelNumber(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            string numStr = Regex.Replace(id, "[^0-9]", "");
            if (int.TryParse(numStr, out int num)) return num;
            return 0;
        }

        /// <summary> 페이드 아웃 연출을 위한 전역 캔버스 그룹을 초기화하고, Q1 진입 시 타임랩스 기록을 리셋합니다. </summary>
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

        /// <summary> 안전하게 인덱스 범위를 체크하여 특정 페이지 컴포넌트에 데이터를 주입합니다. </summary>
        private void SetupPageData(int index, object data)
        {
            if (pages != null && index >= 0 && index < pages.Length && pages[index])
                pages[index].SetupData(data);
        }

        /// <summary> 레벨 내 모든 페이지 흐름이 종료되었을 때 호출되며, 다음 씬(다음 문제 또는 엔딩)으로 이동합니다. </summary>
        protected override void OnAllFinished()
        {
            if (!_isTutorialMode && _currentQuestionNumber == 15) StartCoroutine(Q15TransitionRoutine());
            else MoveToNextLevelDynamic(); 
        }

        /// <summary> 마지막 문제(Q15) 종료 시 엔딩 씬으로 부드럽게 넘어가기 위한 특수 페이드 연출 코루틴입니다. </summary>
        private IEnumerator Q15TransitionRoutine()
        {
            GamePage current = (currentPageIndex >= 0 && currentPageIndex < pages.Length) ? pages[currentPageIndex] : null;
            
            if (current is Page_Transition transitionPage)
            {
                yield return StartCoroutine(transitionPage.FadeOutTextOnly(1.0f)); 
                current.OnExit();
            }
            else if (current)
            {
                yield return StartCoroutine(FadePage(current, 1f, 0f, 1.0f)); 
                current.OnExit();
            }
            
            yield return CoroutineData.GetWaitForSeconds(0.5f); 
            SceneManager.LoadScene(GameConstants.Scene.Ending);
        }

        /// <summary> 
        /// 이전 페이지에서 다음 페이지로 넘어갈 때 인덱스 구간별로 차별화된 연출(Cover, Reveal, Amjeon)을 적용합니다. 
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
            
            // 카메라 하드웨어 로딩 지연을 숨기기 위해 직전 페이지(3번 인덱스)에서 카메라를 백그라운드 예열
            if (targetIndex == 3 && pages.Length > 4 && pages[4] is Page_Camera camPage)
            {
                camPage.PreloadCamera(); 
            }

            GamePage next = pages[targetIndex];
            bool handled = false;

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

            // 매칭된 특수 전환이 없을 경우 기본 페이드 전환 수행
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

        /// <summary> 특정 레벨(Q15)에서 카메라 모듈에 특수 영상 변환 트리거를 전달하도록 동적 설정합니다. </summary>
        private void ConfigureCameraPage(bool save)
        {
            if (pages.Length > 4 && pages[4] is Page_Camera camPage)
            {
                bool isQ15 = string.Equals(levelID, GameConstants.Level.Q15, StringComparison.OrdinalIgnoreCase);
                camPage.Configure(save, cameraMaskMaterial, isQ15);
                camPage.SetLevelID(levelID);
            }
        }

        /// <summary> 타임랩스 사진 저장 시 중복을 막고 서버 매칭을 위해 유저 ID와 레벨 ID를 결합한 파일명을 구성합니다. </summary>
        private void SetCameraFileName()
        {
            if (pages.Length <= 4) return;
            Page_Camera cameraPage = pages[4] as Page_Camera;
            if (!cameraPage) return;
            
            string userIdStr = HasActiveSession ? SessionManager.Instance.CurrentUserId.ToString() : "0";
            cameraPage.SetPhotoFilename($"{userIdStr}_{levelID}");
        }

        /// <summary> 현재 레벨 종료 후, 유저 타입에 맞는 다음 레벨 씬 이름을 조합하여 이동합니다. </summary>
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

        /// <summary> 레벨 번호와 현재 유저 타입(A~F)을 기반으로 대상 씬의 정확한 문자열 이름을 반환합니다. </summary>
        private string GetNextSceneName(int qNum)
        {
            if (qNum > 15) return GameConstants.Scene.Ending;

            string suffix = GameManager.Instance ? GameManager.Instance.GetLevelSuffix(qNum) : "";
            return $"Play_Q{qNum}{suffix}"; 
        }
        
        /// <summary> 이전 페이지(ex: QnA)에서 선택한 응답 번호 등의 데이터를 다음 대기 페이지(ex: Check)로 전달합니다. </summary>
        private void HandleTrigger(GamePage page, int info)
        {
            if (info != 0 && page is ITriggerReceiver receiver)
            {
                receiver.ReceiveTrigger(info);
            }
        }

        /// <summary> 화면 전체를 검은색 캔버스로 덮은 뒤 다음 페이지를 로드하여 장면 전환의 어색함을 숨깁니다. </summary>
        private IEnumerator CoverTransition(GamePage current, GamePage next, int info)
        {
            if (globalBlackCanvasGroup) yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 0f, 1f, 0.5f));
            yield return CoroutineData.GetWaitForSeconds(0.5f);
            if (current) current.OnExit();
            if (next) { next.OnEnter(); next.SetAlpha(0f); HandleTrigger(next, info); }
            if (next) yield return StartCoroutine(FadePage(next, 0f, 1f));
            if (globalBlackCanvasGroup) yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 1f, 0f, 0.5f));
        }

        /// <summary> 덮여있던 전역 검은 캔버스를 걷어내며 다음 페이지를 드러내는 연출입니다. </summary>
        private IEnumerator RevealTransition(GamePage current, GamePage next, int info)
        {
            if (globalBlackCanvasGroup) globalBlackCanvasGroup.alpha = 1f;
            if (current) { yield return StartCoroutine(FadePage(current, 1f, 0f)); current.OnExit(); }
            if (next) { next.OnEnter(); next.SetAlpha(0f); HandleTrigger(next, info); yield return StartCoroutine(FadePage(next, 0f, 1f)); }
            if (globalBlackCanvasGroup) yield return StartCoroutine(FadeCanvasGroup(globalBlackCanvasGroup, 1f, 0f, 0.5f));
        }

        /// <summary> 화면을 완전히 암전시켰다가 밝아지는 효과(FadeManager 연동)를 통해 긴장감을 조성하는 전환입니다. </summary>
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
                Color c = globalWhiteBackground.color; c.a = 1f; globalWhiteBackground.color = c;
            }
            if (next) { next.OnEnter(); next.SetAlpha(1f); HandleTrigger(next, info); }
            if (FadeManager.Instance) FadeManager.Instance.FadeIn(1f);
        }

        /// <summary> 배경 활성화와 시간차 대기를 포함한 시퀀스 연출 기반의 전환입니다. </summary>
        private IEnumerator SequenceTransition(GamePage current, GamePage next, Image background, int info, float waitTime = 0f)
        {
            if (background) background.gameObject.SetActive(true);
            if (current) { yield return StartCoroutine(FadePage(current, 1f, 0f)); current.OnExit(); }
            if (waitTime > 0f) yield return CoroutineData.GetWaitForSeconds(waitTime);
            if (next) { next.OnEnter(); next.SetAlpha(0f); HandleTrigger(next, info); yield return StartCoroutine(FadePage(next, 0f, 1f)); }
        }

        /// <summary> CanvasGroup의 투명도를 선형 보간하는 헬퍼 코루틴입니다. </summary>
        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float s, float e, float d)
        {
            if (!cg) yield break;
            float t = 0f; cg.alpha = s;
            while (t < d) { t += Time.deltaTime; cg.alpha = Mathf.Lerp(s, e, t / d); yield return null; }
            cg.alpha = e;
        }
    }
}