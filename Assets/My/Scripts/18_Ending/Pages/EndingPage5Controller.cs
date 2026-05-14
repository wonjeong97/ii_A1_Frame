using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using My.Scripts.Core;
using My.Scripts.Global;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._18_Ending.Pages
{
    [Serializable]
    public class EndingPage5Data
    {
        public TextSetting descriptionText;
        public TextSetting allFinishedText;
    }

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

        /// <summary>
        /// 전달받은 페이지 데이터를 내부 변수에 캐싱함.
        /// </summary>
        /// <param name="data">초기화할 페이지 데이터</param>
        protected override void SetupData(EndingPage5Data data)
        {
            _data = data;
        }

        /// <summary>
        /// 페이지 활성화 시 초기 상태를 구성하고 결과에 따른 텍스트 설정 및 API 종료 절차를 수행함.
        /// 씬 진입과 동시에 비동기 연출 및 데이터 동기화를 병렬로 실행하기 위함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            InitializeInternalState();
            UpdateStatusText();

            CancellationToken token = this.GetCancellationTokenOnDestroy();
            HandleApiFinalization(token);
            SequenceAsync(token).Forget();
        }

        /// <summary>
        /// 변수 및 이미지 컴포넌트의 초기 상태를 리셋함.
        /// </summary>
        private void InitializeInternalState()
        {
            SetAlpha(0f);
            _isApiFinalized = false;

            if (redLineImage)
            {
                redLineImage.type = Image.Type.Filled;
                redLineImage.fillAmount = 0f;
            }

            // 타 카트리지 완료 여부를 확인하여 엔딩 분기를 결정함.
            if (GameManager.Instance && SessionManager.Instance)
            {
                _isAllFinished = SessionManager.Instance.IsOtherCartridgeContentsCleared;
            }
        }

        /// <summary>
        /// 클리어 상태에 맞는 텍스트 데이터를 UI에 적용함.
        /// </summary>
        private void UpdateStatusText()
        {
            if (_data == null)
            {
                return;
            }

            // 상태에 따라 참조할 데이터와 로그 메시지를 선택함.
            TextSetting targetSetting = _isAllFinished ? _data.allFinishedText : _data.descriptionText;
            string missingErrorMsg = _isAllFinished ? "allFinishedText 누락됨." : "descriptionText 누락됨.";

            if (targetSetting != null)
            {
                if (descriptionText)
                {
                    UIManager.Instance.SetText(descriptionText.gameObject, targetSetting);
                }
            }
            else
            {
                Debug.LogWarning(missingErrorMsg);
            }
        }

        /// <summary>
        /// 세션 종료 API 호출 여부를 판단하고 실행함.
        /// 중복 전송을 방지하고 유효한 사용자일 경우에만 종료 시퀀스를 트리거하기 위함.
        /// </summary>
        private void HandleApiFinalization(CancellationToken token)
        {
            if (_hasSentEndTime || !SessionManager.Instance)
            {
                _isApiFinalized = true;
                return;
            }

            if (SessionManager.Instance.CurrentUserId == 0)
            {
                Debug.LogWarning("EndingPage5: 사용자 ID 누락으로 통신을 스킵함.");
                _isApiFinalized = true;
            }
            else
            {
                // 기존의 코루틴 호출 대신 UniTask 메서드 호출로 변경함
                FinalizeSessionAsync(token).Forget();
                _hasSentEndTime = true;
            }
        }

        /// <summary>
        /// 엔딩 연출 시퀀스를 실행하고 API 통신 종료를 대기함.
        /// 시각적 연출과 백그라운드 통신 작업의 완료 시점을 동기화하기 위함.
        /// </summary>
        private async UniTaskVoid SequenceAsync(CancellationToken token)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(1.0), cancellationToken: token);

            if (_isAllFinished && redLineImage)
            {
                await FillImageAsync(redLineImage, 0f, 1f, 2.0f, token);

                if (SoundManager.Instance)
                {
                    SoundManager.Instance.FadeOutBGM(5.0f);
                }

                await UniTask.Delay(TimeSpan.FromSeconds(5.0), cancellationToken: token);
            }
            else
            {
                await UniTask.Delay(TimeSpan.FromSeconds(2.0), cancellationToken: token);

                if (SoundManager.Instance)
                {
                    SoundManager.Instance.FadeOutBGM(5.0f);
                }

                await UniTask.Delay(TimeSpan.FromSeconds(5.0), cancellationToken: token);
            }

            // 통신 작업이 완료될 때까지 연출 종료를 지연시킴
            await UniTask.WaitUntil(this, controller => controller._isApiFinalized, PlayerLoopTiming.Update, token);

            CompleteStep();
        }

        /// <summary>
        /// 사용자 세션 종료 및 퇴장 처리를 위한 API를 순차 호출함.
        /// </summary>
        private async UniTaskVoid FinalizeSessionAsync(CancellationToken token)
        {
            if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
            {
                _isApiFinalized = true;
                return;
            }

            int userId = SessionManager.Instance.CurrentUserId;
            string code = GameConstants.Module.Code.ToLower();

            string timeUrl = string.Format("{0}?idx_user={1}&option=end&code={2}",
                GameManager.Instance.ApiConfig.UpdateTimeUrl, userId, code);
            await SendWithRetryAsync(timeUrl, "종료 시간 기록", token);

            string exitUrl = string.Format("{0}?code={1}&idx_user={2}",
                GameManager.Instance.ApiConfig.ExitRoomUrl, code, userId);
            await SendWithRetryAsync(exitUrl, "방 퇴장 처리", token);

            _isApiFinalized = true;
        }

        /// <summary>
        /// 지정된 URL로 API 요청을 보내고 실패 시 재시도함.
        /// 네트워크 불안정 상황에서도 데이터 누락을 최소화하고 최종 실패 시 로컬에 기록하기 위함.
        /// </summary>
        private async UniTask SendWithRetryAsync(string url, string taskName, CancellationToken token)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 10;
                    await req.SendWebRequest().ToUniTask(cancellationToken: token);

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log(string.Format("{0} 성공", taskName));
                        return;
                    }

                    if (attempt < maxRetries - 1)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(retryDelay), cancellationToken: token);
                    }
                    else
                    {
                        // 최종 실패 시에만 로그 파일 덤프를 수행함
                        SaveBackupLocally(taskName, url, req.error);
                    }
                }
            }
        }

        /// <summary>
        /// API 최종 실패 시 데이터 누락 방지를 위해 로컬 디스크에 텍스트 로그를 저장함.
        /// </summary>
        /// <param name="taskName">실패한 작업명</param>
        /// <param name="url">실패한 요청 URL</param>
        /// <param name="error">에러 메시지</param>
        private void SaveBackupLocally(string taskName, string url, string error)
        {
            try
            {
                string userId = SessionManager.Instance ? SessionManager.Instance.CurrentUserId.ToString() : "Unknown";
                string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");

                string dataPath = Application.dataPath;
                DirectoryInfo parentDir = Directory.GetParent(dataPath);
                string rootPath = parentDir != null ? parentDir.FullName : dataPath;

                string directoryPath = Path.Combine(rootPath, "Backups", dateFolder);
                if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

                string filePath = Path.Combine(directoryPath, "api_backup_logs.txt");
                string logContent =
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [User:{userId}] [Task:{taskName}] [Error:{error}] [URL:{url}]\n";

                File.AppendAllText(filePath, logContent);
                Debug.Log($"로컬 백업 저장 완료: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"로컬 백업 저장 실패: {e.Message}");
            }
        }

        /// <summary>
        /// Image UI의 FillAmount 속성을 선형 보간함.
        /// </summary>
        private async UniTask FillImageAsync(Image img, float start, float end, float duration, CancellationToken token)
        {
            if (!img) return;

            float time = 0f;
            img.fillAmount = start;
            while (time < duration)
            {
                time += Time.deltaTime;
                img.fillAmount = Mathf.Lerp(start, end, time / duration);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            img.fillAmount = end;
        }
    }
}