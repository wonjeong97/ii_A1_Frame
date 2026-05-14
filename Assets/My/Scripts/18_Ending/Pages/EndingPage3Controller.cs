using System;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._18_Ending.Pages
{
    /// <summary> 엔딩 3페이지용 데이터 구조체 </summary>
    [Serializable]
    public class EndingPage3Data
    {
        public TextSetting descriptionText; 
    }

    /// <summary> 
    /// 엔딩 3페이지 컨트롤러.
    /// 녹화된 '리얼타임' 영상을 재생하며 15초 카운트다운을 시각적으로 동기화합니다.
    /// 영상이 15초보다 짧을 경우 반복 재생하여 연출 시간을 유지합니다.
    /// </summary>
   public class EndingPage3Controller : GamePage<EndingPage3Data>
    {
        [Header("UI References")]
        [SerializeField] private RawImage videoDisplay; 
        [SerializeField] private VideoPlayer videoPlayer; 
        [SerializeField] private Text descriptionText; 
        
        private const float FixedDuration = 15f; 
        private readonly static StringBuilder TimerBuilder = new StringBuilder(16);
        private CancellationTokenSource _presentationCts;

        protected override void SetupData(EndingPage3Data data)
        {
            if (descriptionText && data.descriptionText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
                descriptionText.text = "00:00";
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            UIFadeUtility.SetAlpha(videoDisplay, 0f);
            
            if (descriptionText) 
            {
                Color c = descriptionText.color;
                c.a = 0f;
                descriptionText.color = c;
            }
            
            _presentationCts?.Cancel();
            _presentationCts?.Dispose();
            _presentationCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            PresentationAsync(_presentationCts.Token).Forget();
        }

        public override void OnExit()
        {
            _presentationCts?.Cancel();
            _presentationCts?.Dispose();
            _presentationCts = null;
            base.OnExit();
        }

        /// <summary>
        /// 영상 준비부터 재생, 타이머 업데이트 및 퇴장 연출까지의 전체 시퀀스를 제어함.
        /// </summary>
        private async UniTaskVoid PresentationAsync(CancellationToken token)
        {
            if (!videoPlayer || !videoDisplay)
            {
                CompleteStep();
                return;
            }

            string filePath = GetVideoPath();
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[EndingPage3] 영상 누락: {filePath}");
                CompleteStep();
                return;
            }

            await PrepareVideoAsync(filePath, token);

            // 클로저 할당 방지: videoPlayer를 상태 매개변수로 전달함
            await UniTask.WaitUntil(videoPlayer, v => v.isPrepared && v.texture != null, PlayerLoopTiming.Update, token);

            videoPlayer.isLooping = true;
            videoDisplay.texture = videoPlayer.texture;
            videoPlayer.Play();

            // 등장 연출
            UIFadeUtility.FadeGraphicAsync(videoDisplay, 0f, 1f, 1f, token).Forget();
            UIFadeUtility.FadeGraphicAsync(descriptionText, 0f, 1f, 1f, token).Forget();

            await RunDisplayTimerAsync(token);

            if (videoPlayer.isPlaying) videoPlayer.Pause();
            await UniTask.Delay(TimeSpan.FromSeconds(1.5), cancellationToken: token);

            // 퇴장 연출
            var f1 = UIFadeUtility.FadeGraphicAsync(videoDisplay, 1f, 0f, 1f, token);
            var f2 = UIFadeUtility.FadeGraphicAsync(descriptionText, 1f, 0f, 1f, token);
            await UniTask.WhenAll(f1, f2);

            CompleteStep();
        }

        private async UniTask PrepareVideoAsync(string path, CancellationToken token)
        {
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = new Uri(path).AbsoluteUri;
            videoPlayer.Prepare();
            
            // 타임아웃 10초 적용
            await UniTask.WaitUntil(videoPlayer, v => v.isPrepared, PlayerLoopTiming.Update, token).Timeout(TimeSpan.FromSeconds(10));
        }

        private async UniTask RunDisplayTimerAsync(CancellationToken token)
        {
            float currentTimer = 0f;
            while (currentTimer < FixedDuration)
            {
                currentTimer += Time.deltaTime;
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

            TimerBuilder.Clear();
            TimerBuilder.Append(seconds.ToString("D2")).Append(":").Append(milliseconds.ToString("D2"));
            descriptionText.text = TimerBuilder.ToString();
        }

        private string GetVideoPath()
        {   
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
            string userIdStr = (SessionManager.Instance) ? SessionManager.Instance.CurrentUserId.ToString() : "0";
            return Path.Combine(root, "Timelapse", "Realtime_Video", dateFolder, $"{userIdStr}_Realtime.mp4");
        }
    }
}