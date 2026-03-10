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

                using (UnityWebRequest stateReq = UnityWebRequest.Get(checkUrl))
                {
                    stateReq.timeout = 10; 
                    yield return stateReq.SendWebRequest();

                    if (stateReq.result == UnityWebRequest.Result.Success)
                    {
                        // # FIX: 여기서 즉시 리셋하지 않고 전체 사이클 성공 시 리셋하도록 변경
                        if (stateReq.downloadHandler.text.IndexOf(GameConstants.Api.StatusEmpty, StringComparison.OrdinalIgnoreCase) >= 0)
                            isRoomEmpty = true;
                    }
                    else isNetworkError = true;
                }

                if (!isNetworkError && !isRoomEmpty)
                {
                    bool isUserEmpty = false;
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
                                // 전체 요청 성공 시에만 폴링 주기 복구
                                _currentPollInterval = basePollInterval;
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

                                        // # FIX: 타임아웃을 25초로 확장
                                        yield return apiManager.FetchDataAsync(uidLeft)
                                                               .Timeout(TimeSpan.FromSeconds(25))
                                                               .ToCoroutine(
                                                                    r => fetchSuccess = r, 
                                                                    ex => { fetchFaulted = true; }
                                                                );

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
                        else isNetworkError = true;
                    }

                    if (isUserEmpty)
                    {
                        if (emptyUserStartTime < 0f) emptyUserStartTime = Time.time;
                        if (Time.time - emptyUserStartTime >= 15f) isRoomEmpty = true;
                    }
                }

                if (isNetworkError)
                {
                    _currentPollInterval = Mathf.Min(_currentPollInterval * 2f, maxPollInterval);
                }

                if (isRoomEmpty)
                {
                    if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
                    yield break;
                }

                yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);
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