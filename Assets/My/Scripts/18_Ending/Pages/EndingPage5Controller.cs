using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Text; 
using Microsoft.Extensions.Logging; 
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Utils;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;
using ZLogger;

namespace My.Scripts._18_Ending.Pages
{
    [Serializable]
    public class EndingPage5Data
    {
        public TextSetting descriptionText;
        public TextSetting allFinishedText;
    }

    /// <summary>
    /// 엔딩의 최종 시퀀스 및 서버/로컬 통신 종료를 책임지는 컨트롤러.
    /// 디스크 I/O 스레드 분리 작업이 적용되어, 통신 실패 시에도 프레임 드랍(렉)이 발생하지 않습니다.
    /// </summary>
    public class EndingPage5Controller : GamePage<EndingPage5Data>
    {
        [Header("UI References")]
        [SerializeField] private Text descriptionText;
        [SerializeField] private Image redLineImage;

        [Header("API Retry Settings")]
        [SerializeField] private int maxRetries = 10;
        [SerializeField] private float retryDelay = 1.0f;

        private bool _isAllFinished;
        private bool _hasSentEndTime;
        private bool _isApiFinalized;
        private EndingPage5Data _data;
        private CancellationTokenSource _pageCts;

        // --- 의존성 주입 (DI) 변수 ---
        private GameManager _gameManager;
        private SessionManager _sessionManager;
        private SoundManager _soundManager;
        private ILogger<EndingPage5Controller> _logger;

        [Inject]
        public void ConstructEnding5(
            GameManager gameManager, 
            SessionManager sessionManager, 
            SoundManager soundManager, 
            ILogger<EndingPage5Controller> logger)
        {
            _gameManager = gameManager;
            _sessionManager = sessionManager;
            _soundManager = soundManager;
            _logger = logger;
        }

        protected override void SetupData(EndingPage5Data data)
        {
            _data = data;
        }

        public override void OnEnter()
        {
            base.OnEnter();

            _pageCts?.Cancel();
            _pageCts?.Dispose();
            _pageCts = new CancellationTokenSource();

            InitializeInternalState();
            UpdateStatusText();

            HandleApiFinalization(_pageCts.Token);
            SequenceAsync(_pageCts.Token).Forget();
        }

        public override void OnExit()
        {
            _pageCts?.Cancel();
            _pageCts?.Dispose();
            _pageCts = null;

            base.OnExit();
        }

        private void InitializeInternalState()
        {
            SetAlpha(0f);
            _isApiFinalized = false;

            if (redLineImage)
            {
                redLineImage.type = Image.Type.Filled;
                redLineImage.fillAmount = 0f;
            }

            if (_sessionManager)
            {
                _isAllFinished = _sessionManager.IsOtherCartridgeContentsCleared;
            }
        }

        private void UpdateStatusText()
        {
            if (_data == null) return;

            TextSetting targetSetting = _isAllFinished ? _data.allFinishedText : _data.descriptionText;
            
            if (targetSetting != null && descriptionText && _uiManager)
            {
                _uiManager.SetText(descriptionText.gameObject, targetSetting);
            }
            else if (targetSetting == null)
            {
                string missingErrorMsg = _isAllFinished ? "allFinishedText 누락됨." : "descriptionText 누락됨.";
                _logger?.ZLogWarning($"[EndingPage5] {missingErrorMsg}");
            }
        }

        private void HandleApiFinalization(CancellationToken token)
        {
            if (_hasSentEndTime || !_sessionManager)
            {
                _isApiFinalized = true;
                return;
            }

            if (_sessionManager.CurrentUserId == 0)
            {
                _logger?.ZLogWarning($"[EndingPage5] 사용자 ID 누락으로 통신을 스킵함.");
                _isApiFinalized = true;
            }
            else
            {
                FinalizeSessionAsync(token).Forget();
                _hasSentEndTime = true;
            }
        }

