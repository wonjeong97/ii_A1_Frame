using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Text; 
using Microsoft.Extensions.Logging; 
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Utils;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;
using ZLogger;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage1Data
    {
        public TextSetting descriptionText;
        public string warningMessage;
        public string resetMessage;
    }

    /// <summary>
    /// 튜토리얼 진입 시 서버 폴링을 통해 실제 유저 상태를 동기화하는 컨트롤러.
    /// UI 렌더링 타이밍을 OnEnter로 이관하여 초기화 시 텍스트가 누락되는 라이프사이클 버그를 완벽 요격했습니다.
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
        private bool _isFetchingData;

        // --- 의존성 주입 (DI) 변수 ---
        private SessionManager _sessionManager;

        [Inject]
        public void Construct(GameManager gameManager, SessionManager sessionManager, ILogger<TutorialPage1Controller> logger)
        {
            _gameManager = gameManager;
            _sessionManager = sessionManager;
            _logger = logger;
        }

        protected override void Awake()
        {
            base.Awake();

            fadeTime = 1.0f;

            if (!descriptionText)
            {
               if (_logger != null) _logger.ZLogWarning($"[TutorialPage1] descriptionText 컴포넌트가 누락됨.");
            }
            else
            {
                descriptionText.SetAlpha(0f); 
            }

            if (!apiManager)
            {
                if (_logger != null) _logger.ZLogWarning($"[TutorialPage1] apiManager 인스턴스가 할당되지 않음.");
            }
        }

        protected override void SetupData(TutorialPage1Data data)
        {
            pageData = data;
            SetupPopupMessage(data?.warningMessage ?? string.Empty, data?.resetMessage ?? string.Empty);
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

        public override void OnEnter()
        {
            base.OnEnter();
            ResetIdleState(true);
            
            // 타이틀 씬에서 누락되었을 수 있는 LED 강제 소등 처리
            if (_arduinoManager)
            {
                _arduinoManager.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOff);
                _arduinoManager.SendCommandToBoth(GameConstants.Hardware.CmdLedShotOff);
                _arduinoManager.SendCommandToLight(GameConstants.Hardware.CmdLightOff);
            }
    
            emptyUserStartTime = -1f;
            _isFetchingData = false; 

            if (descriptionText && pageData?.descriptionText != null)
            {
                if (!_uiManager)
                {
                    Debug.LogError($"[TutorialPage1] 치명적 오류: _uiManager가 주입되지 않았습니다! 스코프 배치를 확인하세요.");
                }
                else
                {
                    _uiManager.SetText(descriptionText.gameObject, pageData.descriptionText);
                }
            }

            if (descriptionText)
            {
                descriptionText.FadeAsync(0f, 1f, fadeTime, this.GetCancellationTokenOnDestroy()).Forget();
            }

            pollCts?.Cancel();
            pollCts?.Dispose();
            pollCts = new CancellationTokenSource();
            
            PollRoomStateAsync(pollCts.Token).Forget();
        }

        public override void OnExit()
        {
            pollCts?.Cancel();
            pollCts?.Dispose();
            pollCts = null;
            
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
                if (emptyUserStartTime < 0f && !_isFetchingData)
                {
                    UpdateInactivity();
                }
            }
        }

        private async UniTaskVoid PollRoomStateAsync(CancellationToken token)
        {
#if UNITY_EDITOR
            await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: token);
            if (apiManager) apiManager.FillDebugSession();
            CompleteStep();
            return;
#endif
            int intervalMs = Mathf.RoundToInt((pollInterval > 0 ? pollInterval : 3.0f) * 1000);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_gameManager && _gameManager.ApiConfig != null)
                    {
                        CacheUrls();
                        await ProcessRoomStateAsync(token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    _logger?.ZLogWarning($"[TutorialPage1] 폴링 루프 예외 발생 (재시도 대기): {e.Message}");
                }

                await UniTask.Delay(intervalMs, ignoreTimeScale: true, cancellationToken: token);
            }
        }

        private void CacheUrls()
        {
            if (string.IsNullOrEmpty(cachedCheckUrl) && _gameManager?.ApiConfig != null)
            {
                string code = GameConstants.Module.Code.ToLower();
                cachedCheckUrl = ZString.Format("{0}?code={1}", _gameManager.ApiConfig.CheckRoomStateUrl, code);
                cachedUserUrl = ZString.Format("{0}?code={1}", _gameManager.ApiConfig.GetCurrentRoomUserUrl, code);
            }
        }

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
                        _logger?.ZLogWarning($"[TutorialPage1] 방 상태 EMPTY, 1초 뒤 타이틀로 돌아감.");
                        await UniTask.Delay(1000, ignoreTimeScale: true, cancellationToken: token);
                        if (_gameManager) _gameManager.ReturnToTitle();
                        return;
                    }
                    
                    await ProcessUserStateAsync(token);
                }
            }
        }

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

        private void HandleEmptyUserTimeout()
        {
            if (emptyUserStartTime < 0f)
            {
                emptyUserStartTime = Time.unscaledTime;
            }

            if (Time.unscaledTime - emptyUserStartTime >= 15f)
            {   
                _logger?.ZLogWarning($"[TutorialPage1] 15초 경과, 유저 데이터 없음으로 타이틀로 돌아감.");
                if (_gameManager) _gameManager.ReturnToTitle();
            }
        }

        private async UniTask FetchAndApplyUserDataAsync(string rawText, CancellationToken token)
        {
            int commaIndex = rawText.IndexOf(',');
            if (commaIndex < 0) return;

            string uidLeft = rawText.Substring(0, commaIndex).Trim();
            
            if (_sessionManager)
            {
                _sessionManager.PlayerAUid = uidLeft;
                _sessionManager.PlayerBUid = rawText.Substring(commaIndex + 1).Trim();
            }

            if (!apiManager) return;

            _isFetchingData = true; 

            try
            {
                bool success = await apiManager.FetchDataAsync(uidLeft)
                    .AttachExternalCancellation(token)
                    .Timeout(TimeSpan.FromSeconds(25));

                if (success && _sessionManager && _sessionManager.CurrentUserId != 0)
                {
                    CompleteStep();
                }
            }
            catch (TimeoutException)
            {
                _logger?.ZLogWarning($"[TutorialPage1] 유저 데이터 페치 타임아웃.");
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _isFetchingData = false; 
            }
        }
    }
}