using System.Collections;
using System.Collections.Generic;
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
    public enum UserType
    {
        A, B, C, D, E, F
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        [SerializeField] private Reporter reporter;

        private float _currentInactivityTimer;
        private bool _isTransitioning;
        private float _inactivityLimit = 60f;
        private float _fadeTime = 1.0f;

        public int firstTaggedPlayer = 0;
        public UserType currentUserType = UserType.A;
        
        public ApiSettings ApiConfig { get; private set; }
        
        // --- API 연동 데이터 캐싱 ---
        public int CurrentUserId { get; set; } 
        public string PlayerAUid { get; set; } = string.Empty;
        public string PlayerBUid { get; set; } = string.Empty;
        public string CurrentLanguage { get; set; } = "ko";
        public string PlayerALastName { get; set; } = "NoNameA";
        public string PlayerBLastName { get; set; } = "NoNameB";
        public ColorData PlayerAColor { get; set; } = ColorData.NotSet;
        public ColorData PlayerBColor { get; set; } = ColorData.NotSet;

        public string CurrentModuleCode { get; set; } = "A1";

        // [추가] 카트리지 내 A1을 제외한 나머지 콘텐츠 클리어 여부
        public string Cartridge { get; set; } = string.Empty;
        public bool IsOtherCartridgeContentsCleared { get; set; } = false;

        public int PieceA1 { get; set; }
        public int PieceA2 { get; set; }
        public int PieceA3 { get; set; }
        public int PieceB1 { get; set; }
        public int PieceB2 { get; set; }
        public int PieceB3 { get; set; }
        public int PieceC1 { get; set; }
        public int PieceC2 { get; set; }
        public int PieceC3 { get; set; }
        public int PieceD1 { get; set; }
        public int PieceD2 { get; set; }
        public int PieceD3 { get; set; }

        public int TotalPieces => PieceA2 + PieceA3 + 
                                  PieceB1 + PieceB2 + PieceB3 + 
                                  PieceC1 + PieceC2 + PieceC3 + 
                                  PieceD1 + PieceD2 + PieceD3;

        [Header("Player Color Sprites")]
        public Sprite[] playerColorSprites;

        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
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
            CurrentUserId = 0; 

            ChangeScene(GameConstants.Scene.Title);
        }

        public string GetLevelSuffix(int questionNumber)
        {
            if (questionNumber <= 0) return ""; 
            switch (currentUserType)
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
            if (CurrentUserId == 0) return;
            StartCoroutine(ResetStartRoutine(CurrentUserId));
        }

        private IEnumerator ResetStartRoutine(int userId)
        {
            if (ApiConfig == null) yield break;
            string url = $"{ApiConfig.ResetStartUrl}?idx_user={userId}&code={CurrentModuleCode.ToLower()}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
            }
        }

        public void SendExitRoomAPI()
        {
            if (CurrentUserId == 0) return;
            StartCoroutine(ExitRoomRoutine(CurrentUserId));
        }

        private IEnumerator ExitRoomRoutine(int userId)
        {
            if (ApiConfig == null) yield break;
            string url = $"{ApiConfig.ExitRoomUrl}?code={CurrentModuleCode.ToLower()}&idx_user={userId}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
            }
        }

        public void SendTimeUpdateAPI()
        {
            if (CurrentUserId == 0) return;
            StartCoroutine(TimeUpdateRoutine(CurrentUserId));
        }

        private IEnumerator TimeUpdateRoutine(int userId)
        {
            if (ApiConfig == null) yield break;
            string url = $"{ApiConfig.UpdateTimeUrl}?idx_user={userId}&option=end&code={CurrentModuleCode.ToLower()}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
            }
        }

        public void SendValueUpdateAPI(int qNo, string side, int value)
        {
            if (CurrentUserId == 0) return;
            StartCoroutine(ValueUpdateRoutine(CurrentUserId, qNo, side, value));
        }

        private IEnumerator ValueUpdateRoutine(int userId, int qNo, string side, int value)
        {
            if (ApiConfig == null) yield break; 
            string url = $"{ApiConfig.UpdateValueUrl}?idx_user={userId}&q_no={qNo}&side={side}&code={CurrentModuleCode.ToLower()}&value={value}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
            }
        }

        public void SendPieceUpdateAPI(int value)
        {
            if (value < 0 || CurrentUserId == 0) return;
            StartCoroutine(PieceUpdateRoutine(CurrentUserId, value));
        }

        private IEnumerator PieceUpdateRoutine(int userId, int value)
        {
            if (ApiConfig == null) yield break;
            string url = $"{ApiConfig.UpdatePieceUrl}?idx_user={userId}&code={CurrentModuleCode.ToLower()}&value={value}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
            }
        }

        #endregion

        #region 프로그램 강제 종료 시 예외 처리

        private void OnApplicationQuit()
        {
            if (CurrentUserId != 0 && ApiConfig != null)
            {   
                string resetUrl = $"{ApiConfig.ResetStartUrl}?idx_user={CurrentUserId}&code={CurrentModuleCode.ToLower()}";
                using (UnityWebRequest req = UnityWebRequest.Get(resetUrl))
                {   
                    req.timeout = 2;
                    var op = req.SendWebRequest();
                    float deadline = Time.realtimeSinceStartup + 2.5f;
                    while (!op.isDone && Time.realtimeSinceStartup < deadline)
                    {
                        System.Threading.Thread.Sleep(10);
                    }

                    if (!op.isDone)
                    {
                        req.Abort();
                        Debug.LogWarning("[GameManager] OnApplicationQuit resetStart 요청 타임아웃");
                    }
                }

                string exitUrl = $"{ApiConfig.ExitRoomUrl}?code={CurrentModuleCode.ToLower()}&idx_user={CurrentUserId}";
                using (UnityWebRequest req = UnityWebRequest.Get(exitUrl))
                {   
                    req.timeout = 2;
                    var op = req.SendWebRequest();
                    float deadline = Time.realtimeSinceStartup + 2.5f;
                    while (!op.isDone && Time.realtimeSinceStartup < deadline)
                    {
                        System.Threading.Thread.Sleep(10);
                    }

                    if (!op.isDone)
                    {
                        req.Abort();
                        Debug.LogWarning("[GameManager] OnApplicationQuit exitRoom 요청 타임아웃");
                    }
                }
            }
        }

        #endregion
    }
}