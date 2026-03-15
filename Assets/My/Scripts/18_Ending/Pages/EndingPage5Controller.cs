using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using My.Scripts.Core;
using My.Scripts.Global;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

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

        protected override void SetupData(EndingPage5Data data)
        {
            _data = data;
        }

        public override void OnEnter()
        {
            base.OnEnter();

            SetAlpha(0f);
            _isApiFinalized = false;

            if (redLineImage)
            {
                redLineImage.type = Image.Type.Filled;
                redLineImage.fillAmount = 0f;
            }

            _isAllFinished = false;

            if (GameManager.Instance && SessionManager.Instance)
            {
                _isAllFinished = SessionManager.Instance.IsOtherCartridgeContentsCleared;
            }

            if (_data != null)
            {
                TextSetting textToUse = _isAllFinished && _data.allFinishedText != null
                    ? _data.allFinishedText
                    : _data.descriptionText;

                if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, textToUse);
            }

            if (!_hasSentEndTime && SessionManager.Instance)
            {
                if (SessionManager.Instance.CurrentUserId == 0)
                {
                    Debug.LogWarning("[EndingPage5] CurrentUserId 누락으로 통신 보류");
                    _isApiFinalized = true;
                }
                else
                {
                    StartCoroutine(FinalizeSessionRoutine());
                    _hasSentEndTime = true;
                }
            }
            else
            {
                _isApiFinalized = true;
            }

            StartCoroutine(SequenceRoutine());
        }

        private IEnumerator SequenceRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(1.0f);

            if (_isAllFinished && redLineImage)
            {
                yield return StartCoroutine(FillImageRoutine(redLineImage, 0f, 1f, 2.0f));

                if (SoundManager.Instance) SoundManager.Instance.FadeOutBGM(5.0f);
                yield return CoroutineData.GetWaitForSeconds(5.0f);
            }
            else
            {
                yield return CoroutineData.GetWaitForSeconds(2.0f);

                if (SoundManager.Instance) SoundManager.Instance.FadeOutBGM(5.0f);
                yield return CoroutineData.GetWaitForSeconds(5.0f);
            }

            while (!_isApiFinalized)
            {
                yield return null;
            }

            CompleteStep();
        }

        private IEnumerator FinalizeSessionRoutine()
        {
            if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
            {
                _isApiFinalized = true;
                yield break;
            }

            int userId = SessionManager.Instance.CurrentUserId;
            string code = GameConstants.Module.Code.ToLower();

            string timeUrl = $"{GameManager.Instance.ApiConfig.UpdateTimeUrl}?idx_user={userId}&option=end&code={code}";
            yield return StartCoroutine(SendWithRetry(timeUrl, "종료 시간 기록"));

            string exitUrl = $"{GameManager.Instance.ApiConfig.ExitRoomUrl}?code={code}&idx_user={userId}";
            yield return StartCoroutine(SendWithRetry(exitUrl, "방 퇴장 처리"));

            _isApiFinalized = true;
        }

        private IEnumerator SendWithRetry(string url, string taskName)
        {
            // 전역 변수 사용
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 10;
                    yield return req.SendWebRequest();

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log($"[EndingPage5] {taskName} 성공");
                        yield break;
                    }

                    if (attempt < maxRetries - 1)
                    {
                        Debug.LogWarning(
                            $"[EndingPage5] {taskName} 실패: {req.error}. {retryDelay}초 후 재시도... ({attempt + 1}/{maxRetries})");
                        yield return CoroutineData.GetWaitForSeconds(retryDelay);
                    }
                    else
                    {
                        Debug.LogError($"[EndingPage5] {taskName} 최종 실패. 로컬 백업을 시도합니다.");
                        SaveBackupLocally(taskName, url, req.error);
                    }
                }
            }
        }

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
                Debug.Log($"[EndingPage5] 로컬 백업 저장 완료: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EndingPage5] 로컬 백업 저장 실패: {e.Message}");
            }
        }

        private IEnumerator FillImageRoutine(Image t, float s, float e, float d)
        {
            if (!t) yield break;

            float time = 0f;
            t.fillAmount = s;
            while (time < d)
            {
                time += Time.deltaTime;
                t.fillAmount = Mathf.Lerp(s, e, time / d);
                yield return null;
            }

            t.fillAmount = e;
        }
    }
}