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
using Wonjeong.Data;
using Wonjeong.Reporter;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Global
{
    /// <summary>
    /// 게임의 전반적인 상태, 씬 전환, 전역 하드웨어 제어 및 앱 종료 시퀀스를 관리함.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        [SerializeField] private Reporter reporter;

        public bool isDebugMode = false;
        
        private bool _isTransitioning;
        private float _fadeTime = 0.5f;
        private bool _isQuitting;
        private bool _isQuitSafe;
        private Coroutine _transitionRoutine;

        public int firstTaggedPlayer = 0;
        public ApiSettings ApiConfig { get; set; }

        [Header("Player Color Sprites")]
        public Sprite[] playerColorSprites;
        
        [Header("API Retry Settings")]
        [SerializeField] private int maxRetries = 10;
        [SerializeField] private float retryDelay = 1.0f;

        /// <summary>
        /// 싱글톤 인스턴스를 초기화하고 전역 로깅 및 세션 매니저를 구성함.
        /// </summary>
        private void Awake()
        {   
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                TimestampLogHandler.Attach();

                if (!SessionManager.Instance)
                {
                    GameObject sessionObj = new GameObject("SessionManager");
                    sessionObj.AddComponent<SessionManager>();
                }

                Application.wantsToQuit += WantsToQuit;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (!reporter) reporter = FindObjectOfType<Reporter>();
        }

        /// <summary>
        /// 게임 초기 설정 로드 및 불필요한 마우스 커서/리포터 UI를 숨김.
        /// </summary>
        private void Start()
        {
            Cursor.visible = false;
            LoadSettings();
            if (reporter && reporter.show) reporter.show = false;
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
        /// 로컬 JSON 파일에서 전역 환경 설정값을 로드함. 누락 시 경고 로그를 남김.
        /// </summary>
        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);
            if (settings != null)
            {
                _fadeTime = settings.fadeTime;
            }
            else
            {
                Debug.LogWarning("Settings.json 설정이 누락됨.");
            }

            ApiConfig = JsonLoader.Load<ApiSettings>(GameConstants.Path.ApiSetting);
            if (ApiConfig == null)
            {
                Debug.LogWarning("API.json 설정이 누락됨.");
            }
        }

        /// <summary>
        /// 디버그 모드 전환 및 강제 씬 스킵 키보드 입력을 처리함.
        /// </summary>
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.D) && reporter)
            {
                reporter.showGameManagerControl = !reporter.showGameManagerControl;
                if (reporter.show) reporter.show = false;
            }
            else if (Input.GetKeyDown(KeyCode.M)) Cursor.visible = !Cursor.visible;

            if (Input.GetKeyDown(KeyCode.F))
            {
                isDebugMode = !isDebugMode;
                Debug.Log($"디버그 모드 {(isDebugMode ? "활성화" : "비활성화")} 됨");
            }

            if (isDebugMode && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                SkipToNextSceneDebug();
            }
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
            else if (currentScene == GameConstants.Scene.PlayTutorial) nextScene = $"Play_Q1{GetLevelSuffix(1)}";
            else if (currentScene.StartsWith("Play_Q"))
            {
                // # TODO: Substring 연산이 빈번하여 GC를 유발하므로 정규식 캐싱 구조 고려 필요.
                int qIdx = currentScene.IndexOf('Q') + 1;
                int underIdx = currentScene.IndexOf('_', qIdx);
                if (underIdx == -1) underIdx = currentScene.Length;
                
                // ex: currentScene="Play_Q14_A2", qIdx=6, underIdx=8 -> currentQ=14
                if (int.TryParse(currentScene.Substring(qIdx, underIdx - qIdx), out int currentQ))
                {
                    if (currentQ >= 15) nextScene = GameConstants.Scene.Ending;
                    else nextScene = $"Play_Q{currentQ + 1}{GetLevelSuffix(currentQ + 1)}";
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
                
                SceneManager.LoadScene(nextScene);
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
                SceneManager.LoadScene(sceneName);
                _isTransitioning = false;
                yield break;
            }

            bool fadeDone = false;
            FadeManager.Instance.FadeOut(_fadeTime, () => { fadeDone = true; });
            while (!fadeDone) yield return null;

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            while (asyncLoad != null && !asyncLoad.isDone) yield return null;

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

            firstTaggedPlayer = 0;

            if (SessionManager.Instance) SessionManager.Instance.ClearSession();

            _isTransitioning = false; 
            ChangeScene(GameConstants.Scene.Title);
        }

        /// <summary>
        /// 문항 번호와 사용자 세션을 조합하여 씬 접미사를 반환함. 유효하지 않은 씬일 경우 경고를 남김.
        /// </summary>
        public string GetLevelSuffix(int questionNumber)
        {
            if (questionNumber <= 0 || !SessionManager.Instance) return "";
            string currentType = SessionManager.Instance.CurrentUserType.ToString(); 
            // ex: questionNumber=4, currentType="A3" -> targetScene="Play_Q4_A3"
            string targetScene = $"Play_Q{questionNumber}_{currentType}";
            if (Application.CanStreamedLevelBeLoaded(targetScene))
            {
                return $"_{currentType}"; 
            }
            
            Debug.LogWarning($"씬 누락됨: {targetScene}");
            // 씬이 없더라도 타입 정보를 유지하여 호출부에서 적절히 처리하도록 함
            return $"_{currentType}";
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
            // # TODO: 파일 IO 관련 반복적인 DateTime 문자열 생성은 GC를 유발하므로 캐싱 고려 필요.
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