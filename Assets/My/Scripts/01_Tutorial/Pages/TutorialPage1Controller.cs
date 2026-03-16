using System;
using System.Collections;
using Cysharp.Threading.Tasks; 
using My.Scripts.Core;
using My.Scripts.Core.Pages;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.Networking;
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
    
    public class TutorialPage1Controller : PopupGamePage<TutorialPage1Data>
    {
        [Header("Page 1 UI")]
        [SerializeField] private Text descriptionText;
        [Header("API Manager")]
        [SerializeField] private APIManager apiManager;
        [Header("Polling Settings")]
        [SerializeField] private float pollInterval = 3.0f; // 고정 3초 간격 폴링

        private readonly float fadeTime = 1f;
        private Coroutine _pollCoroutine; 

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

        protected override void SetupData(TutorialPage1Data data)
        {
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        public override void OnEnter()
        {
            base.OnEnter(); 
            ResetIdleState(true);
            if (descriptionText) StartCoroutine(FadeInTextRoutine());
            if (_pollCoroutine != null) StopCoroutine(_pollCoroutine);
            _pollCoroutine = StartCoroutine(PollRoomStateRoutine());
        }

        public override void OnExit()
        {
            if (_pollCoroutine != null)
            {
                StopCoroutine(_pollCoroutine);
                _pollCoroutine = null;
            }
            base.OnExit();
        }

        private void Update()
        {
            if (Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false); 
                if (Input.GetKeyDown(KeyCode.Return)) CompleteStep(); 
            }
            else UpdateInactivity();
        }

        private IEnumerator PollRoomStateRoutine()
        {
            float emptyUserStartTime = -1f; // 유저 정보 EMPTY 타이머

            while (true)
            {
                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    yield return CoroutineData.GetWaitForSeconds(pollInterval);
                    continue;
                }

                string checkUrl = $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code={GameConstants.Module.Code.ToLower()}";
                string userUrl = $"{GameManager.Instance.ApiConfig.GetCurrentRoomUserUrl}?code={GameConstants.Module.Code.ToLower()}";

                bool isRoomEmpty = false;
                bool isNetworkError = false;

                // 1. 방 상태 확인
                using (UnityWebRequest stateReq = UnityWebRequest.Get(checkUrl))
                {
                    stateReq.timeout = 10; 
                    yield return stateReq.SendWebRequest();

                    if (stateReq.result == UnityWebRequest.Result.Success)
                    {
                        if (stateReq.downloadHandler.text.IndexOf(GameConstants.Api.StatusEmpty, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            isRoomEmpty = true;
                        }
                    }
                    else
                    {
                        isNetworkError = true;
                    }
                }
                
                if (isRoomEmpty)
                {
                    Debug.Log("[TutorialPage1] 방 상태가 EMPTY입니다. 타이틀로 복귀합니다.");
                    yield return CoroutineData.GetWaitForSeconds(1.0f);
                    if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
                    yield break;
                }

                // 2. 방이 USING(비어있지 않음)일 때 유저 상태 확인
                if (!isNetworkError)
                {
                    bool isUserEmpty = false;

                    using (UnityWebRequest userReq = UnityWebRequest.Get(userUrl))
                    {
                        userReq.timeout = 10; 
                        yield return userReq.SendWebRequest();

                        if (userReq.result == UnityWebRequest.Result.Success)
                        {
                            string rawText = userReq.downloadHandler.text;
                            if (rawText.IndexOf(GameConstants.Api.StatusEmpty, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                isUserEmpty = true;
                            }
                            else if (rawText.Contains(","))
                            {
                                // 유저 데이터가 정상적으로 있으면 EMPTY 타이머 초기화
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

                                        yield return apiManager.FetchDataAsync(uidLeft)
                                                               .Timeout(TimeSpan.FromSeconds(25))
                                                               .ToCoroutine(
                                                                    r => fetchSuccess = r, 
                                                                    ex => { fetchFaulted = true; }
                                                                );

                                        if (fetchFaulted || !fetchSuccess || !SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0)
                                        {
                                            yield return CoroutineData.GetWaitForSeconds(pollInterval);
                                            continue;
                                        }
                                    }
                                    CompleteStep(); 
                                    yield break; 
                                }
                            }
                        }
                        else
                        {
                            isNetworkError = true;
                        }
                    }

                    // 유저 상태가 EMPTY인 경우에만 15초 타이머 체크
                    if (isUserEmpty)
                    {
                        if (emptyUserStartTime < 0f)
                        {
                            emptyUserStartTime = Time.time;
                        }

                        if (Time.time - emptyUserStartTime >= 15f)
                        {
                            Debug.Log("[TutorialPage1] 15초 이상 유저 정보가 EMPTY 상태이므로 타이틀로 복귀합니다.");
                            if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
                            yield break;
                        }
                    }
                }

                if (isNetworkError)
                {
                    Debug.LogWarning($"[TutorialPage1] 네트워크 오류 발생. {pollInterval}초 후 재시도.");
                }

                // 항상 설정된 간격(3초) 대기
                yield return CoroutineData.GetWaitForSeconds(pollInterval);
            }
        }

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