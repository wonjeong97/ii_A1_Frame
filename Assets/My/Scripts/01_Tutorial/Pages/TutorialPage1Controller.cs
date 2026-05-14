using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Utils;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary>
    /// 튜토리얼 1페이지 텍스트 데이터 구조체.
    /// </summary>
    [Serializable]
    public class TutorialPage1Data
    {
        public TextSetting descriptionText;
        public string warningMessage;
        public string resetMessage;
    }

    /// <summary>
    /// 튜토리얼 진입 시 서버 폴링을 통해 실제 유저 상태를 동기화하는 컨트롤러.
    /// UniTask를 사용하여 가비지 발생을 억제하고 비동기 루프를 안정적으로 관리함.
    /// </summary>
    public class TutorialPage1Controller : PopupGamePage<TutorialPage1Data>
    {
        [Header("Page 1 UI")]
        [SerializeField] private Text descriptionText;
        [Header("API Manager")]
        [SerializeField] private APIManager apiManager;
        [Header("Polling Settings")]
        [SerializeField] private float pollInterval; 
        
        private float fadeTime;
        private CancellationTokenSource pollCts;
        private TutorialPage1Data pageData;
        
        private string cachedCheckUrl;
        private string cachedUserUrl;
        private float emptyUserStartTime;

        /// <summary>
        /// 초기화 단계에서 페이드인 연출을 위해 텍스트 투명도를 선제적으로 낮춤.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            fadeTime = 1.0f;

            if (!descriptionText)
            {
                Debug.LogWarning("TutorialPage1: descriptionText 컴포넌트가 누락됨.");
            }
            else
            {
                Color c = descriptionText.color;
                c.a = 0f;
                descriptionText.color = c;
            }

            if (!apiManager)
            {
                Debug.LogWarning("TutorialPage1: apiManager 인스턴스가 할당되지 않음.");
            }
        }

        /// <summary>
        /// 전달받은 데이터를 기반으로 UI 텍스트 컴포넌트의 값을 갱신함.
        /// </summary>
        protected override void SetupData(TutorialPage1Data data)
        {
            pageData = data;
            if (descriptionText && data.descriptionText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            }
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        public override object ExtractCurrentData()
        {
            return new TutorialPage1Data
            {
                descriptionText = TutorialPageUtils.BuildTextSetting(descriptionText, pageData?.descriptionText),
                warningMessage  = pageData?.warningMessage ?? string.Empty,
                resetMessage    = pageData?.resetMessage   ?? string.Empty,
            };
        }

        /// <summary>
        /// 페이지 활성화 시 무인 타이머를 초기화하고 네트워크 상태 폴링을 시작함.
        /// 기존 작업이 있다면 취소하고 새로운 토큰을 발행하여 중복 실행을 방지함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            ResetIdleState(true);
            
            emptyUserStartTime = -1f;

            if (descriptionText)
            {
                UIFadeUtility.FadeGraphicAsync(descriptionText, 0f, 1f, fadeTime, this.GetCancellationTokenOnDestroy()).Forget();
            }

            pollCts?.Cancel();
            pollCts?.Dispose();
            pollCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            
            PollRoomStateAsync(pollCts.Token).Forget();
        }

        /// <summary>
        /// 페이지 비활성화 시 비동기 루프를 중단하고 토큰 리소스를 해제함.
        /// </summary>
        public override void OnExit()
        {
            pollCts?.Cancel();
            pollCts?.Dispose();
            pollCts = null;
            
            base.OnExit();
        }

        /// <summary>
        /// 입력 장치 이벤트를 감지하여 대기 상태 해제 및 수동 스킵을 제어함.
        /// </summary>
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
        /// 서버 상태를 주기적으로 확인하며 유저 세션을 동기화하는 비동기 루프.
        /// 에디터 환경에서는 폴링을 생략하여 불필요한 로그 발생을 억제함.
        /// </summary>
        private async UniTaskVoid PollRoomStateAsync(CancellationToken token)
        {
#if UNITY_EDITOR
            return;
#endif
            while (!token.IsCancellationRequested)
            {
                if (GameManager.Instance && GameManager.Instance.ApiConfig != null)
                {
                    CacheUrls();
                    await ProcessRoomStateAsync(token);
                }

                await UniTask.Delay(TimeSpan.FromSeconds(pollInterval > 0 ? pollInterval : 3.0f), cancellationToken: token);
            }
        }

        /// <summary>
        /// URL 문자열 조합 시 발생하는 가비지를 방지하기 위해 엔드포인트를 캐싱함.
        /// </summary>
        private void CacheUrls()
        {
            if (string.IsNullOrEmpty(cachedCheckUrl) && GameManager.Instance.ApiConfig != null)
            {
                string code = GameConstants.Module.Code.ToLower();
                cachedCheckUrl = $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code={code}";
                cachedUserUrl = $"{GameManager.Instance.ApiConfig.GetCurrentRoomUserUrl}?code={code}";
            }
        }

        /// <summary>
        /// 현재 방의 점유 상태를 확인하고, 비어있을 시 타이틀로 강제 복귀시킴.
        /// </summary>
        private async UniTask ProcessRoomStateAsync(CancellationToken token)
        {
            using (UnityWebRequest stateReq = UnityWebRequest.Get(cachedCheckUrl))
            {
                stateReq.timeout = 10;
                await stateReq.SendWebRequest().ToUniTask(cancellationToken: token);

                if (stateReq.result == UnityWebRequest.Result.Success)
                {
                    if (stateReq.downloadHandler.text.IndexOf(GameConstants.Api.StatusEmpty, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(1.0), cancellationToken: token);
                        if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
                        return;
                    }
                    
                    // 방이 비어있지 않다면 유저 상세 상태 확인 단계로 진입
                    await ProcessUserStateAsync(token);
                }
            }
        }

        /// <summary>
        /// 방에 접속한 특정 유저의 유효성을 검사하고 데이터 적용 시퀀스를 실행함.
        /// </summary>
        private async UniTask ProcessUserStateAsync(CancellationToken token)
        {
            using (UnityWebRequest userReq = UnityWebRequest.Get(cachedUserUrl))
            {
                userReq.timeout = 10;
                await userReq.SendWebRequest().ToUniTask(cancellationToken: token);

                if (userReq.result != UnityWebRequest.Result.Success) return;

                string rawText = userReq.downloadHandler.text;
                if (rawText.IndexOf(GameConstants.Api.StatusEmpty, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    HandleEmptyUserTimeout();
                }
                else if (rawText.Contains(","))
                {
                    emptyUserStartTime = -1f;
                    await FetchAndApplyUserDataAsync(rawText, token);
                }
            }
        }

        /// <summary>
        /// 일시적인 통신 지연을 고려하여 15초 이상 유저 정보가 없을 경우에만 리셋을 수행함.
        /// </summary>
        private void HandleEmptyUserTimeout()
        {
            if (emptyUserStartTime < 0f)
            {
                emptyUserStartTime = Time.time;
            }

            if (Time.time - emptyUserStartTime >= 15f)
            {
                if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
            }
        }

        /// <summary>
        /// 수신된 UID를 바탕으로 APIManager를 호출하여 유저 데이터를 세션에 동기화함.
        /// </summary>
        private async UniTask FetchAndApplyUserDataAsync(string rawText, CancellationToken token)
        {
            string[] parts = rawText.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 1) return;

            string uidLeft = parts[0].Trim();

            if (parts.Length >= 2 && SessionManager.Instance)
            {
                SessionManager.Instance.PlayerAUid = uidLeft;
                SessionManager.Instance.PlayerBUid = parts[1].Trim();
            }

            if (!apiManager) return;

            // API 조회 무한 대기를 방지하기 위해 타임아웃을 적용한 비동기 호출 수행
            try
            {
                bool success = await apiManager.FetchDataAsync(uidLeft).Timeout(TimeSpan.FromSeconds(25));
                
                if (success && SessionManager.Instance && SessionManager.Instance.CurrentUserId != 0)
                {
                    CompleteStep();
                }
            }
            catch (TimeoutException)
            {
                Debug.LogWarning("TutorialPage1: 유저 데이터 페치 타임아웃.");
            }
        }
    }
}