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
        
        private readonly float pollInterval = 1.0f; // 폴링 간격 (1초)
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
            if (descriptionText) 
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            }
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

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

        private IEnumerator PollRoomStateRoutine()
        {
            float emptyStartTime = -1f; 

            while (true)
            {
                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    yield return CoroutineData.GetWaitForSeconds(pollInterval);
                    continue;
                }

                // 상수 사용
                string checkRoomStateUrl = $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code={GameConstants.Module.Code.ToLower()}";
                string getCurrentUserUrl = $"{GameManager.Instance.ApiConfig.GetCurrentRoomUserUrl}?code={GameConstants.Module.Code.ToLower()}";

                bool isEmptyThisPoll = false;

                using (UnityWebRequest stateReq = UnityWebRequest.Get(checkRoomStateUrl))
                {
                    stateReq.timeout = 10; 
                    
                    yield return stateReq.SendWebRequest();

                    if (stateReq.result != UnityWebRequest.Result.ConnectionError && 
                        stateReq.result != UnityWebRequest.Result.ProtocolError)
                    {
                        string responseText = stateReq.downloadHandler.text;
                        
                        if (!string.IsNullOrEmpty(responseText) && responseText.IndexOf(GameConstants.Api.StatusEmpty, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            isEmptyThisPoll = true;
                        }
                    }
                }

                if (!isEmptyThisPoll)
                {
                    using (UnityWebRequest userReq = UnityWebRequest.Get(getCurrentUserUrl))
                    {
                        userReq.timeout = 10; 
                        
                        yield return userReq.SendWebRequest();

                        if (userReq.result != UnityWebRequest.Result.ConnectionError && 
                            userReq.result != UnityWebRequest.Result.ProtocolError)
                        {
                            string rawText = userReq.downloadHandler.text;
                            
                            if (rawText.IndexOf(GameConstants.Api.StatusEmpty, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                isEmptyThisPoll = true;
                            }
                            else if (rawText.Contains(","))
                            {
                                emptyStartTime = -1f; 

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

                                        if (parts.Length >= 2 && GameManager.Instance)
                                        {
                                            SessionManager.Instance.PlayerAUid = uidLeft;
                                            SessionManager.Instance.PlayerBUid = parts[1].Trim();
                                        }

                                        if (apiManager)
                                        {   
                                            if (SessionManager.Instance) SessionManager.Instance.CurrentUserId = 0;
                                            apiManager.FetchData(uidLeft);
                                            float timeoutAt = Time.time + 11f;
                                            while (GameManager.Instance &&
                                                   SessionManager.Instance.CurrentUserId == 0 &&
                                                   Time.time < timeoutAt)
                                            {
                                                yield return null;
                                            }

                                            if (!GameManager.Instance || SessionManager.Instance.CurrentUserId == 0)
                                            {
                                                Debug.LogWarning("[TutorialPage1] CurrentUserId 확정 실패. 다음 폴링에서 재시도합니다.");
                                                yield return CoroutineData.GetWaitForSeconds(pollInterval);
                                                continue;
                                            }
                                        }
                                        else
                                        {
                                            Debug.LogWarning("[TutorialPage1] APIManager를 찾을 수 없습니다.");
                                            yield return CoroutineData.GetWaitForSeconds(pollInterval);
                                            continue;
                                        }
                                    }
                                    CompleteStep(); 
                                    yield break; 
                                }
                            }
                        }
                    }
                }

                if (isEmptyThisPoll)
                {
                    if (emptyStartTime < 0f) emptyStartTime = Time.time;
                    if (Time.time - emptyStartTime >= 15f)
                    {
                        Debug.LogWarning($"[TutorialPage1] 15초 연속 EMPTY 감지. 강제 초기화를 진행합니다.");
                        if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
                        else SceneManager.LoadScene(GameConstants.Scene.Title);
                        yield break;
                    }
                }
                else
                {
                    emptyStartTime = -1f;
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