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
    /// 게임 전반의 상태(씬 전환, 대기 시간 체크, 공통 API 호출, 강제 종료 방어)를 관리하는 전역 싱글톤 매니저입니다.
    /// 플레이어 개인 데이터는 단일 책임 원칙에 따라 SessionManager에서 분리하여 관리합니다.
    /// </summary>
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

        /// <summary> 싱글톤 인스턴스 초기화 및 필수 의존성(SessionManager) 자동 생성 보장 </summary>
        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                // 전역 데이터 무결성을 위해 SessionManager가 없을 경우 동적 생성
                if (!SessionManager.Instance)
                {
                    GameObject sessionObj = new GameObject("SessionManager");
                    sessionObj.AddComponent<SessionManager>();
                }

                // 사용자가 창을 닫거나 강제 종료를 시도할 때, 서버에 퇴장 신호를 보내기 위해 종료 프로세스를 가로챔
                Application.wantsToQuit += WantsToQuit;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (!reporter) reporter = FindObjectOfType<Reporter>();
        }

        /// <summary> 초기 환경 설정(커서 숨김, JSON 데이터 로드) 수행 </summary>
        private void Start()
        {
            Cursor.visible = false;
            LoadSettings();
            if (reporter && reporter.show) reporter.show = false;
        }

        /// <summary> 객체 파괴 시 애플리케이션 종료 이벤트 구독 해제 </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Application.wantsToQuit -= WantsToQuit;
            }
        }

        /// <summary> ColorData 열거형 인덱스를 기반으로 사전 등록된 스프라이트를 반환합니다. </summary>
        public Sprite GetColorSprite(ColorData color)
        {
            int index = (int)color;
            if (index >= 0 && playerColorSprites != null && index < playerColorSprites.Length)
            {
                return playerColorSprites[index];
            }

            return null;
        }

        /// <summary> 환경 설정 및 API 엔드포인트 JSON 파일을 파싱하여 메모리에 캐싱합니다. </summary>
        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);
            if (settings != null)
            {
                _fadeTime = settings.fadeTime;
            }
            else
            {
                // 로드 실패 시 시스템 마비를 막기 위한 Fallback 기본값 설정
                _inactivityLimit = 60f;
                _fadeTime = 1.0f;
            }

            ApiConfig = JsonLoader.Load<ApiSettings>(GameConstants.Path.ApiSetting);
            if (ApiConfig == null) Debug.LogError("[GameManager] API.json 설정 파일을 로드하지 못했습니다.");
        }

        /// <summary> 매 프레임 키보드 디버그 입력 및 전역 무응답(Idle) 타이머 갱신 </summary>
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

        /// <summary> 타이틀 화면을 제외한 모든 씬에서 일정 시간 입력이 없을 경우 타이틀로 자동 회귀(리셋)시킵니다. </summary>
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

        /// <summary> 씬 전환 중복 호출 방지 플래그를 세팅하고 페이드 아웃 연출을 동반한 비동기 로딩을 시작합니다. </summary>
        public void ChangeScene(string sceneName)
        {
            if (_isTransitioning) return;

            _isTransitioning = true;
            Debug.Log($"[GameManager] Scene Transition Requested: {sceneName}");
            StartCoroutine(ChangeSceneRoutine(sceneName));
        }

        /// <summary> 화면을 암전시키고 백그라운드에서 다음 씬을 로드하여 시각적 끊김을 방지합니다. </summary>
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

        /// <summary> 현재 진행 상황을 전면 무효화하고 서버에 퇴장/리셋 신호를 보낸 뒤 타이틀 화면으로 돌아갑니다. </summary>
        public void ReturnToTitle()
        {
            if (_isTransitioning) return;

            Debug.Log("[GameManager] 타이틀로 돌아감");

            SendResetStartAPI();
            SendExitRoomAPI();

            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOff);
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedShotOff);
            }

            if (HueManager.Instance)
            {
                HueManager.Instance.SetLightStateAsync(1, false).Forget();
                HueManager.Instance.SetLightStateAsync(2, false).Forget();
            }

            firstTaggedPlayer = 0;
            _currentInactivityTimer = 0f;

            // 기존 플레이어의 잔존 데이터가 다음 플레이어에게 노출되지 않도록 세션 초기화
            if (SessionManager.Instance) SessionManager.Instance.ClearSession();

            ChangeScene(GameConstants.Scene.Title);
        }

        /// <summary> 유저 타입(A~F)과 현재 진행 중인 문제 번호에 맞춰 분기될 씬 이름의 접미사(Suffix)를 결정합니다. </summary>
        public string GetLevelSuffix(int questionNumber)
        {
            if (questionNumber <= 0 || !SessionManager.Instance) return "";

            // 기획된 플로우 차트에 따라 특정 문항에서만 분기가 갈리도록 하드코딩된 규칙 적용
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

        /// <summary> 공통 GET 방식 API 요청을 수행하는 단일 코루틴입니다. 중복 코드를 제거하고 유지보수성을 높입니다. </summary>
        private IEnumerator SendGetRequestRoutine(string url)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[GameManager] API 전송 실패: {req.error} (URL: {url})");
                }
            }
        }

        /// <summary> 룸 상태를 초기화(시작 시간 리셋)하는 API 호출 </summary>
        public void SendResetStartAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 || ApiConfig == null) return;

            string url =
                $"{ApiConfig.ResetStartUrl}?idx_user={SessionManager.Instance.CurrentUserId}&code={GameConstants.Module.Code.ToLower()}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        /// <summary> 현재 룸에서 유저를 퇴장 처리하는 API 호출 </summary>
        public void SendExitRoomAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 || ApiConfig == null) return;

            string url =
                $"{ApiConfig.ExitRoomUrl}?code={GameConstants.Module.Code.ToLower()}&idx_user={SessionManager.Instance.CurrentUserId}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        /// <summary> 세션 종료 시간을 서버에 기록하는 API 호출 </summary>
        public void SendTimeUpdateAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 || ApiConfig == null) return;

            string url =
                $"{ApiConfig.UpdateTimeUrl}?idx_user={SessionManager.Instance.CurrentUserId}&option=end&code={GameConstants.Module.Code.ToLower()}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        /// <summary> 각 문항(QnA 등)에서 유저가 선택한 값(1~5)을 서버에 전송하는 API 호출 </summary>
        public void SendValueUpdateAPI(int qNo, string side, int value)
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 || ApiConfig == null) return;

            string url =
                $"{ApiConfig.UpdateValueUrl}?idx_user={SessionManager.Instance.CurrentUserId}&q_no={qNo}&side={side}&code={GameConstants.Module.Code.ToLower()}&value={value}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        /// <summary> 획득한 마음 조각 보상 개수를 서버 데이터에 누적시키는 API 호출 </summary>
        public void SendPieceUpdateAPI(int value)
        {
            if (value < 0 || !SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 ||
                ApiConfig == null) return;

            string url =
                $"{ApiConfig.UpdatePieceUrl}?idx_user={SessionManager.Instance.CurrentUserId}&code={GameConstants.Module.Code.ToLower()}&value={value}";
            StartCoroutine(SendGetRequestRoutine(url));
        }

        #endregion

        #region 프로그램 강제 종료 시 예외 처리

        /// <summary> 
        /// Alt+F4, 창 닫기 등 강제 종료 이벤트 발생 시 앱 종료를 보류(return false)하고,
        /// 서버에 정상 퇴장 처리를 완료한 후 직접 종료 프로세스를 마저 수행하도록 제어합니다.
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

        /// <summary> 종료 전 룸 상태 초기화, 퇴장 API 전송 및 불필요한 로컬 더미 파일 정리를 순차적으로 실행합니다. </summary>
        private IEnumerator QuitRoutine()
        {
            // 종료 시 아두이노 LED 즉시 강제 소등
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOff);
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedShotOff);
            }

            // 종료 시 휴(Hue) 조명 즉시 강제 소등
            if (HueManager.Instance)
            {
                HueManager.Instance.SetLightStateAsync(1, false).Forget();
                HueManager.Instance.SetLightStateAsync(2, false).Forget();
            }

            if (SessionManager.Instance && SessionManager.Instance.CurrentUserId != 0 && ApiConfig != null)
            {
                int uid = SessionManager.Instance.CurrentUserId;

                string resetUrl =
                    $"{ApiConfig.ResetStartUrl}?idx_user={uid}&code={GameConstants.Module.Code.ToLower()}";
                using (UnityWebRequest req = UnityWebRequest.Get(resetUrl))
                {
                    req.timeout = 2; // 종료 지연 최소화를 위한 짧은 타임아웃
                    yield return req.SendWebRequest();
                }

                string exitUrl = $"{ApiConfig.ExitRoomUrl}?code={GameConstants.Module.Code.ToLower()}&idx_user={uid}";
                using (UnityWebRequest req = UnityWebRequest.Get(exitUrl))
                {
                    req.timeout = 2;
                    yield return req.SendWebRequest();
                }
            }

            // 백그라운드 파일 삭제가 완료될 때까지 코루틴을 일시 정지하여 앱이 메인 스레드를 유지하도록 보장
            yield return ClearSourceFoldersAsync().ToCoroutine();

            _isQuitSafe = true; // 안전 종료 플래그 활성화

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 환경에서 플레이(▶) 버튼을 다시 눌러 강제로 멈출 때는 WantsToQuit 이벤트가 타지 않으므로,
        /// OnApplicationQuit 생명주기에서 메인 스레드를 일시적으로 블로킹(Thread.Sleep)하더라도 강제로 통신을 수행합니다.
        /// </summary>
        private void OnApplicationQuit()
        {
            if (_isQuitSafe) return; // 이미 일반 종료 루틴이 완료되었다면 무시

            // 에디터 강제 정지 시에도 아두이노 및 휴 조명 소등
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOff);
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedShotOff);
            }

            if (HueManager.Instance)
            {
                HueManager.Instance.SetLightStateAsync(1, false).Forget();
                HueManager.Instance.SetLightStateAsync(2, false).Forget();
            }

            if (SessionManager.Instance && SessionManager.Instance.CurrentUserId != 0 && ApiConfig != null)
            {
                int uid = SessionManager.Instance.CurrentUserId;

                string resetUrl =
                    $"{ApiConfig.ResetStartUrl}?idx_user={uid}&code={GameConstants.Module.Code.ToLower()}";
                using (UnityWebRequest req = UnityWebRequest.Get(resetUrl))
                {
                    req.timeout = 2;
                    UnityWebRequestAsyncOperation op = req.SendWebRequest();
                    float deadline = Time.realtimeSinceStartup + 2.0f;

                    // 비동기 오퍼레이션이 완료될 때까지 메인 스레드 강제 대기 (에디터 전용 꼼수)
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

            // 에디터 종료 시에는 대기하지 않고 백그라운드 태스크만 호출(Fire and Forget)하여 유니티 에디터 프리징을 방지합니다.
            ClearSourceFoldersAsync().Forget();
        }
#endif

        /// <summary> 
        /// 영상 변환을 위해 임시로 저장했던 수많은 프레임 이미지(.png)들을 삭제하여 디스크 용량 낭비를 막습니다.
        /// 메인 스레드 블로킹(프리징)을 방지하기 위해 UniTask를 활용하여 백그라운드 스레드에서 I/O를 처리합니다.
        /// </summary>
        private async UniTask ClearSourceFoldersAsync()
        {
            // 유니티 API(Application.dataPath)는 메인 스레드에서만 접근 가능하므로 백그라운드 진입 전 미리 캐싱합니다.
            string dataPath = Application.dataPath;
            string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");

            try
            {
                // 무거운 파일 I/O 작업을 스레드 풀에 위임
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
                            try
                            {
                                File.Delete(file);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[GameManager] 타임랩스 소스 파일 삭제 실패 ({file}): {ex.Message}");
                            }
                        }
                    }

                    if (Directory.Exists(realtimeSource))
                    {
                        string[] rFiles = Directory.GetFiles(realtimeSource);
                        foreach (string file in rFiles)
                        {
                            try
                            {
                                File.Delete(file);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[GameManager] 리얼타임 소스 파일 삭제 실패 ({file}): {ex.Message}");
                            }
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