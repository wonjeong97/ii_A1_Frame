using System;
using System.Collections;
using System.IO;
using Cysharp.Threading.Tasks; 
using My.Scripts.Core;
using My.Scripts.Core.Data;
using My.Scripts.Hardware;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Wonjeong.Core; 
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Global
{
    /// <summary>
    /// 게임의 전반적인 상태, 씬 전환, 전역 하드웨어 제어 및 앱 종료 시퀀스를 관리함.
    /// GameManagerBase를 상속받아 공통 시스템(Reporter, Cursor, Inspector 등) 기능을 위임함.
    /// </summary>
    public class GameManager : GameManagerBase<GameManager>
    {
        public bool isDebugMode;
        
        private bool _isTransitioning;
        private float _fadeTime = 0.5f;
        private bool _isQuitting;
        private bool _isQuitSafe;
        private Coroutine _transitionRoutine;
        public ApiSettings ApiConfig { get; set; }

        [Header("Player Color Sprites")]
        public Sprite[] playerColorSprites;
        
        [Header("API Retry Settings")]
        [SerializeField] private int maxRetries = 10;
        [SerializeField] private float retryDelay = 1.0f;

        /// <summary>
        /// 부모 객체의 싱글톤 및 로깅 설정을 상속받고 세션 매니저를 구성함.
        /// </summary>
        protected override void Awake()
        {   
            base.Awake();

            if (Instance != this) return;

            if (!SessionManager.Instance)
            {
                GameObject sessionObj = new GameObject("SessionManager");
                sessionObj.AddComponent<SessionManager>();
            }

            Application.wantsToQuit += WantsToQuit;
        }

        /// <summary>
        /// 게임 초기 설정 로드. (Reporter 및 커서 숨김 기능은 부모 Start에서 처리됨)
        /// </summary>
        protected override void Start()
        {
            base.Start();

            Application.runInBackground = true;
        }

        /// <summary>
        /// 인스턴스 파괴 시 등록된 앱 종료 이벤트를 해제함.
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Application.wantsToQuit -= WantsToQuit;
            }
        }

        /// <summary>
        /// 지정된 색상 데이터에 매핑되는 UI 스프라이트를 반환함.
        /// </summary>
        public Sprite GetColorSprite(ColorData color)
        {
            int index = (int)color;
            if (index >= 0 && playerColorSprites != null && index < playerColorSprites.Length)
            {
                return playerColorSprites[index];
            }
            return null;
        }

        /// <summary>
        /// 프로젝트 전용 경로 상수를 사용하여 설정을 로드하고, 자식 클래스 전용 필드를 초기화함.
        /// </summary>
        protected override void LoadSettings()
        {
            settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);
    
            if (settings == null)
            {
                Debug.LogWarning($"{GameConstants.Path.JsonSetting} 설정 파일 로드 실패. 기본값으로 대체함.");
                settings = new Settings();
            }
    
            _fadeTime = settings.fadeTime;

            string apiPath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.ApiSetting);
            ApiConfig = JsonLoader.Load<ApiSettings>(apiPath);

            if (ApiConfig == null)
            {
                Debug.LogWarning($"{apiPath}.json 설정이 누락됨.");
            }
        }

        /// <summary>
        /// 부모의 공통 키 입력 처리 외에, 자식 고유의 디버그 모드 전환을 처리함.
        /// </summary>
        protected override void Update()
        {
            base.Update(); // 부모의 Update 실행 (D키 Reporter, M키 커서 토글 등)

            if (Input.GetKeyDown(KeyCode.F))
            {
                isDebugMode = !isDebugMode;
                Debug.Log($"디버그 모드 {(isDebugMode ? "활성화" : "비활성화")} 됨");
            }

            if (isDebugMode && (Input.GetKeyDown(KeyCode.Return))) SkipToNextSceneDebug();
        }

        /// <summary>
        /// 디버그 목적의 빠른 씬 이동을 위해 현재 씬 이름을 파싱하여 강제 전환함.
        /// </summary>
        public void SkipToNextSceneDebug()
        {
            if (_isTransitioning) return;

            string currentScene = SceneManager.GetActiveScene().name;
            string nextScene = "";

            if (currentScene == GameConstants.Scene.Title) nextScene = GameConstants.Scene.Tutorial;
            else if (currentScene == GameConstants.Scene.Tutorial) nextScene = GameConstants.Scene.PlayTutorial;
            else if (currentScene == GameConstants.Scene.PlayTutorial) nextScene = "Play_Q1";
            else if (currentScene.StartsWith("Play_Q"))
            {
                int qIdx = currentScene.IndexOf('Q') + 1;
                if (int.TryParse(currentScene.Substring(qIdx), out int currentQ))
                {
                    if (currentQ >= 15) nextScene = GameConstants.Scene.Ending;
                    else nextScene = $"Play_Q{currentQ + 1}";
                }
            }
            else if (currentScene == GameConstants.Scene.Ending)
            {
                ReturnToTitle(); 
                return;
            }

            if (!string.IsNullOrEmpty(nextScene))
            {
                Debug.Log($"디버그 즉시 스킵: {currentScene} -> {nextScene}");
                
                if (_transitionRoutine != null)
                {
                    StopCoroutine(_transitionRoutine);
                    _transitionRoutine = null;
                }
                
                _isTransitioning = false; 

                if (FadeManager.Instance) FadeManager.Instance.FadeIn(0f);
                
                SceneLoader.LoadAsync(nextScene).Forget();
            }
        }

        /// <summary>
        /// 페이드 아웃 연출을 동반하여 지정된 씬으로 이동함.
        /// </summary>
        public void ChangeScene(string sceneName)
        {
            if (_isTransitioning) return;

            _isTransitioning = true;
            Debug.Log($"Scene Transition Requested: {sceneName}");
            _transitionRoutine = StartCoroutine(ChangeSceneRoutine(sceneName));
        }

        private IEnumerator ChangeSceneRoutine(string sceneName)
        {
            if (!FadeManager.Instance)
            {
                yield return SceneLoader.LoadAsync(sceneName).ToCoroutine();
                _isTransitioning = false;
                yield break;
            }

            bool fadeDone = false;
            FadeManager.Instance.FadeOut(_fadeTime, () => { fadeDone = true; });
            while (!fadeDone) yield return null;

            yield return SceneLoader.LoadAsync(sceneName).ToCoroutine();

            FadeManager.Instance.FadeIn(_fadeTime);
            _isTransitioning = false;
        }

        /// <summary>
        /// 현재 세션을 초기화하고 타이틀 씬으로 복귀함.
        /// </summary>
        public void ReturnToTitle()
        {
            if (_isTransitioning) return;
            ReturnToTitleAsync().Forget();
        }

        private async UniTaskVoid ReturnToTitleAsync()
        {
            _isTransitioning = true;
            Debug.Log("타이틀로 돌아감");

            SendResetStartAPI();
            SendExitRoomAPI();

            await TurnOffAllHardwareOutputsAsync();

            if (SessionManager.Instance) SessionManager.Instance.ClearSession();

            _isTransitioning = false; 
            ChangeScene(GameConstants.Scene.Title);
        }

        #region Hardware Control Helper

        /// <summary>
        /// 비정상 종료 시 하드웨어 부하를 막기 위해 아두이노 및 조명 장치를 소등함.
        /// </summary>
        private async UniTask TurnOffAllHardwareOutputsAsync()
        {
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOff);
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedShotOff);
                ArduinoManager.Instance.SendCommandToLight(GameConstants.Hardware.CmdLightOff);
            }

            if (HueManager.Instance)
            {
                try
                {
                    await UniTask.WhenAll(
                        HueManager.Instance.SetLightStateAsync(1, false),
                        HueManager.Instance.SetLightStateAsync(2, false)
                    ).Timeout(TimeSpan.FromSeconds(2));
                }
                catch (TimeoutException)
                {
                    Debug.LogWarning("휴 조명 소등 대기 타임아웃.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"휴 조명 소등 중 예외: {ex.Message}");
                }
            }
        }

        #endregion

        #region API 호출 로직

        /// <summary>
        /// GET 방식의 API 요청을 수행하고 실패 시 설정된 횟수만큼 재시도함.
        /// </summary>
        private IEnumerator SendGetRequestRoutine(string url)
        {
#if UNITY_EDITOR
            yield break;
#endif

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 10; 
                    yield return req.SendWebRequest();

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        yield break;
                    }

                    if (attempt < maxRetries - 1)
                    {
                        Debug.LogWarning($"API 전송 실패 ({attempt + 1}/{maxRetries}): {req.error}. {retryDelay}초 후 재시도.");
                        yield return CoroutineData.GetWaitForSeconds(retryDelay);
                    }
                    else
                    {
                        Debug.LogError($"API 전송 최종 실패 (URL: {url}) - {req.error}");
                    }
                }
            }
        }

        public void SendResetStartAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.ResetStartUrl}?idx_user={SessionManager.Instance.CurrentUserId}&code={GameConstants.Module.Code.ToLower()}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        public void SendExitRoomAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.ExitRoomUrl}?code={GameConstants.Module.Code.ToLower()}&idx_user={SessionManager.Instance.CurrentUserId}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        public void SendTimeUpdateAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.UpdateTimeUrl}?idx_user={SessionManager.Instance.CurrentUserId}&option=end&code={GameConstants.Module.Code.ToLower()}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        public void SendValueUpdateAPI(int qNo, string side, int value)
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.UpdateValueUrl}?idx_user={SessionManager.Instance.CurrentUserId}&q_no={qNo}&side={side}&code={GameConstants.Module.Code.ToLower()}&value={value}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        public void SendPieceUpdateAPI(int value)
        {
            if (value < 0 || !SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 || ApiConfig == null) return;
            string url = $"{ApiConfig.UpdatePieceUrl}?idx_user={SessionManager.Instance.CurrentUserId}&code={GameConstants.Module.Code.ToLower()}&value={value}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        #endregion

        #region 프로그램 강제 종료 시 예외 처리

        /// <summary>
        /// 앱 강제 종료 이벤트를 인터셉트하여 비동기 정리 작업을 먼저 수행하도록 함.
        /// </summary>
        private bool WantsToQuit()
        {
            if (_isQuitSafe) return true;

            if (!_isQuitting)
            {
                _isQuitting = true;
                StartCoroutine(QuitRoutine());
            }

            return false;
        }

        /// <summary>
        /// 앱 종료 전 유저 세션을 닫고 하드웨어 장치를 초기화함.
        /// </summary>
        private IEnumerator QuitRoutine()
        {
            yield return TurnOffAllHardwareOutputsAsync().ToCoroutine();

#if !UNITY_EDITOR
            if (SessionManager.Instance && SessionManager.Instance.CurrentUserId != 0 && ApiConfig != null)
            {
                int uid = SessionManager.Instance.CurrentUserId;

                string resetUrl = $"{ApiConfig.ResetStartUrl}?idx_user={uid}&code={GameConstants.Module.Code.ToLower()}";
                using (UnityWebRequest req = UnityWebRequest.Get(resetUrl))
                {
                    req.timeout = 2; 
                    yield return req.SendWebRequest();
                }

                string exitUrl = $"{ApiConfig.ExitRoomUrl}?code={GameConstants.Module.Code.ToLower()}&idx_user={uid}";
                using (UnityWebRequest req = UnityWebRequest.Get(exitUrl))
                {
                    req.timeout = 2;
                    yield return req.SendWebRequest();
                }
            }
#endif

            yield return ClearSourceFoldersAsync().ToCoroutine();

            _isQuitSafe = true;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

#if UNITY_EDITOR
        private void OnApplicationQuit()
        {
            if (_isQuitSafe) return; 

            TurnOffAllHardwareOutputsAsync().Forget();
            ClearSourceFoldersAsync().Forget();
        }
#endif

        /// <summary>
        /// 타임랩스 및 리얼타임 데이터 저장을 위해 생성했던 당일 임시 파일을 일괄 삭제함.
        /// </summary>
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

                    if (Directory.Exists(timelapseSource))
                    {
                        string[] tFiles = Directory.GetFiles(timelapseSource);
                        foreach (string file in tFiles)
                        {
                            try { File.Delete(file); }
                            catch (Exception ex) { Debug.LogWarning($"타임랩스 소스 파일 삭제 실패 ({file}): {ex.Message}"); }
                        }
                    }

                    if (Directory.Exists(realtimeSource))
                    {
                        string[] rFiles = Directory.GetFiles(realtimeSource);
                        foreach (string file in rFiles)
                        {
                            try { File.Delete(file); }
                            catch (Exception ex) { Debug.LogWarning($"리얼타임 소스 파일 삭제 실패 ({file}): {ex.Message}"); }
                        }
                    }
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"비동기 소스 폴더 접근 오류: {e.Message}");
            }
        }

        #endregion
    }
}