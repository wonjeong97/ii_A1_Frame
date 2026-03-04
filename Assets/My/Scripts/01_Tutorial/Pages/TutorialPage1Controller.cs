using System;
using System.Collections;
using System.Text.RegularExpressions; 
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using My.Scripts.Core; // APIManager 참조용
using My.Scripts.Core.Pages;
using My.Scripts.Global;
using TMPro;
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
        
        private float pollInterval = 1.0f; // 폴링 간격 (1초)
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

                // 디버그용
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
            while (true)
            {
                // GameManager에서 API 설정이 로드될 때까지 대기
                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    yield return CoroutineData.GetWaitForSeconds(pollInterval);
                    continue;
                }

                // API Settings에서 URL 동적 조합
                string checkRoomStateUrl = $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code=a1";
                string getCurrentUserUrl = $"{GameManager.Instance.ApiConfig.GetCurrentRoomUserUrl}?code=a1";

                // =========================================================
                // 1. 방 상태가 EMPTY인지 확인 (타이틀로 복귀)
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
                            Debug.Log($"[TutorialPage1] RoomState 'EMPTY' 감지. 타이틀 화면으로 돌아갑니다.");
                            
                            if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
                            else SceneManager.LoadScene(GameConstants.Scene.Title);
                            
                            yield break; 
                        }
                    }
                }

                // =========================================================
                // 2. 현재 방의 유저 데이터가 있는지 확인 (파싱 및 다음 페이지 진행)
                // =========================================================
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
                            // 아직 유저 데이터가 없음. 무시하고 다음 폴링 대기
                        }
                        else if (rawText.Contains(","))
                        {
                            // 줄바꿈 기준으로 나누어서 진짜 데이터가 있는 줄만 추출
                            string cleanData = "";
                            string[] lines = rawText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            
                            foreach (string line in lines)
                            {
                                string trimmed = line.Trim();
                                // HTML 태그(<)로 시작하지 않고 콤마(,)가 있는 줄을 순수 데이터로 간주
                                if (trimmed.Contains(",") && !trimmed.StartsWith("<"))
                                {
                                    cleanData = trimmed;
                                    break;
                                }
                            }

                            if (!string.IsNullOrEmpty(cleanData))
                            {
                                // 콤마(,)를 기준으로 데이터 파싱 (uid_left, uid_right, idx_user)
                                string[] parts = cleanData.Split(',');
                                if (parts.Length >= 1)
                                {
                                    string uidLeft = parts[0].Trim();

                                    // GameManager에 UID 저장
                                    if (parts.Length >= 2 && GameManager.Instance)
                                    {
                                        GameManager.Instance.PlayerAUid = uidLeft;
                                        GameManager.Instance.PlayerBUid = parts[1].Trim();
                                    }

                                    // APIManager에 추출한 UID 전달
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