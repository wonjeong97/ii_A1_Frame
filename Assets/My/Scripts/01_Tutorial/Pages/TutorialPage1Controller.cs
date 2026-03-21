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
        [SerializeField] private float pollInterval = 3.0f;

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
            // Enter 키를 누르면 다음으로 넘어갈 수 있습니다.
            if (Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false); 
                if (Input.GetKeyDown(KeyCode.Return)) CompleteStep(); 
            }
            else UpdateInactivity();
        }

        private IEnumerator PollRoomStateRoutine()
        {
// 에디터에서 실행 중일 때는 실제 유저 데이터를 긁어와서 방해하지 않도록 차단합니다.
#if UNITY_EDITOR
            Debug.Log("<color=orange>[TutorialPage1] 에디터 모드 방지: 실제 유저 데이터 추적을 차단했습니다. 더미 데이터로 진행되며, Enter 키를 누르면 다음으로 넘어갑니다.</color>");
            yield break;
#endif

            float emptyUserStartTime = -1f;

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