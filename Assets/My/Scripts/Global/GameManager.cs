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
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        [SerializeField] private Reporter reporter;

        private float _currentInactivityTimer;
        private bool _isTransitioning;
        private float _inactivityLimit = 60f;
        private float _fadeTime = 0.5f;
        private bool _isQuitting;
        private bool _isQuitSafe;

        public int firstTaggedPlayer = 0;
        public ApiSettings ApiConfig { get; set; }

        [Header("Player Color Sprites")]
        public Sprite[] playerColorSprites;
        
        [Header("API Retry Settings")]
        [SerializeField] private int maxRetries = 10;
        [SerializeField] private float retryDelay = 1.0f;

        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

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

        private void Start()
        {
            Cursor.visible = false;
            LoadSettings();
            if (reporter && reporter.show) reporter.show = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Application.wantsToQuit -= WantsToQuit;
            }
        }

        public Sprite GetColorSprite(ColorData color)
        {
            int index = (int)color;
            if (index >= 0 && playerColorSprites != null && index < playerColorSprites.Length)
            {
                return playerColorSprites[index];
            }
            return null;
        }

        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);
            if (settings != null)
            {
                _fadeTime = settings.fadeTime;
            }
            else
            {
                _inactivityLimit = 60f;
                _fadeTime = 1.0f;
            }

            ApiConfig = JsonLoader.Load<ApiSettings>(GameConstants.Path.ApiSetting);
            if (ApiConfig == null) Debug.LogError("[GameManager] API.json 설정 파일을 로드하지 못했습니다.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.D) && reporter)
            {
                reporter.showGameManagerControl = !reporter.showGameManagerControl;
                if (reporter.show) reporter.show = false;
            }
            else if (Input.GetKeyDown(KeyCode.M)) Cursor.visible = !Cursor.visible;

            if (_isTransitioning) return;

            HandleInactivity();
        }

        private void HandleInactivity()
        {
            if (SceneManager.GetActiveScene().name == GameConstants.Scene.Title ||
                SceneManager.GetActiveScene().name == GameConstants.Scene.Ending)
            {
                _currentInactivityTimer = 0f;
                return;
            }

            if (Input.anyKey || Input.touchCount > 0) _currentInactivityTimer = 0f;
            else
            {
                _currentInactivityTimer += Time.deltaTime;
                if (_currentInactivityTimer >= _inactivityLimit) ReturnToTitle();
            }
        }

        public void ChangeScene(string sceneName)
        {
            if (_isTransitioning) return;

            _isTransitioning = true;
            Debug.Log($"[GameManager] Scene Transition Requested: {sceneName}");
            StartCoroutine(ChangeSceneRoutine(sceneName));
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

        public void ReturnToTitle()
        {
            if (_isTransitioning) return;
            ReturnToTitleAsync().Forget();
        }

        private async UniTaskVoid ReturnToTitleAsync()
        {
            _isTransitioning = true;

            Debug.Log("[GameManager] 타이틀로 돌아감");

            SendResetStartAPI();
            SendExitRoomAPI();

            await TurnOffAllHardwareOutputsAsync();

            firstTaggedPlayer = 0;
            _currentInactivityTimer = 0f;

            if (SessionManager.Instance) SessionManager.Instance.ClearSession();

            _isTransitioning = false; 
            ChangeScene(GameConstants.Scene.Title);
        }

        public string GetLevelSuffix(int questionNumber)
        {
            if (questionNumber <= 0 || !SessionManager.Instance) return "";

            switch (SessionManager.Instance.CurrentUserType)
            {
                case UserType.A: return "_A";
                case UserType.B: return (questionNumber == 4) ? "_B" : "_A";
                case UserType.C:
                    if (questionNumber == 4 || questionNumber == 10 || questionNumber == 11 ||
                        questionNumber == 13 || questionNumber == 14 || questionNumber == 15) return "_C";

                    return "_A";
                case UserType.D: return "_D";
                case UserType.E: return "_E";
                case UserType.F: return "_F";
                default: return "_A";
            }
        }

        #region Hardware Control Helper

        private async UniTask TurnOffAllHardwareOutputsAsync()
        {
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOff);
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedShotOff);
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
                    Debug.LogWarning("[GameManager] 휴 조명 소등 대기 타임아웃.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GameManager] 휴 조명 소등 중 예외: {ex.Message}");
                }
            }
        }

        #endregion

        #region API 호출 로직

        private IEnumerator SendGetRequestRoutine(string url)
        {
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
                        Debug.LogWarning($"[GameManager] API 전송 실패 ({attempt + 1}/{maxRetries}): {req.error}. {retryDelay}초 후 재시도...");
                        yield return CoroutineData.GetWaitForSeconds(retryDelay);
                    }
                    else
                    {
                        Debug.LogError($"[GameManager] API 전송 최종 실패 (URL: {url}) - {req.error}");
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

        private IEnumerator QuitRoutine()
        {
            yield return TurnOffAllHardwareOutputsAsync().ToCoroutine();

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

            if (SessionManager.Instance && SessionManager.Instance.CurrentUserId != 0 && ApiConfig != null)
            {
                int uid = SessionManager.Instance.CurrentUserId;

                string resetUrl = $"{ApiConfig.ResetStartUrl}?idx_user={uid}&code={GameConstants.Module.Code.ToLower()}";
                using (UnityWebRequest req = UnityWebRequest.Get(resetUrl))
                {
                    req.timeout = 2;
                    UnityWebRequestAsyncOperation op = req.SendWebRequest();
                    float deadline = Time.realtimeSinceStartup + 2.0f;

                    while (!op.isDone && Time.realtimeSinceStartup < deadline)
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                }

                string exitUrl = $"{ApiConfig.ExitRoomUrl}?code={GameConstants.Module.Code.ToLower()}&idx_user={uid}";
                using (UnityWebRequest req = UnityWebRequest.Get(exitUrl))
                {
                    req.timeout = 2;
                    UnityWebRequestAsyncOperation op = req.SendWebRequest();
                    float deadline = Time.realtimeSinceStartup + 2.0f;
                    while (!op.isDone && Time.realtimeSinceStartup < deadline)
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                }
            }

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

                    if (Directory.Exists(timelapseSource))
                    {
                        string[] tFiles = Directory.GetFiles(timelapseSource);
                        foreach (string file in tFiles)
                        {
                            try { File.Delete(file); }
                            catch (Exception ex) { Debug.LogWarning($"[GameManager] 타임랩스 소스 파일 삭제 실패 ({file}): {ex.Message}"); }
                        }
                    }

                    if (Directory.Exists(realtimeSource))
                    {
                        string[] rFiles = Directory.GetFiles(realtimeSource);
                        foreach (string file in rFiles)
                        {
                            try { File.Delete(file); }
                            catch (Exception ex) { Debug.LogWarning($"[GameManager] 리얼타임 소스 파일 삭제 실패 ({file}): {ex.Message}"); }
                        }
                    }

                    Debug.Log("[GameManager] 백그라운드 스레드에서 앱 종료 전 소스 폴더 정리 완료");
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameManager] 비동기 소스 폴더 접근 오류: {e.Message}");
            }
        }

        #endregion
    }
}