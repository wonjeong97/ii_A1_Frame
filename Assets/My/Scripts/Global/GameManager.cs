using System;
using System.Collections;
using System.IO;
using My.Scripts.Core;
using My.Scripts.Core.Data;
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
    /// 게임의 전반적인 상태(씬 전환, 대기 시간 체크, API 호출, 강제 종료 처리)를 관리합니다.
    /// 플레이어의 개인 데이터는 SessionManager로 분리되었습니다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        [SerializeField] private Reporter reporter;

        private float _currentInactivityTimer;
        private bool _isTransitioning;
        private float _inactivityLimit = 60f;
        private float _fadeTime = 1.0f;
        private bool _isQuitting;
        private bool _isQuitSafe;

        public int firstTaggedPlayer = 0;
        public ApiSettings ApiConfig { get; private set; }
        
        [Header("Player Color Sprites")]
        public Sprite[] playerColorSprites;

        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                // SessionManager 자동 생성 보장
                if (!SessionManager.Instance)
                {
                    GameObject sessionObj = new GameObject("SessionManager");
                    sessionObj.AddComponent<SessionManager>();
                }
                
                // 앱 종료를 지연시키고 비동기 정리를 실행하기 위한 이벤트 구독
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
            if (SceneManager.GetActiveScene().name == GameConstants.Scene.Title)
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
            Debug.Log("[GameManager] 타이틀로 돌아감");

            SendResetStartAPI();
            SendExitRoomAPI();

            firstTaggedPlayer = 0; 
            _currentInactivityTimer = 0f;
            
            if (SessionManager.Instance) SessionManager.Instance.ClearSession(); 

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

       #region API 호출 로직

        public void SendResetStartAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0) return;
            StartCoroutine(ResetStartRoutine(SessionManager.Instance.CurrentUserId));
        }

        private IEnumerator ResetStartRoutine(int userId)
        {
            if (ApiConfig == null) yield break;
            string url = $"{ApiConfig.ResetStartUrl}?idx_user={userId}&code={GameConstants.Module.Code.ToLower()}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
            }
        }

        public void SendExitRoomAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0) return;
            StartCoroutine(ExitRoomRoutine(SessionManager.Instance.CurrentUserId));
        }

        private IEnumerator ExitRoomRoutine(int userId)
        {
            if (ApiConfig == null) yield break;
            string url = $"{ApiConfig.ExitRoomUrl}?code={GameConstants.Module.Code.ToLower()}&idx_user={userId}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
            }
        }

        public void SendTimeUpdateAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0) return;
            StartCoroutine(TimeUpdateRoutine(SessionManager.Instance.CurrentUserId));
        }

        private IEnumerator TimeUpdateRoutine(int userId)
        {
            if (ApiConfig == null) yield break;
            string url = $"{ApiConfig.UpdateTimeUrl}?idx_user={userId}&option=end&code={GameConstants.Module.Code.ToLower()}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
            }
        }

        public void SendValueUpdateAPI(int qNo, string side, int value)
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0) return;
            StartCoroutine(ValueUpdateRoutine(SessionManager.Instance.CurrentUserId, qNo, side, value));
        }

        private IEnumerator ValueUpdateRoutine(int userId, int qNo, string side, int value)
        {
            if (ApiConfig == null) yield break; 
            string url = $"{ApiConfig.UpdateValueUrl}?idx_user={userId}&q_no={qNo}&side={side}&code={GameConstants.Module.Code.ToLower()}&value={value}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
            }
        }

        public void SendPieceUpdateAPI(int value)
        {
            if (value < 0 || !SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0) return;
            StartCoroutine(PieceUpdateRoutine(SessionManager.Instance.CurrentUserId, value));
        }

        private IEnumerator PieceUpdateRoutine(int userId, int value)
        {
            if (ApiConfig == null) yield break;
            string url = $"{ApiConfig.UpdatePieceUrl}?idx_user={userId}&code={GameConstants.Module.Code.ToLower()}&value={value}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
            }
        }

        #endregion

        #region 프로그램 강제 종료 시 예외 처리


        // [수정됨] Thread.Sleep을 사용한 동기 대기 방식을 폐기하고, Application.wantsToQuit을 활용한 비동기 안전 종료 처리로 교체합니다.
        private bool WantsToQuit()
        {
            if (_isQuitSafe) return true;

            if (!_isQuitting)
            {
                _isQuitting = true;
                StartCoroutine(QuitRoutine());
            }
            
            return false; // 통신과 폴더 정리가 끝날 때까지 1차적인 종료를 캔슬
        }

        private IEnumerator QuitRoutine()
        {
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

            ClearSourceFolders();
            
            _isQuitSafe = true;
            
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(); // 정리가 완료된 후 실제 종료 호출
#endif
        }

        private void ClearSourceFolders()
        {
            try
            {
                string dataPath = Application.dataPath;
                DirectoryInfo parentDir = Directory.GetParent(dataPath);
                string rootPath = parentDir != null ? parentDir.FullName : dataPath;
                string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");

                string timelapseSource = Path.Combine(rootPath, "Timelapse", "Timelapse_Source", dateFolder);
                string realtimeSource = Path.Combine(rootPath, "Timelapse", "Realtime_Source", dateFolder);

                if (Directory.Exists(timelapseSource))
                    foreach (string file in Directory.GetFiles(timelapseSource)) File.Delete(file);

                if (Directory.Exists(realtimeSource))
                    foreach (string file in Directory.GetFiles(realtimeSource)) File.Delete(file);

                Debug.Log("[GameManager] 앱 종료 시 소스 폴더 정리 완료");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameManager] 앱 종료 시 소스 폴더 정리 중 오류: {e.Message}");
            }
        }

        #endregion
    }
}