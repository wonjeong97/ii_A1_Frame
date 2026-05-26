using System;
using System.IO;
using Cysharp.Threading.Tasks;
using Cysharp.Text; 
using Microsoft.Extensions.Logging;
using My.Scripts.Core;
using ZLogger; 
using My.Scripts.Core.Data;
using My.Scripts.Hardware;
using My.Scripts.Timelapse;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using VContainer;
using Wonjeong.Core; 
using Wonjeong.Data; 
using Wonjeong.UI; 
using Wonjeong.Utils;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace My.Scripts.Global
{
    /// <summary>
    /// 게임의 전반적인 상태, 씬 전환, 전역 하드웨어 제어 및 앱 종료 시퀀스를 관리함.
    /// </summary>
    public class GameManager : GameManagerBase<GameManager>
    {
        public bool isDebugMode;

        private bool _isTransitioning;
        private float _fadeTime;
        private bool _isQuitting;
        private bool _isQuitSafe;
        private Coroutine _transitionRoutine;
        
        public ApiSettings ApiConfig { get; set; }

        [Header("Player Color Sprites")]
        public Sprite[] playerColorSprites;

        [Header("API Retry Settings")]
        [SerializeField] private int maxRetries;
        [SerializeField] private float retryDelay;

        // --- 자식 전용 의존성 주입 (DI) ---
        private ILogger<GameManager> _childLogger; 
        private SessionManager _sessionManager;
        private ArduinoManager _arduinoManager;
        private HueManager _hueManager;
        private FadeManager _fadeManager;

        [Inject]
        public void ConstructDependencies(
            ILogger<GameManager> childLogger,
            SessionManager sessionManager,
            ArduinoManager arduinoManager,
            HueManager hueManager,
            FadeManager fadeManager)
        {
            _childLogger = childLogger;
            _sessionManager = sessionManager;
            _arduinoManager = arduinoManager;
            _hueManager = hueManager;
            _fadeManager = fadeManager;
        }

        protected override void Awake()
        {
            base.Awake();
            Application.wantsToQuit += WantsToQuit;
            _fadeTime = 0.5f;
        }

        protected override void Start()
        {
            base.Start();
            Application.runInBackground = true;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy(); 
            Application.wantsToQuit -= WantsToQuit;
        }

        public Sprite GetColorSprite(ColorData color)
        {
            int index = (int)color;
            if (index >= 0 && playerColorSprites != null && playerColorSprites.Length > index)
            {
                Sprite targetSprite = playerColorSprites[index];
                if (targetSprite) return targetSprite;
            }
            return null;
        }
        
        protected override async UniTaskVoid LoadSettingsAsync()
        {
            // 1. 부모의 셋팅 템플릿 로드 결합
            string settingPath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.JsonSetting);
            settings = await JsonLoader.LoadAsync<Settings>(settingPath, this.GetCancellationTokenOnDestroy());
            
            if (settings == null)
            {
                _childLogger.ZLogError($"[GameManager] Settings file not found: {settingPath}");
                settings = new Settings();
            }
            else
            {
                _fadeTime = settings.fadeTime;
            }

            // 2. 자식 고유의 API 설정 로드
            string currentLang = _sessionManager ? _sessionManager.CurrentLanguage : "ko";
            string apiPath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.ApiSetting, currentLang);
            ApiConfig = JsonLoader.Load<ApiSettings>(apiPath);  

            if (ApiConfig == null)
            {
                _childLogger.ZLogWarning($"{apiPath}.json 설정이 누락됨.");
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                isDebugMode = !isDebugMode;
                _childLogger.ZLogInformation($"디버그 모드 {(isDebugMode ? "활성화" : "비활성화")} 됨");
            }

            if (isDebugMode && Input.GetKeyDown(KeyCode.Return))
            {
                SkipToNextSceneDebug();
            }
        }

        public void SkipToNextSceneDebug()
        {
            if (_isTransitioning) return;

            string currentScene = SceneManager.GetActiveScene().name;
            string nextScene = DetermineNextDebugScene(currentScene);

            if (string.IsNullOrEmpty(nextScene)) return;

            _childLogger.ZLogInformation($"디버그 즉시 스킵: {currentScene} -> {nextScene}");

            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }

            _isTransitioning = false;

            if (_fadeManager) _fadeManager.FadeInAsync(0f).Forget();

            SceneLoader.LoadAsync(nextScene).Forget();
        }

        private string DetermineNextDebugScene(string currentScene)
        {
            if (currentScene == GameConstants.Scene.Title) return GameConstants.Scene.Tutorial;
            if (currentScene == GameConstants.Scene.Tutorial) return GameConstants.Scene.PlayTutorial;
            if (currentScene == GameConstants.Scene.PlayTutorial) return ZString.Format("Play_{0}", GameConstants.Level.Q1);

            if (currentScene == GameConstants.Scene.Ending)
            {
                ReturnToTitle();
                return null;
            }

            if (currentScene.StartsWith("Play_Q"))
            {
                return GetNextQuestionScene(currentScene);
            }

            return null;
        }

        private string GetNextQuestionScene(string currentScene)
        {
            int qIdx = currentScene.IndexOf('Q') + 1;
            if (int.TryParse(currentScene.Substring(qIdx), out int currentQ))
            {
                return currentQ >= 15 ? GameConstants.Scene.Ending : ZString.Format("Play_Q{0}", currentQ + 1);
            }

            return null;
        }

        public void ChangeScene(string sceneName)
        {
            if (_isTransitioning) return;

            ChangeSceneAsync(sceneName).Forget();
        }

        private async UniTaskVoid ChangeSceneAsync(string sceneName)
        {
            _isTransitioning = true;

            try
            {
                if (!_fadeManager)
                {
                    await SceneLoader.LoadAsync(sceneName);
                    return;
                }

                await _fadeManager.FadeOutAsync(_fadeTime);
                await SceneLoader.LoadAsync(sceneName);
                _fadeManager.FadeInAsync(_fadeTime).Forget();
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private async UniTaskVoid QuitAsync()
        {
            await TurnOffAllHardwareOutputsAsync();

#if !UNITY_EDITOR
            if (_sessionManager && _sessionManager.CurrentUserId != 0 && ApiConfig != null)
            {
                int uid = _sessionManager.CurrentUserId;
                string moduleCode = GameConstants.Module.Code.ToLower();

                string resetUrl = ZString.Format("{0}?idx_user={1}&code={2}", ApiConfig.ResetStartUrl, uid, moduleCode);
                string exitUrl = ZString.Format("{0}?code={1}&idx_user={2}", ApiConfig.ExitRoomUrl, moduleCode, uid);

                await UniTask.WhenAll(SendGetRequestAsync(resetUrl), SendGetRequestAsync(exitUrl));
            }
#endif
            await ClearSourceFoldersAsync();

            _isQuitSafe = true;

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void ReturnToTitle()
        {
            if (_isTransitioning) return;

            ReturnToTitleAsync().Forget();
        }

        private async UniTaskVoid ReturnToTitleAsync()
        {
            _isTransitioning = true;
            _childLogger.ZLogInformation($"타이틀로 돌아감");

            SendResetStartAPI();
            SendExitRoomAPI();

            await TurnOffAllHardwareOutputsAsync();

            if (_sessionManager) _sessionManager.ClearSession();

            _isTransitioning = false;
            ChangeScene(GameConstants.Scene.Title);
        }

        #region Hardware Control Helper

        private async UniTask TurnOffAllHardwareOutputsAsync()
        {
            if (_arduinoManager)
            {
                _arduinoManager.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOff);
                _arduinoManager.SendCommandToBoth(GameConstants.Hardware.CmdLedShotOff);
                _arduinoManager.SendCommandToLight(GameConstants.Hardware.CmdLightOff);
            }

            if (_hueManager)
            {
                try
                {
                    bool isTimeout = await UniTask.WhenAll(
                        _hueManager.SetLightStateAsync(1, false),
                        _hueManager.SetLightStateAsync(2, false)
                    ).TimeoutWithoutException(TimeSpan.FromSeconds(2));

                    if (isTimeout)
                    {
                        _childLogger.ZLogWarning($"휴 조명 소등 대기 타임아웃.");
                    }
                }
                catch (Exception ex)
                {
                    _childLogger.ZLogWarning($"휴 조명 소등 중 예외: {ex.Message}");
                }
            }
        }

        #endregion

        #region API 호출 로직

        private async UniTask SendGetRequestAsync(string url)
        {
#if UNITY_EDITOR
            return;
#endif
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 10;
                    await req.SendWebRequest().ToUniTask();

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        if (attempt > 0) _childLogger.ZLogInformation($"[GameManager] API 요청 성공: {url} (시도: {attempt + 1}회)");
                        return;
                    }

                    // 상세 에러 로그 기록 (응답 코드 포함)
                    _childLogger.ZLogWarning($"[GameManager] API 요청 실패 (시도 {attempt + 1}/{maxRetries})");
                    _childLogger.ZLogWarning($"-> URL: {url}");
                    _childLogger.ZLogWarning($"-> 결과: {req.result}, 응답 코드: {req.responseCode}, 에러: {req.error}");

                    if (attempt < maxRetries - 1)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(retryDelay));
                    }
                    else
                    {
                        _childLogger.ZLogError($"[GameManager] API 통신 최종 실패: {url}");
                    }
                }
            }
        }

        public void SendResetStartAPI()
        {
            if (!_sessionManager || _sessionManager.CurrentUserId == 0 || ApiConfig == null) return;

            string url = ZString.Format("{0}?idx_user={1}&code={2}", ApiConfig.ResetStartUrl, _sessionManager.CurrentUserId, GameConstants.Module.Code.ToLower());
            SendGetRequestAsync(url).Forget();
        }

        public void SendExitRoomAPI()
        {
            if (!_sessionManager || _sessionManager.CurrentUserId == 0 || ApiConfig == null) return;

            string url = ZString.Format("{0}?code={1}&idx_user={2}", ApiConfig.ExitRoomUrl, GameConstants.Module.Code.ToLower(), _sessionManager.CurrentUserId);
            SendGetRequestAsync(url).Forget();
        }

        public void SendTimeUpdateAPI()
        {
            if (!_sessionManager || _sessionManager.CurrentUserId == 0 || ApiConfig == null) return;

            string url = ZString.Format("{0}?idx_user={1}&option=end&code={2}", ApiConfig.UpdateTimeUrl, _sessionManager.CurrentUserId, GameConstants.Module.Code.ToLower());
            SendGetRequestAsync(url).Forget();
        }

        public void SendValueUpdateAPI(int qNo, string side, int value)
        {
            if (!_sessionManager || _sessionManager.CurrentUserId == 0 || ApiConfig == null) return;

            string url = ZString.Format("{0}?idx_user={1}&q_no={2}&side={3}&code={4}&value={5}", 
                ApiConfig.UpdateValueUrl, _sessionManager.CurrentUserId, qNo, side, GameConstants.Module.Code.ToLower(), value);
            SendGetRequestAsync(url).Forget();
        }

        public void SendPieceUpdateAPI(int value)
        {
            if (value < 0 || !_sessionManager || _sessionManager.CurrentUserId == 0 || ApiConfig == null) return;

            string url = ZString.Format("{0}?idx_user={1}&code={2}&value={3}", 
                ApiConfig.UpdatePieceUrl, _sessionManager.CurrentUserId, GameConstants.Module.Code.ToLower(), value);
            SendGetRequestAsync(url).Forget();
        }

        #endregion

        #region 프로그램 강제 종료 시 예외 처리

        private bool WantsToQuit()
        {
            if (_isQuitSafe) return true;

            if (!_isQuitting)
            {
                _isQuitting = true;
                QuitAsync().Forget();
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnApplicationQuit()
        {
            if (_isQuitSafe) return;

            TurnOffAllHardwareOutputsAsync().Forget();
            ClearSourceFoldersAsync().Forget();
        }
#endif

        private async UniTask ClearSourceFoldersAsync()
        {
            string dataPath = Application.dataPath;
            string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");

            try
            {
                await UniTask.RunOnThreadPool(() =>
                {
                    DirectoryInfo parentDir = Directory.GetParent(dataPath);
                    string rootPath = parentDir != null ? parentDir.FullName : dataPath;

                    string timelapseSource = Path.Combine(rootPath, "Timelapse", "Timelapse_Source", dateFolder);
                    string realtimeSource = Path.Combine(rootPath, "Timelapse", "Realtime_Source", dateFolder);

                    DeleteFilesInDirectory(timelapseSource, "타임랩스");
                    DeleteFilesInDirectory(realtimeSource, "리얼타임");
                });
            }
            catch (Exception e)
            {
                _childLogger.ZLogWarning($"비동기 소스 폴더 접근 오류: {e.Message}");
            }
        }

        private void DeleteFilesInDirectory(string directoryPath, string logPrefix)
        {
            if (!Directory.Exists(directoryPath)) return;

            string[] files = Directory.GetFiles(directoryPath);
            foreach (string file in files)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    _childLogger.ZLogWarning($"{logPrefix} 소스 파일 삭제 실패 ({file}): {ex.Message}");
                }
            }
        }

        #endregion
    }
}