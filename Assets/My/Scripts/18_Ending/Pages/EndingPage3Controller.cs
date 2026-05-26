using System;
using System.IO;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using VContainer;
using Wonjeong.Data;
using ZLogger;

namespace My.Scripts._18_Ending.Pages
{
    [Serializable]
    public class EndingPage3Data
    {
        public TextSetting descriptionText; 
    }

    /// <summary> 
    /// 엔딩 3페이지 컨트롤러.
    /// 녹화된 '리얼타임' 영상을 재생하며 15초 카운트다운을 시각적으로 동기화합니다.
    /// </summary>
    public class EndingPage3Controller : GamePage<EndingPage3Data>
    {
        [Header("UI References")]
        [SerializeField] private RawImage videoDisplay; 
        [SerializeField] private VideoPlayer videoPlayer; 
        [SerializeField] private Text descriptionText; 
        
        private const float FixedDuration = 15f; 
        private CancellationTokenSource _presentationCts;

        private int _lastSeconds = -1;
        private int _lastMilliseconds = -1;

        // --- 의존성 주입 (DI) 변수 ---
        private SessionManager _sessionManager;
        private ILogger<EndingPage3Controller> _logger;

        [Inject]
        public void Construct(SessionManager sessionManager, ILogger<EndingPage3Controller> logger)
        {
            _sessionManager = sessionManager;
            _logger = logger;
        }

        protected override void SetupData(EndingPage3Data data)
        {
            if (descriptionText && data?.descriptionText != null && _uiManager)
            {
                _uiManager.SetText(descriptionText.gameObject, data.descriptionText);
                descriptionText.text = "00:00";
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            _lastSeconds = -1;
            _lastMilliseconds = -1;

            if (videoDisplay) videoDisplay.SetAlpha(0f);
            if (descriptionText) descriptionText.SetAlpha(0f);
            
            _presentationCts?.Cancel();
            _presentationCts?.Dispose();
            _presentationCts = new CancellationTokenSource();

            PresentationAsync(_presentationCts.Token).Forget();
        }

        public override void OnExit()
        {
            _presentationCts?.Cancel();
            _presentationCts?.Dispose();
            _presentationCts = null;

            if (videoPlayer && videoPlayer.isPlaying)
            {
                videoPlayer.Stop();
            }

            base.OnExit();
        }

        /// <summary>
        /// 세부 구현을 헬퍼 메서드로 위임하여, 메인 연출 흐름이 목차처럼 직관적으로 읽히게 구성함.
        /// </summary>
        private async UniTaskVoid PresentationAsync(CancellationToken token)
        {
            try
            {
                // 1. 영상 준비 및 파일 검증
                bool isReady = await TryInitializeVideoAsync(token);
                if (!isReady)
                {
                    CompleteStep();
                    return;
                }

                // 2. 영상 재생 및 등장 연출 시작
                StartVideoPlayback(token);

                // 3. 타이머 연출 대기
                await RunDisplayTimerAsync(token);

                // 4. 영상 종료 및 퇴장 연출
                await FinishPresentationAsync(token);
            }
            catch (OperationCanceledException)
            {
                // 취소 시 에디터 콘솔 에러 방어
            }
        }

        private async UniTask<bool> TryInitializeVideoAsync(CancellationToken token)
        {
            if (!videoPlayer || !videoDisplay) return false;

            string filePath = GetVideoPath();
            if (!File.Exists(filePath))
            {
                _logger?.ZLogError($"[EndingPage3] 영상 누락: {filePath}");
                return false;
            }

            try
            {
                await PrepareVideoAsync(filePath, token);
                return true;
            }
            catch (TimeoutException)
            {
                _logger?.ZLogWarning($"[EndingPage3] 비디오 준비 타임아웃 발생");
                return false;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _logger?.ZLogWarning($"[EndingPage3] 비디오 준비 예외 발생: {0}", e.Message);
                return false;
            }
        }

        private void StartVideoPlayback(CancellationToken token)
        {
            videoPlayer.isLooping = true;
            videoDisplay.texture = videoPlayer.texture;
            videoPlayer.Play();

            if (videoDisplay) videoDisplay.FadeAsync(0f, 1f, 1f, token).Forget();
            if (descriptionText) descriptionText.FadeAsync(0f, 1f, 1f, token).Forget();
        }

        private async UniTask FinishPresentationAsync(CancellationToken token)
        {
            if (videoPlayer && videoPlayer.isPlaying) videoPlayer.Pause();
            
            await UniTask.Delay(1500, ignoreTimeScale: true, cancellationToken: token);

            UniTask f1 = videoDisplay ? videoDisplay.FadeAsync(1f, 0f, 1f, token) : UniTask.CompletedTask;
            UniTask f2 = descriptionText ? descriptionText.FadeAsync(1f, 0f, 1f, token) : UniTask.CompletedTask;
            
            await UniTask.WhenAll(f1, f2);

            CompleteStep();
        }

        private async UniTask PrepareVideoAsync(string path, CancellationToken token)
        {
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = new Uri(path).AbsoluteUri;
            videoPlayer.Prepare();
            
            bool isTimeout = await UniTask.WaitUntil(
                videoPlayer,                                 // 1. 상태(State)로 videoPlayer를 직접 넘겨줌
                vp => vp.isPrepared && vp.texture,        // 2. 외부 변수 대신 매개변수 vp를 받아서 검사함
                PlayerLoopTiming.Update, 
                token
            ).TimeoutWithoutException(TimeSpan.FromSeconds(10));

            if (isTimeout)
            {
                throw new TimeoutException("Video preparation timed out.");
            }
        }

        private async UniTask RunDisplayTimerAsync(CancellationToken token)
        {
            float currentTimer = 0f;
            while (currentTimer < FixedDuration)
            {
                currentTimer += Time.unscaledDeltaTime;
                UpdateTimerUI(Mathf.Min(currentTimer, FixedDuration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            UpdateTimerUI(FixedDuration);
        }

        private void UpdateTimerUI(float time)
        {
            if (!descriptionText) return;

            int seconds = Mathf.FloorToInt(time);
            int milliseconds = Mathf.FloorToInt((time * 100) % 100);

            if (_lastSeconds == seconds && _lastMilliseconds == milliseconds) return;

            _lastSeconds = seconds;
            _lastMilliseconds = milliseconds;

            descriptionText.text = ZString.Format("{0:D2}:{1:D2}", seconds, milliseconds);
        }

        private string GetVideoPath()
        {   
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
            
            string userIdStr = (_sessionManager && _sessionManager.CurrentUserId != 0) 
                ? _sessionManager.CurrentUserId.ToString() 
                : "0";
                
            return Path.Combine(root, "Timelapse", "Realtime_Video", dateFolder, ZString.Format("{0}_Realtime.mp4", userIdStr));
        }

        protected override void OnDestroy()
        {
            _presentationCts?.Cancel();
            _presentationCts?.Dispose();
            _presentationCts = null;

            base.OnDestroy();
        }
    }
}