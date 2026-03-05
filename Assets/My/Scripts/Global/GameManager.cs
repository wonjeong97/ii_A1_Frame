using System.Collections;
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
        A, // 커플 표준형
        B, // 친구
        C, // 동료
        D, // 부모-성인자녀
        E, // 2년 이상 커플
        F  // 부부사이 (추후)
    }

    /// <summary> 게임 전반적인 상태 및 씬 전환 관리 매니저 </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance; // 싱글톤 인스턴스

        [SerializeField] private Reporter reporter; // 로그 리포터 참조

        private float _currentInactivityTimer; // 현재 비활성 시간 타이머
        private bool _isTransitioning; // 씬 전환 중 여부
        private float _inactivityLimit = 60f; // 비활성 제한 시간
        private float _fadeTime = 1.0f; // 페이드 시간

        // 플레이어 태그 정보 (0: 없음, 1: Player1, 2: Player2)
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

        // 각 타입/질문별 얻은 마음 조각(Piece) 데이터
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

        // 모든 마음 조각의 합계를 반환하는 프로퍼티
        public int TotalPieces => PieceA2 + PieceA3 + 
                                  PieceB1 + PieceB2 + PieceB3 + 
                                  PieceC1 + PieceC2 + PieceC3 + 
                                  PieceD1 + PieceD2 + PieceD3;  // A1은 해당 컨텐츠이므로 계산에서 제외함.

        [Header("Player Color Sprites")]
        [Tooltip("인덱스 순서대로 등록하세요. 0:Cyan, 1:Pink, 2:Orange, 3:Green, 4:Red, 5:Yellow")]
        public Sprite[] playerColorSprites;

        /// <summary> 싱글톤 초기화 </summary>
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (reporter == null)
            {
                reporter = FindObjectOfType<Reporter>();
            }
        }

        /// <summary> 초기 설정 및 커서 숨김 </summary>
        private void Start()
        {
            Cursor.visible = false;
            LoadSettings();
            
            if (reporter != null && reporter.show)
            {
                reporter.show = false;
            }
        }

        /// <summary> ColorData에 해당하는 스프라이트 반환 </summary>
        public Sprite GetColorSprite(ColorData color)
        {
            int index = (int)color;
            if (index >= 0 && playerColorSprites != null && index < playerColorSprites.Length)
            {
                return playerColorSprites[index];
            }
            return null;
        }

        /// <summary> 설정 파일 로드 </summary>
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
            if (ApiConfig == null)
            {
                Debug.LogError("[GameManager] API.json 설정 파일을 로드하지 못했습니다.");
            }
        }

        /// <summary> 입력 감지 및 비활성 체크 </summary>
        private void Update()
        {
            // D키: 리포터(로그) 제어
            if (Input.GetKeyDown(KeyCode.D) && reporter)
            {
                reporter.showGameManagerControl = !reporter.showGameManagerControl;
                if (reporter.show) reporter.show = false;
            }
            // M키: 마우스 커서 토글
            else if (Input.GetKeyDown(KeyCode.M))
            {
                Cursor.visible = !Cursor.visible;
            }

            if (_isTransitioning) return;

            HandleInactivity();
        }

        /// <summary> 사용자 입력 부재 감지 </summary>
        private void HandleInactivity()
        {
            // 현재 씬이 Title이라면 비활성 타이머 작동 안함
            if (SceneManager.GetActiveScene().name == GameConstants.Scene.Title)
            {
                _currentInactivityTimer = 0f;
                return;
            }

            if (Input.anyKey || Input.touchCount > 0)
            {
                _currentInactivityTimer = 0f;
            }
            else
            {
                _currentInactivityTimer += Time.deltaTime;
                if (_currentInactivityTimer >= _inactivityLimit)
                {
                    ReturnToTitle();
                }
            }
        }
    
        /// <summary> 씬 전환 요청 </summary>
        public void ChangeScene(string sceneName)
        {
            if (_isTransitioning) return;
            
            _isTransitioning = true;
            Debug.Log($"[GameManager] Scene Transition Requested: {sceneName}");
            StartCoroutine(ChangeSceneRoutine(sceneName));
        }

        /// <summary> 페이드 효과를 포함한 씬 전환 코루틴 </summary>
        private IEnumerator ChangeSceneRoutine(string sceneName)
        {
            if (!FadeManager.Instance)
            {
                Debug.LogWarning("[GameManager] FadeManager instance not found. Loading immediately.");
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

        /// <summary> 타이틀 화면 복귀 및 초기화 처리 </summary>
        public void ReturnToTitle()
        {
            if (_isTransitioning) return;
            
            Debug.Log("[GameManager] 타이틀로 돌아감");

            // 강제 초기화 시 서버 측 상태(resetStart)와 방 점유(exitRoom)를 모두 리셋
            SendResetStartAPI();
            SendExitRoomAPI();

            // 상태 초기화
            firstTaggedPlayer = 0; 
            _currentInactivityTimer = 0f;
            CurrentUserId = 0; 

            ChangeScene(GameConstants.Scene.Title);
        }

        /// <summary> 질문 번호와 현재 유저 타입에 따라 적용될 Suffix(_A, _B 등) 반환 </summary>
        public string GetLevelSuffix(int questionNumber)
        {
            if (questionNumber <= 0) return ""; 

            switch (currentUserType)
            {
                case UserType.A: return "_A"; 
                case UserType.B:
                    if (questionNumber == 4) return "_B";
                    return "_A";
                case UserType.C:
                    if (questionNumber == 4 || questionNumber == 10 || questionNumber == 11 || 
                        questionNumber == 13 || questionNumber == 14 || questionNumber == 15)
                    {
                        return "_C";
                    }
                    return "_A";
                case UserType.D: return "_D"; 
                case UserType.E: return "_E"; 
                case UserType.F: return "_F";
                default: return "_A";
            }
        }

       #region API 호출 로직 (시간 및 값 기록)

        /// <summary> 초기화(타임아웃 등) 시 서버 상태를 리셋 </summary>
        public void SendResetStartAPI()
        {
            int userId = CurrentUserId;
            if (userId == 0)
            {
                Debug.LogWarning($"[GameManager] CurrentUserId가 0입니다. ResetStart API 호출을 건너뜁니다.");
                return;
            }
            StartCoroutine(ResetStartRoutine(userId));
        }

        private IEnumerator ResetStartRoutine(int userId)
        {
            if (ApiConfig == null) yield break;
            
            string url = $"{ApiConfig.ResetStartUrl}?idx_user={userId}&code=a1";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) 
                    Debug.LogError($"[ResetStart API] 에러: {req.error}");
                else 
                    Debug.Log($"[ResetStart API] 시작 상태 초기화 성공");
            }
        }

        /// <summary> 방 퇴장(exitRoom) 상태를 서버에 업데이트합니다. </summary>
        public void SendExitRoomAPI()
        {
            int userId = CurrentUserId; // 캡처
            if (userId == 0)
            {
                Debug.LogWarning($"[GameManager] CurrentUserId가 0입니다. ExitRoom API 호출을 건너뜁니다.");
                return;
            }
            StartCoroutine(ExitRoomRoutine(userId));
        }

        private IEnumerator ExitRoomRoutine(int userId)
        {
            if (ApiConfig == null) yield break;
            
            string url = $"{ApiConfig.ExitRoomUrl}?code=a1&idx_user={userId}";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) 
                    Debug.LogError($"[ExitRoom API] 에러: {req.error}");
                else 
                    Debug.Log($"[ExitRoom API] 방 퇴장 업데이트 성공");
            }
        }

        /// <summary> 콘텐츠 종료(end) 시간을 서버에 기록합니다. </summary>
        public void SendTimeUpdateAPI()
        {
            int userId = CurrentUserId;
            if (userId == 0)
            {
                Debug.LogWarning($"[GameManager] CurrentUserId가 0입니다. end API 호출을 건너뜁니다.");
                return;
            }
            StartCoroutine(TimeUpdateRoutine(userId));
        }

        private IEnumerator TimeUpdateRoutine(int userId)
        {
            if (ApiConfig == null) yield break;
            
            string url = $"{ApiConfig.UpdateTimeUrl}?idx_user={userId}&option=end&code=a1";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) 
                    Debug.LogError($"[Time API] 에러: {req.error}");
                else 
                    Debug.Log($"[Time API] end 업데이트 성공");
            }
        }

        /// <summary> 사용자의 질문 응답 값을 서버에 업데이트합니다. </summary>
        public void SendValueUpdateAPI(int qNo, string side, int value)
        {
            int userId = CurrentUserId;
            if (userId == 0)
            {
                Debug.LogWarning("[GameManager] CurrentUserId가 0입니다. Value 업데이트를 건너뜁니다.");
                return;
            }
            StartCoroutine(ValueUpdateRoutine(userId, qNo, side, value));
        }

        private IEnumerator ValueUpdateRoutine(int userId, int qNo, string side, int value)
        {
            if (ApiConfig == null) yield break; 

            string url = $"{ApiConfig.UpdateValueUrl}?idx_user={userId}&q_no={qNo}&side={side}&code=a1&value={value}";
            
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) Debug.LogError($"[Value API] 통신 에러: {req.error}");
                else Debug.Log($"[Value API] {side} Q{qNo} 값({value}) 업데이트 성공");
            }
        }

        /// <summary> 획득한 마음 조각 개수를 서버에 업데이트합니다. </summary>
        public void SendPieceUpdateAPI(int value)
        {
            int userId = CurrentUserId;
            if (value < 0)
            {
                Debug.LogWarning($"[GameManager] 유효하지 않은 Piece 값입니다: {value}");
                return;
            }
            if (userId == 0)
            {
                Debug.LogWarning("[GameManager] CurrentUserId가 0입니다. Piece 업데이트를 건너뜁니다.");
                return;
            }
            StartCoroutine(PieceUpdateRoutine(userId, value));
        }

        private IEnumerator PieceUpdateRoutine(int userId, int value)
        {
            if (ApiConfig == null) yield break;

            string url = $"{ApiConfig.UpdatePieceUrl}?idx_user={userId}&code=a1&value={value}";
            
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) 
                    Debug.LogError($"[Piece API] 에러: {req.error}");
                else 
                    Debug.Log($"[Piece API] 마음 조각({value}개) 업데이트 성공! (URL: {url})");
            }
        }

        #endregion

        #region 프로그램 강제 종료 시 예외 처리

        /// <summary> 앱이 비정상적으로 종료될 때(Alt+F4 등) 남은 세션 정리 </summary>
        private void OnApplicationQuit()
        {
            if (CurrentUserId != 0 && ApiConfig != null)
            {
                // 1. 유저 Start 초기화
                string resetUrl = $"{ApiConfig.ResetStartUrl}?idx_user={CurrentUserId}&code=a1";
                using (UnityWebRequest req = UnityWebRequest.Get(resetUrl))
                {
                    req.SendWebRequest();
                    // 꺼지는 찰나이므로 완료될 때까지 메인 스레드를 붙잡고 기다립니다.
                    while (!req.isDone) 
                    { 
                        System.Threading.Thread.Sleep(10); 
                    }
                }

                // 2. 방 점유 초기화
                string exitUrl = $"{ApiConfig.ExitRoomUrl}?code=a1&idx_user={CurrentUserId}";
                using (UnityWebRequest req = UnityWebRequest.Get(exitUrl))
                {
                    req.SendWebRequest();
                    while (!req.isDone) 
                    { 
                        System.Threading.Thread.Sleep(10); 
                    }
                }
                
                Debug.Log("[GameManager] OnApplicationQuit: API 통신 완료.");
            }
        }

        #endregion
    }
}