using System;
using System.IO;
using Cysharp.Threading.Tasks;
using My.Scripts.Core;
using My.Scripts.Core.Data;
using My.Scripts.Hardware;
using UnityEditor;
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
            _fadeTime = 0.5f;
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
            if (index >= 0 && playerColorSprites != null && playerColorSprites.Length > index)
            {
                Sprite targetSprite = playerColorSprites[index];
                if (targetSprite) return targetSprite;
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
            string nextScene = DetermineNextDebugScene(currentScene);

            if (string.IsNullOrEmpty(nextScene)) return;

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

        /// <summary>
        /// 현재 씬의 이름을 기반으로 다음에 이동할 씬의 이름을 결정함.
        /// </summary>
        private string DetermineNextDebugScene(string currentScene)
        {
            if (currentScene == GameConstants.Scene.Title) return GameConstants.Scene.Tutorial;
            if (currentScene == GameConstants.Scene.Tutorial) return GameConstants.Scene.PlayTutorial;
            if (currentScene == GameConstants.Scene.PlayTutorial) return "Play_Q1";

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

        /// <summary>
        /// 현재 문항 번호를 파싱하여 다음 문항 또는 엔딩 씬 이름을 반환함.
        /// </summary>
        private string GetNextQuestionScene(string currentScene)
        {
            int qIdx = currentScene.IndexOf('Q') + 1;
            if (int.TryParse(currentScene.Substring(qIdx), out int currentQ))
            {
                return currentQ >= 15 ? GameConstants.Scene.Ending : $"Play_Q{currentQ + 1}";
            }

            return null;
        }

        /// <summary>
        /// 페이드 아웃 연출을 동반하여 지정된 씬으로 이동함.
        /// </summary>
        public void ChangeScene(string sceneName)
        {
            if (_isTransitioning) return;

            ChangeSceneAsync(sceneName).Forget(); // Forget()으로 비동기 호출
        }

        /// <summary> 페이드 연출과 함께 씬을 비동기로 로드함. </summary>
        private async UniTaskVoid ChangeSceneAsync(string sceneName)
        {
            _isTransitioning = true;

            if (!FadeManager.Instance)
            {
                await SceneLoader.LoadAsync(sceneName);
                _isTransitioning = false;
                return;
            }

            UniTaskCompletionSource fadeTcs = new UniTaskCompletionSource();
            FadeManager.Instance.FadeOut(_fadeTime, () => fadeTcs.TrySetResult());
            await fadeTcs.Task;

            await SceneLoader.LoadAsync(sceneName);

            FadeManager.Instance.FadeIn(_fadeTime);
            _isTransitioning = false;
        }

        /// <summary> 앱 종료 요청 시 하드웨어 정리 및 임시 파일을 삭제한 후 안전하게 종료함. </summary>
        private async UniTaskVoid QuitAsync()
        {
            // 하드웨어 소등 대기
            await TurnOffAllHardwareOutputsAsync();

#if !UNITY_EDITOR
            if (SessionManager.Instance && SessionManager.Instance.CurrentUserId != 0 && ApiConfig != null)
            {
                int uid = SessionManager.Instance.CurrentUserId;
                string moduleCode = GameConstants.Module.Code.ToLower();

                // 종료 시점의 마지막 API 상태 동기화
                string resetUrl = $"{ApiConfig.ResetStartUrl}?idx_user={uid}&code={moduleCode}";
                string exitUrl = $"{ApiConfig.ExitRoomUrl}?code={moduleCode}&idx_user={uid}";

                await UniTask.WhenAll(SendGetRequestAsync(resetUrl), SendGetRequestAsync(exitUrl));
            }
#endif
            // 임시 소스 파일 정리 후 종료 확정
            await ClearSourceFoldersAsync();

            _isQuitSafe = true;

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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
        /// API 요청을 비동기로 수행하며 실패 시 재시도함.
        /// 호출 측에서 응답을 기다리지 않도록 Forget()과 조합하여 사용 권장.
        /// </summary>
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

                    if (req.result == UnityWebRequest.Result.Success) return;

                    // 재시도 간 지연 시간 부여
                    if (attempt < maxRetries - 1)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(retryDelay));
                    }
                }
            }
        }

        public void SendResetStartAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 || ApiConfig == null) return;

            string url =
                $"{ApiConfig.ResetStartUrl}?idx_user={SessionManager.Instance.CurrentUserId}&code={GameConstants.Module.Code.ToLower()}";
            SendGetRequestAsync(url).Forget();
        }

        public void SendExitRoomAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 || ApiConfig == null) return;

            string url =
                $"{ApiConfig.ExitRoomUrl}?code={GameConstants.Module.Code.ToLower()}&idx_user={SessionManager.Instance.CurrentUserId}";
            SendGetRequestAsync(url).Forget();
        }

        public void SendTimeUpdateAPI()
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 || ApiConfig == null) return;

            string url =
                $"{ApiConfig.UpdateTimeUrl}?idx_user={SessionManager.Instance.CurrentUserId}&option=end&code={GameConstants.Module.Code.ToLower()}";
            SendGetRequestAsync(url).Forget();
        }

        public void SendValueUpdateAPI(int qNo, string side, int value)
        {
            if (!SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 || ApiConfig == null) return;

            string url =
                $"{ApiConfig.UpdateValueUrl}?idx_user={SessionManager.Instance.CurrentUserId}&q_no={qNo}&side={side}&code={GameConstants.Module.Code.ToLower()}&value={value}";
            SendGetRequestAsync(url).Forget();
        }

        public void SendPieceUpdateAPI(int value)
        {
            if (value < 0 || !SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0 ||
                ApiConfig == null) return;

            string url =
                $"{ApiConfig.UpdatePieceUrl}?idx_user={SessionManager.Instance.CurrentUserId}&code={GameConstants.Module.Code.ToLower()}&value={value}";
            SendGetRequestAsync(url).Forget();
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

                    DeleteFilesInDirectory(timelapseSource, "타임랩스");
                    DeleteFilesInDirectory(realtimeSource, "리얼타임");
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"비동기 소스 폴더 접근 오류: {e.Message}");
            }
        }

        /// <summary>
        /// 지정된 디렉토리 내의 모든 파일을 안전하게 삭제함.
        /// </summary>
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
                    Debug.LogWarning($"{logPrefix} 소스 파일 삭제 실패 ({file}): {ex.Message}");
                }
            }
        }

        #endregion
    }
}