        private async UniTaskVoid SequenceAsync(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(1000, ignoreTimeScale: true, cancellationToken: token);

                if (_isAllFinished && redLineImage)
                {
                    await FillImageAsync(redLineImage, 0f, 1f, 2.0f, token);

                    if (_soundManager) _soundManager.FadeOutBGM(5.0f);
                    await UniTask.Delay(5000, ignoreTimeScale: true, cancellationToken: token);
                }
                else
                {
                    await UniTask.Delay(2000, ignoreTimeScale: true, cancellationToken: token);

                    if (_soundManager) _soundManager.FadeOutBGM(5.0f);
                    await UniTask.Delay(5000, ignoreTimeScale: true, cancellationToken: token);
                }

                while (!_isApiFinalized)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                CompleteStep();
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTaskVoid FinalizeSessionAsync(CancellationToken token)
        {
            try
            {   
#if UNITY_EDITOR
                // 에디터 환경에서는 통신을 시도하지 않고 즉시 완료 처리합니다.
                _logger?.ZLogInformation($"[EndingPage5] 유니티 에디터 환경: 종료 시간 기록 및 퇴장 API 통신을 스킵합니다.");
                return;
#endif
                if (!_gameManager || _gameManager.ApiConfig == null) return;

                int userId = _sessionManager.CurrentUserId;
                string code = GameConstants.Module.Code.ToLower();

                string timeUrl = ZString.Format("{0}?idx_user={1}&option=end&code={2}", _gameManager.ApiConfig.UpdateTimeUrl, userId, code);
                await SendWithRetryAsync(timeUrl, "종료 시간 기록", token);

                string exitUrl = ZString.Format("{0}?code={1}&idx_user={2}", _gameManager.ApiConfig.ExitRoomUrl, code, userId);
                await SendWithRetryAsync(exitUrl, "방 퇴장 처리", token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _isApiFinalized = true;
            }
        }

        private async UniTask SendWithRetryAsync(string url, string taskName, CancellationToken token)
        {
            int delayMs = Mathf.RoundToInt(retryDelay * 1000);

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    using (UnityWebRequest req = UnityWebRequest.Get(url))
                    {
                        req.timeout = 10;
                        
                        await req.SendWebRequest().ToUniTask(cancellationToken: token);

                        if (req.result == UnityWebRequest.Result.Success)
                        {
                            _logger?.ZLogInformation($"[EndingPage5] {taskName} 성공 (시도: {attempt + 1}회)");
                            return; 
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw; 
                }
                catch (Exception e)
                {
                    _logger?.ZLogWarning($"[EndingPage5] {taskName} 통신 실패 (시도 {attempt + 1}/{maxRetries}) : {e.Message}");
                }

                if (attempt < maxRetries - 1)
                {
                    await UniTask.Delay(delayMs, ignoreTimeScale: true, cancellationToken: token);
                }
                else
                {
                    // 백그라운드 스레드에서 파일 I/O를 처리하도록 Forget() 비동기 호출
                    SaveBackupLocallyAsync(taskName, url, "최대 재시도 횟수 초과 및 통신 실패").Forget();
                }
            }
        }

        /// <summary>
        /// 디스크 I/O로 인한 메인 스레드 렌더링 마비(프리징)를 방지하기 위해, 
        /// 유니티 API 캐싱 후 백그라운드 스레드 풀(ThreadPool)로 넘어가서 파일을 씁니다.
        /// </summary>
        private async UniTaskVoid SaveBackupLocallyAsync(string taskName, string url, string error)
        {
            try
            {
                // 1. 메인 스레드 전용 API 안전 캐싱 (Application.dataPath 등은 백그라운드 스레드에서 접근 불가)
                string userId = _sessionManager ? _sessionManager.CurrentUserId.ToString() : "Unknown";
                string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
                string dataPath = Application.dataPath;
                
                string logContent = ZString.Format("[{0:yyyy-MM-dd HH:mm:ss}] [User:{1}] [Task:{2}] [Error:{3}] [URL:{4}]\n",
                    DateTime.Now, userId, taskName, error, url);

                // 2. 무거운 디스크 쓰기 작업을 백그라운드 스레드로 이전 (프레임 드랍 원천 차단)
                await UniTask.SwitchToThreadPool();

                DirectoryInfo parentDir = Directory.GetParent(dataPath);
                string rootPath = parentDir != null ? parentDir.FullName : dataPath;

                string directoryPath = Path.Combine(rootPath, "Backups", dateFolder);
                if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

                string filePath = Path.Combine(directoryPath, "api_backup_logs.txt");
                File.AppendAllText(filePath, logContent);

                // 3. 다시 메인 스레드로 돌아와 안전하게 로그 출력
                await UniTask.SwitchToMainThread();
                _logger?.ZLogInformation($"[EndingPage5] 로컬 백업 저장 완료: {filePath}");
            }
            catch (Exception e)
            {
                await UniTask.SwitchToMainThread();
                _logger?.ZLogError($"[EndingPage5] 로컬 백업 저장 실패: {e.Message}");
            }
        }

        private async UniTask FillImageAsync(Image img, float start, float end, float duration, CancellationToken token)
        {
            if (!img) return;

            float time = 0f;
            img.fillAmount = start;
            
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                img.fillAmount = Mathf.Lerp(start, end, time / duration);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            img.fillAmount = end;
        }

        protected override void OnDestroy()
        {
            _pageCts?.Cancel();
            _pageCts?.Dispose();
            _pageCts = null;

            base.OnDestroy();
        }
    }
}