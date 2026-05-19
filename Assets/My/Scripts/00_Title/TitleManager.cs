using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Text; 
using Microsoft.Extensions.Logging;
using My.Scripts.Global;
using My.Scripts.Hardware;
using My.Scripts.Timelapse;
using UnityEngine;
using UnityEngine.Networking;
using VContainer; 
using Wonjeong.UI;
using ZLogger;

namespace My.Scripts._00_Title
{
    /// <summary>
    /// 타이틀 화면의 하드웨어 초기화 및 서버 상태 폴링을 통합 관리하는 총괄 컨트롤러.
    /// 하드웨어 지연 타임아웃에 영 영향을 받지 않는 독립형(Decoupled) 초고속 서버 모니터링이 구현되었습니다.
    /// </summary>
    public class TitleManager : MonoBehaviour
    {
        private const float PollInterval = 1.0f;
        private bool _isTransitioning;
        private string _cachedRoomStateUrl;

        // --- 의존성 주입 (DI) 변수 ---
        private SessionManager _sessionManager;
        private TimeLapseRecorder _timeLapseRecorder;
        private HueManager _hueManager;
        private ArduinoManager _arduinoManager;
        private GameManager _gameManager;
        private SoundManager _soundManager;
        private ILogger<TitleManager> _logger;

        [Inject]
        public void Construct(
            SessionManager sessionManager,
            TimeLapseRecorder timeLapseRecorder,
            HueManager hueManager,
            ArduinoManager arduinoManager,
            GameManager gameManager,
            SoundManager soundManager,
            ILogger<TitleManager> logger)
        {
            _sessionManager = sessionManager;
            _timeLapseRecorder = timeLapseRecorder;
            _hueManager = hueManager;
            _arduinoManager = arduinoManager;
            _gameManager = gameManager;
            _soundManager = soundManager;
            _logger = logger;
        }

        private void Start()
        {
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            
            InitializeTitleSequence(token);
        }

        private void Update()
        {
            if (_isTransitioning) return;

            if (Input.GetKeyDown(KeyCode.Return))
            {
                GoToTutorial();
            }
        }

        #region Private Pipelines (독립 실행 시퀀스 분할)

        private void InitializeTitleSequence(CancellationToken token)
        {
            // 1. 사운드 트랙 교체
            StartMainBGMAsync(token).Forget();

            // 2. 가비지 없는 로컬 세션 데이터 청소
            ClearLocalSessionData();

            // 3. 하드웨어 리셋은 백그라운드에서 별도로 흐르도록 무차별 비동기(Forget) 처리.
            TurnOffHueLightsAsync(token).Forget();
            ResetArduinoHardwareAsync(token).Forget();

            // 4. 서버 모니터링은 무조건 타이틀 진입 즉시 0.001초 만에 가동하여 입장 관람객에 실시간 반응 보장
            PollRoomStateAsync(token).Forget();
        }

        private void ClearLocalSessionData()
        {
            if (_sessionManager != null)
            {
                _sessionManager.ClearSession();
            }

            if (_timeLapseRecorder != null)
            {
                _timeLapseRecorder.ClearRecordingData();
            }
        }

        private async UniTaskVoid TurnOffHueLightsAsync(CancellationToken token)
        {
            if (_hueManager == null) return;

            try
            {
                await _hueManager.SetLightStateAsync(1, false, token);
                await _hueManager.SetLightStateAsync(2, false, token);
            }
            catch (Exception ex)
            {
                _logger?.ZLogWarning($"[TitleManager] 필립스 휴 소등 중 예외: {ex.Message}");
            }
        }

        private async UniTaskVoid ResetArduinoHardwareAsync(CancellationToken token)
        {
            if (_arduinoManager == null) return;

            try
            {
                await _arduinoManager.ReconnectAllAsync();

                float timeout = 60.0f;
                float timer = 0f;
                while (!_arduinoManager.AreAllConnected && timer < timeout)
                {
                    timer += Time.unscaledDeltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                if (!_arduinoManager.AreAllConnected)
                {
                    _logger?.ZLogWarning($"[TitleManager] 아두이노 기기 통신 복구 실패 (타임아웃).");
                    return;
                }

                await UniTask.Delay(1500, ignoreTimeScale: true, cancellationToken: token);

                _arduinoManager.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOff);
                _arduinoManager.SendCommandToBoth(GameConstants.Hardware.CmdLedShotOff);
                _arduinoManager.SendCommandToLight(GameConstants.Hardware.CmdLightOff);
            }
            catch (Exception ex)
            {
                _logger?.ZLogWarning($"[TitleManager] 아두이노 초기화 중 예외: {ex.Message}");
            }
        }

        private async UniTaskVoid PollRoomStateAsync(CancellationToken token)
        {
#if UNITY_EDITOR
            return;
#endif
            int intervalMs = Mathf.RoundToInt(PollInterval * 1000);

            while (!token.IsCancellationRequested && !_isTransitioning)
            {
                try
                {
                    await ProcessRoomStateCheckAsync(token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.ZLogWarning($"[TitleManager] 서버 통신 순 순간 에러 (루프 유지됨): {ex.Message}");
                }

                if (_isTransitioning) return;

                await UniTask.Delay(intervalMs, ignoreTimeScale: true, cancellationToken: token);
            }
        }

        private async UniTask ProcessRoomStateCheckAsync(CancellationToken token)
        {
            if (_gameManager?.ApiConfig == null) return;

            if (string.IsNullOrEmpty(_cachedRoomStateUrl))
            {
                string baseUrl = _gameManager.ApiConfig.CheckRoomStateUrl;
                string moduleCode = GameConstants.Module.Code.ToLower();
                _cachedRoomStateUrl = ZString.Format("{0}?code={1}", baseUrl, moduleCode);
            }

            using (UnityWebRequest webRequest = UnityWebRequest.Get(_cachedRoomStateUrl))
            {
                webRequest.timeout = 10;
                await webRequest.SendWebRequest().ToUniTask(cancellationToken: token);

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    _logger?.ZLogWarning($"[TitleManager] 상태 체크 실패: {webRequest.error}");
                    return;
                }

                string responseText = webRequest.downloadHandler.text;
                bool isUsing = !string.IsNullOrEmpty(responseText) &&
                               responseText.IndexOf(GameConstants.Api.StatusUsing, StringComparison.OrdinalIgnoreCase) >= 0;

                if (isUsing)
                {   
                    _logger?.ZLogInformation($"[TitleManager] 방 상태 USING 확인.");
                    GoToTutorial();
                }
            }
        }

        private void GoToTutorial()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            SceneLoader.LoadAsync(GameConstants.Scene.Tutorial).Forget();
        }

        private async UniTaskVoid StartMainBGMAsync(CancellationToken token)
        {
            if (_soundManager == null) return;

            _soundManager.StopBGM();
            await UniTask.Delay(1000, ignoreTimeScale: true, cancellationToken: token);

            if (_soundManager != null)
            {
                _soundManager.PlayBGM("MainBGM");
            }
        }

        #endregion
    }
}