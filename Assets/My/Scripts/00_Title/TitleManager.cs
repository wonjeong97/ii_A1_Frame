using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using My.Scripts.Global;
using My.Scripts.Hardware;
using My.Scripts.Timelapse;
using UnityEngine;
using UnityEngine.Networking;
using Wonjeong.UI;

namespace My.Scripts._00_Title
{
    /// <summary>
    /// 타이틀 화면의 하드웨어 초기화 및 서버 상태 폴링을 관리함.
    /// </summary>
    public class TitleManager : MonoBehaviour
    {
        [Header("Polling Settings")]
        [SerializeField] private float pollInterval = 3.0f;

        private bool _isTransitioning;
        private string _cachedRoomStateUrl;

        private void Start()
        {
            CancellationToken token = this.GetCancellationTokenOnDestroy();

            StartMainBGMAsync(token).Forget();

            if (SessionManager.Instance)
            {
                SessionManager.Instance.ClearSession();
            }

            if (TimeLapseRecorder.Instance)
            {
                TimeLapseRecorder.Instance.ClearRecordingData();
            }

            TurnOffArduinoLedsAsync(token).Forget();
            TurnOffHueLightsAsync(token).Forget();

            PollRoomStateAsync(token).Forget();
        }
        
        /// <summary>
        /// 전시 시작 전 조명을 초기 상태(Off)로 전환함.
        /// </summary>
        private async UniTaskVoid TurnOffHueLightsAsync(CancellationToken token)
        {
            float timeout = 5.0f;
            float timer = 0f;

            while (!HueManager.Instance && timer < timeout)
            {
                timer += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            if (HueManager.Instance)
            {
                HueManager.Instance.SetLightStateAsync(1, false, token).Forget();
                HueManager.Instance.SetLightStateAsync(2, false, token).Forget();
            }
            else
            {
                Debug.LogWarning("TitleManager: 조명 컨트롤러 연결 불가.");
            }
        }

        /// <summary>
        /// 아두이노 하드웨어 재연결 및 LED 상태를 초기화함.
        /// </summary>
        private async UniTaskVoid TurnOffArduinoLedsAsync(CancellationToken token)
        {
            float timeout = 60.0f;
            float timer = 0f;

            while (!ArduinoManager.Instance && timer < timeout)
            {
                timer += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            if (!ArduinoManager.Instance)
            {
                Debug.LogWarning("TitleManager: ArduinoManager 인스턴스 누락.");
                return;
            }

            await ArduinoManager.Instance.ReconnectAllAsync();

            timer = 0f;
            while (!ArduinoManager.Instance.AreAllConnected && timer < timeout)
            {
                timer += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            if (!ArduinoManager.Instance.AreAllConnected)
            {
                Debug.LogWarning("TitleManager: 하드웨어 통신 복구 실패.");
                return;
            }

            await UniTask.Delay(TimeSpan.FromSeconds(1.5), cancellationToken: token);

            ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOff);
            ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedShotOff);
            ArduinoManager.Instance.SendCommandToLight(GameConstants.Hardware.CmdLightOff);
        }

        /// <summary>
        /// 서버 지속 확인 루프를 제어함.
        /// </summary>
        private async UniTaskVoid PollRoomStateAsync(CancellationToken token)
        {
#if UNITY_EDITOR
            return;
#endif
            while (!token.IsCancellationRequested && !_isTransitioning)
            {
                await ProcessRoomStateCheckAsync(token);

                if (_isTransitioning)
                {
                    return;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(pollInterval), cancellationToken: token);
            }
        }

        /// <summary>
        /// 서버 API를 호출하여 현재 전시실의 사용 가능 상태를 확인함.
        /// </summary>
        private async UniTask ProcessRoomStateCheckAsync(CancellationToken token)
        {
            if (!GameManager.Instance)
            {
                Debug.LogWarning("TitleManager: GameManager 인스턴스 누락.");
                return;
            }

            if (GameManager.Instance.ApiConfig == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(_cachedRoomStateUrl))
            {
                string baseUrl = GameManager.Instance.ApiConfig.CheckRoomStateUrl;
                string moduleCode = GameConstants.Module.Code.ToLower();
                _cachedRoomStateUrl = string.Format("{0}?code={1}", baseUrl, moduleCode);
            }

            using (UnityWebRequest webRequest = UnityWebRequest.Get(_cachedRoomStateUrl))
            {
                webRequest.timeout = 10;
                await webRequest.SendWebRequest().ToUniTask(cancellationToken: token);

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning(string.Format("TitleManager: 상태 체크 실패 - {0}", webRequest.error));
                    return;
                }

                string responseText = webRequest.downloadHandler.text;
                bool isUsing = !string.IsNullOrEmpty(responseText) &&
                               responseText.IndexOf(GameConstants.Api.StatusUsing, StringComparison.OrdinalIgnoreCase) >= 0;

                if (isUsing)
                {
                    GoToTutorial();
                }
            }
        }

        private void Update()
        {
            if (_isTransitioning) return;

            // 장치 장애 상황 등을 대비한 수동 강제 진입 루트를 제공함.
            if (Input.GetKeyDown(KeyCode.Return))
            {
                GoToTutorial();
            }
        }

        /// <summary>
        /// 씬 전환 시 중복 호출을 방지하며 튜토리얼 씬으로 이동함.
        /// </summary>
        private void GoToTutorial()
        {
            if (_isTransitioning) return;

            _isTransitioning = true;
            SceneLoader.LoadAsync(GameConstants.Scene.Tutorial).Forget();
        }

        /// <summary>
        /// 사운드 매니저 초기화 및 배경음을 재생함.
        /// </summary>
        private async UniTaskVoid StartMainBGMAsync(CancellationToken token)
        {
            if (!SoundManager.Instance)
            {
                return;
            }

            SoundManager.Instance.StopBGM();
            await UniTask.Delay(TimeSpan.FromSeconds(1.0), cancellationToken: token);

            SoundManager.Instance.PlayBGM("MainBGM");
        }
    }
}