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
            
            if (descriptionText != null)
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

                // 디버그용 강제 스킵
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
            float emptyDuration = 0f; // EMPTY 상태 지속 시간 추적

            while (true)
            {
                // GameManager에서 API 설정이 로드될 때까지 대기
                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    yield return CoroutineData.GetWaitForSeconds(pollInterval);
                    continue;
                }

                string checkRoomStateUrl = $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code=a1";
                string getCurrentUserUrl = $"{GameManager.Instance.ApiConfig.GetCurrentRoomUserUrl}?code=a1";

                bool isEmptyThisPoll = false;

                // =========================================================
                // 1. 방 상태가 EMPTY인지 1차 확인
                // =========================================================
                using (UnityWebRequest stateReq = UnityWebRequest.Get(checkRoomStateUrl))
                {
                    stateReq.timeout = 10; 
                    
                    yield return stateReq.SendWebRequest();

                    if (stateReq.result != UnityWebRequest.Result.ConnectionError && 
                        stateReq.result != UnityWebRequest.Result.ProtocolError)
                    {
                        string responseText = stateReq.downloadHandler.text;
                        
                        if (!string.IsNullOrEmpty(responseText) && responseText.IndexOf("EMPTY", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            isEmptyThisPoll = true;
                        }
                    }
                }

                // =========================================================
                // 2. 방 상태가 EMPTY가 아니라면 유저 데이터 확인
                // =========================================================
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
                            
                            if (rawText.IndexOf("EMPTY", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                // 방 상태는 정상이지만 아직 유저 데이터가 배정되지 않음
                                isEmptyThisPoll = true;
                            }
                            else if (rawText.Contains(","))
                            {
                                // =========================================================
                                // 정상 데이터 수신 시퀀스 진행
                                // =========================================================
                                emptyDuration = 0f; // 타이머 초기화

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
                                            GameManager.Instance.PlayerAUid = uidLeft;
                                            GameManager.Instance.PlayerBUid = parts[1].Trim();
                                        }

                                        if (apiManager)
                                        {
                                            apiManager.FetchData(uidLeft);
                                            float timeoutAt = Time.time + 5f;
                                            while (GameManager.Instance &&
                                                   GameManager.Instance.CurrentUserId == 0 &&
                                                   Time.time < timeoutAt)
                                            {
                                                yield return null;
                                            }
                                        }
                                        else
                                        {
                                            Debug.LogWarning("[TutorialPage1] APIManager를 씬에서 찾을 수 없습니다.");
                                        }
                                    }
                                    CompleteStep(); 
                                    yield break; 
                                }
                            }
                        }
                    }
                }

                // =========================================================
                // 3. EMPTY 연속 지속 시간 검사 (15초 초과 시 초기화)
                // =========================================================
                if (isEmptyThisPoll)
                {
                    emptyDuration += pollInterval;
                    if (emptyDuration >= 15f)
                    {
                        Debug.LogWarning($"[TutorialPage1] 15초 연속 EMPTY 감지. 강제 초기화를 진행합니다.");
                        if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
                        else SceneManager.LoadScene(GameConstants.Scene.Title);
                        yield break;
                    }
                }
                else
                {
                    // 상태가 정상이거나 다른 예외(네트워크 등) 상황이면 타이머 누적 리셋
                    emptyDuration = 0f;
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