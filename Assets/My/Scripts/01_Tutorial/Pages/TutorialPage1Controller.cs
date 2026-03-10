using System;
using System.Collections;
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
    /// 튜토리얼 시작 전 유저 데이터를 대기하고 검증하는 페이지 컨트롤러.
    /// 서버에서 정상적인 유저 정보가 확인되어야만 다음 단계로 넘깁니다.
    /// </summary>
    public class TutorialPage1Controller : PopupGamePage<TutorialPage1Data>
    {
        [Header("Page 1 UI")]
        [SerializeField] private Text descriptionText;
        
        [Header("API Manager")]
        [SerializeField] private APIManager apiManager;
        
        [Header("Polling Settings")]
        [SerializeField] private float basePollInterval = 1.0f; // 기본 API 폴링 간격
        [SerializeField] private float maxPollInterval = 10.0f; // 통신 실패 시 최대 대기 시간 한도

        private float _currentPollInterval; // 현재 적용된 폴링 간격
        private readonly float fadeTime = 1f;
        private Coroutine _pollCoroutine; 

        /// <summary> UI 초기 투명도 설정 (페이드인 연출 준비) </summary>
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

        /// <summary> JSON에서 로드한 텍스트 및 팝업 메시지 데이터 주입 </summary>
        protected override void SetupData(TutorialPage1Data data)
        {
            if (descriptionText) 
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            }
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        /// <summary> 페이지 진입 시 텍스트 연출 및 API 폴링 루프 시작 </summary>
        public override void OnEnter()
        {
            base.OnEnter(); 

            ResetIdleState(true);

            if (descriptionText)
            {
                StartCoroutine(FadeInTextRoutine());
            }

            if (_pollCoroutine != null) StopCoroutine(_pollCoroutine);
            _pollCoroutine = StartCoroutine(PollRoomStateRoutine());
        }

        /// <summary> 페이지 퇴장 시 진행 중인 네트워크 폴링 강제 중단 (메모리 누수 방지) </summary>
        public override void OnExit()
        {
            if (_pollCoroutine != null)
            {
                StopCoroutine(_pollCoroutine);
                _pollCoroutine = null;
            }
            
            base.OnExit();
        }

        /// <summary> 관리자 강제 스킵(Return) 및 유저 입력 감지(무응답 타이머 리셋) </summary>
        private void Update()
        {
            if (Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false); 

                if (Input.GetKeyDown(KeyCode.Return))
                {
                    CompleteStep(); 
                }
            }
            else
            {
                UpdateInactivity();
            }
        }

        /// <summary> 
        /// 방 상태와 유저 데이터를 주기적으로 확인하여 게임 진행 여부 결정.
        /// 지수 백오프 로직을 통해 네트워크 불안정 시 부하를 방지합니다.
        /// </summary>
        private IEnumerator PollRoomStateRoutine()
        {
            // 유저 데이터 일시 누락 방어용 타이머
            float emptyUserStartTime = -1f; 
            _currentPollInterval = basePollInterval;

            while (true)
            {
                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);
                    continue;
                }

                string checkRoomStateUrl = $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code={GameConstants.Module.Code.ToLower()}";
                string getCurrentUserUrl = $"{GameManager.Instance.ApiConfig.GetCurrentRoomUserUrl}?code={GameConstants.Module.Code.ToLower()}";

                bool isRoomEmpty = false;
                bool isNetworkError = false;

                // 1. 방 상태 검증
                using (UnityWebRequest stateReq = UnityWebRequest.Get(checkRoomStateUrl))
                {
                    stateReq.timeout = 10; 
                    
                    yield return stateReq.SendWebRequest();

                    if (stateReq.result != UnityWebRequest.Result.ConnectionError && 
                        stateReq.result != UnityWebRequest.Result.ProtocolError)
                    {
                        // 통신 성공 시 주기 복구
                        _currentPollInterval = basePollInterval;
                        
                        string responseText = stateReq.downloadHandler.text;
                        if (!string.IsNullOrEmpty(responseText) && responseText.IndexOf(GameConstants.Api.StatusEmpty, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            isRoomEmpty = true;
                        }
                    }
                    else
                    {
                        isNetworkError = true;
                        Debug.LogWarning($"[TutorialPage1] 방 상태 통신 실패: {stateReq.error}");
                    }
                }

                // 에러 발생 시 백오프 적용 후 현재 루프 즉시 종료
                if (isNetworkError)
                {
                    _currentPollInterval = Mathf.Min(_currentPollInterval * 2f, maxPollInterval);
                    Debug.Log($"[TutorialPage1] 백오프 적용: {_currentPollInterval}초 후 재시도합니다.");
                    yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);
                    continue; 
                }

                // 방이 비었다면 정상적인 흐름이 아니므로 대기 없이 즉시 타이틀로 강제 초기화
                if (isRoomEmpty)
                {
                    Debug.LogWarning("[TutorialPage1] 방 상태가 EMPTY로 감지되었습니다. 즉시 타이틀로 되돌아갑니다.");
                    if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
                    else SceneManager.LoadScene(GameConstants.Scene.Title);
                    yield break;
                }

                bool isUserEmpty = false;

                // 2. 방이 사용 중일 때 유저 데이터(UID) 검증
                using (UnityWebRequest userReq = UnityWebRequest.Get(getCurrentUserUrl))
                {
                    userReq.timeout = 10; 
                    
                    yield return userReq.SendWebRequest();

                    if (userReq.result != UnityWebRequest.Result.ConnectionError && 
                        userReq.result != UnityWebRequest.Result.ProtocolError)
                    {
                        // 통신 성공 시 주기 복구
                        _currentPollInterval = basePollInterval;
                        
                        string rawText = userReq.downloadHandler.text;
                        
                        if (rawText.IndexOf(GameConstants.Api.StatusEmpty, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            isUserEmpty = true;
                        }
                        else if (rawText.Contains(","))
                        {
                            emptyUserStartTime = -1f; // 유저 데이터 수신 성공, 타이머 초기화

                            string cleanData = "";
                            string[] lines = rawText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            
                            foreach (string line in lines)
                            {
                                string trimmed = line.Trim();
                                if (trimmed.Contains(",") && !trimmed.StartsWith("<"))
                                {
                                    cleanData = trimmed;
                                    break;
                                }
                            }

                            if (!string.IsNullOrEmpty(cleanData))
                            {
                                string[] parts = cleanData.Split(',');
                                if (parts.Length >= 1)
                                {
                                    string uidLeft = parts[0].Trim();

                                    if (parts.Length >= 2 && SessionManager.Instance)
                                    {
                                        SessionManager.Instance.PlayerAUid = uidLeft;
                                        SessionManager.Instance.PlayerBUid = parts[1].Trim();
                                    }

                                    // UID를 기반으로 상세 유저 정보 로드 요청
                                    if (apiManager)
                                    {   
                                        if (SessionManager.Instance) SessionManager.Instance.CurrentUserId = 0;
                                        apiManager.FetchData(uidLeft);
                                        float timeoutAt = Time.time + 11f; // 상세 정보 응답 최대 11초 대기
                                        
                                        while (SessionManager.Instance &&
                                               SessionManager.Instance.CurrentUserId == 0 &&
                                               Time.time < timeoutAt)
                                        {
                                            yield return null;
                                        }

                                        if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0)
                                        {
                                            Debug.LogWarning("[TutorialPage1] CurrentUserId 확정 실패. 다음 폴링에서 재시도합니다.");
                                            yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        Debug.LogWarning("[TutorialPage1] APIManager를 찾을 수 없습니다.");
                                        yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);
                                        continue;
                                    }
                                }
                                
                                // 유저 정보 세팅 완벽히 끝남, 튜토리얼 본격 시작
                                CompleteStep(); 
                                yield break; 
                            }
                        }
                    }
                    else
                    {
                        isNetworkError = true;
                        Debug.LogWarning($"[TutorialPage1] 유저 데이터 통신 실패: {userReq.error}");
                    }
                }

                // 두 번째 통신에서 에러 발생 시 백오프 적용
                if (isNetworkError)
                {
                    _currentPollInterval = Mathf.Min(_currentPollInterval * 2f, maxPollInterval);
                    Debug.Log($"[TutorialPage1] 백오프 적용: {_currentPollInterval}초 후 재시도합니다.");
                    yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);
                    continue; 
                }

                // 서버나 통신 지연으로 유저 상태만 누락될 경우를 대비해 15초 유예 대기
                if (isUserEmpty)
                {
                    if (emptyUserStartTime < 0f) emptyUserStartTime = Time.time;
                    
                    if (Time.time - emptyUserStartTime >= 15f)
                    {
                        Debug.LogWarning($"[TutorialPage1] 유저 상태 15초 연속 EMPTY 감지. 강제 초기화를 진행합니다.");
                        if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
                        else SceneManager.LoadScene(GameConstants.Scene.Title);
                        yield break;
                    }
                }
                else
                {
                    emptyUserStartTime = -1f; 
                }

                yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);
            }
        }

        /// <summary> 안내 텍스트 부드러운 등장 연출 </summary>
        private IEnumerator FadeInTextRoutine()
        {
            float timer = 0f;

            while (timer < fadeTime)
            {
                timer += Time.deltaTime;
                float alpha = Mathf.Clamp01(timer / fadeTime);

                if (descriptionText)
                {
                    Color c = descriptionText.color;
                    c.a = alpha;
                    descriptionText.color = c;
                }
                yield return null;
            }

            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = 1f;
                descriptionText.color = c;
            }
        }
    }
}