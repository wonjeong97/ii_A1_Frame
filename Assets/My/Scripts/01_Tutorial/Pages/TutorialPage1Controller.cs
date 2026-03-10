using System;
using System.Collections;
using Cysharp.Threading.Tasks; 
using My.Scripts.Core;
using My.Scripts.Core.Pages;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary> 튜토리얼 1페이지용 데이터 구조체 </summary>
    [Serializable]
    public class TutorialPage1Data
    {
        public TextSetting descriptionText; 
        public string warningMessage; 
        public string resetMessage;   
    }

    /// <summary>
    /// 유저 입장을 대기하고 상세 정보를 로드하는 튜토리얼의 첫 관문입니다.
    /// 서버로부터 유저 UID를 획득한 후 세션 데이터를 완전히 구성할 때까지 대기합니다.
    /// </summary>
    public class TutorialPage1Controller : PopupGamePage<TutorialPage1Data>
    {
        [Header("Page 1 UI")]
        [SerializeField] private Text descriptionText;
        
        [Header("API Manager")]
        [SerializeField] private APIManager apiManager;
        
        [Header("Polling Settings")]
        [SerializeField] private float basePollInterval = 1.0f; 
        [SerializeField] private float maxPollInterval = 10.0f; 

        private float _currentPollInterval; 
        private readonly float fadeTime = 1f;
        private Coroutine _pollCoroutine; 

        /// <summary> 페이드인 연출을 위해 초기 알파값을 0으로 설정합니다. </summary>
        protected override void Awake()
        {
            base.Awake();
            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = 0f;
                descriptionText.color = c;
            }
        }

        /// <summary> JSON 설정 데이터를 UI 컴포넌트 및 부모 팝업 메시지에 주입합니다. </summary>
        protected override void SetupData(TutorialPage1Data data)
        {
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        /// <summary> 페이지 진입 시 안내 문구 연출과 서버 상태 폴링을 개시합니다. </summary>
        public override void OnEnter()
        {
            base.OnEnter(); 
            ResetIdleState(true);
            if (descriptionText) StartCoroutine(FadeInTextRoutine());
            if (_pollCoroutine != null) StopCoroutine(_pollCoroutine);
            _pollCoroutine = StartCoroutine(PollRoomStateRoutine());
        }

        /// <summary> 페이지 퇴장 시 불필요한 네트워크 리소스 점유를 막기 위해 폴링을 중단합니다. </summary>
        public override void OnExit()
        {
            if (_pollCoroutine != null)
            {
                StopCoroutine(_pollCoroutine);
                _pollCoroutine = null;
            }
            base.OnExit();
        }

        /// <summary> 관리자용 강제 스킵(Return) 및 무응답 타이머 리셋을 처리합니다. </summary>
        private void Update()
        {
            if (Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false); 
                if (Input.GetKeyDown(KeyCode.Return)) CompleteStep(); 
            }
            else UpdateInactivity();
        }

        /// <summary> 
        /// 방 상태와 유저 UID를 주기적으로 확인하며, 상세 정보 로드가 완료될 때까지 루프를 유지합니다.
        /// </summary>
        private IEnumerator PollRoomStateRoutine()
        {
            float emptyUserStartTime = -1f; 
            _currentPollInterval = basePollInterval;

            while (true)
            {
                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);
                    continue;
                }

                string checkUrl = $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code={GameConstants.Module.Code.ToLower()}";
                string userUrl = $"{GameManager.Instance.ApiConfig.GetCurrentRoomUserUrl}?code={GameConstants.Module.Code.ToLower()}";

                bool isRoomEmpty = false;
                bool isNetworkError = false;

                // 1. 방 사용 여부 확인
                using (UnityWebRequest stateReq = UnityWebRequest.Get(checkUrl))
                {
                    stateReq.timeout = 10; 
                    yield return stateReq.SendWebRequest();

                    if (stateReq.result == UnityWebRequest.Result.Success)
                    {
                        _currentPollInterval = basePollInterval;
                        if (stateReq.downloadHandler.text.IndexOf(GameConstants.Api.StatusEmpty, StringComparison.OrdinalIgnoreCase) >= 0)
                            isRoomEmpty = true;
                    }
                    else isNetworkError = true;
                }

                // 에러 발생 시 지수 백오프 적용하여 서버 부하 경감
                if (isNetworkError)
                {
                    _currentPollInterval = Mathf.Min(_currentPollInterval * 2f, maxPollInterval);
                    yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);
                    continue; 
                }

                // 방이 비었을 경우 타이틀로 강제 회귀하여 세션 무결성 유지
                if (isRoomEmpty)
                {
                    if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
                    else SceneManager.LoadScene(GameConstants.Scene.Title);
                    yield break;
                }

                bool isUserEmpty = false;
                // 2. 진입한 유저의 UID 획득 및 상세 정보 로드
                using (UnityWebRequest userReq = UnityWebRequest.Get(userUrl))
                {
                    userReq.timeout = 10; 
                    yield return userReq.SendWebRequest();

                    if (userReq.result == UnityWebRequest.Result.Success)
                    {
                        string rawText = userReq.downloadHandler.text;
                        if (rawText.IndexOf(GameConstants.Api.StatusEmpty, StringComparison.OrdinalIgnoreCase) >= 0) isUserEmpty = true;
                        else if (rawText.Contains(","))
                        {
                            emptyUserStartTime = -1f;
                            string[] parts = rawText.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 1)
                            {
                                string uidLeft = parts[0].Trim();
                                if (parts.Length >= 2 && SessionManager.Instance)
                                {
                                    SessionManager.Instance.PlayerAUid = uidLeft;
                                    SessionManager.Instance.PlayerBUid = parts[1].Trim();
                                }

                                if (apiManager)
                                {   
                                    bool fetchSuccess = false;
                                    bool fetchFaulted = false;

                                    // 비동기 API 응답과 코루틴 흐름을 동기화하기 위해 ToCoroutine 활용
                                    yield return apiManager.FetchDataAsync(uidLeft)
                                                           .Timeout(TimeSpan.FromSeconds(11))
                                                           .ToCoroutine(
                                                                r => fetchSuccess = r, 
                                                                ex => {                
                                                                    if (ex is TimeoutException) Debug.LogWarning("[TutorialPage1] 로드 타임아웃.");
                                                                    fetchFaulted = true;
                                                                }
                                                            );

                                    // 데이터 로드 실패 시 다음 주기에서 재시도하여 중단 없는 진행 유도
                                    if (fetchFaulted || !fetchSuccess || !SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0)
                                    {
                                        yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);
                                        continue;
                                    }
                                }
                                CompleteStep(); 
                                yield break; 
                            }
                        }
                    }
                }

                // 일시적인 데이터 누락(15초 미만)은 네트워크 지연으로 간주하여 유예 시간 부여
                if (isUserEmpty)
                {
                    if (emptyUserStartTime < 0f) emptyUserStartTime = Time.time;
                    if (Time.time - emptyUserStartTime >= 15f)
                    {
                        if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
                        yield break;
                    }
                }
                else emptyUserStartTime = -1f; 

                yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);
            }
        }

        /// <summary> 텍스트 투명도를 점진적으로 증가시켜 시각적 몰입감을 부여합니다. </summary>
        private IEnumerator FadeInTextRoutine()
        {
            float timer = 0f;
            while (timer < fadeTime)
            {
                timer += Time.deltaTime;
                if (descriptionText)
                {
                    Color c = descriptionText.color;
                    c.a = Mathf.Clamp01(timer / fadeTime);
                    descriptionText.color = c;
                }
                yield return null;
            }
        }
    }
}