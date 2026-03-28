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

        /// <summary>
        /// 전달받은 페이지 데이터를 내부 변수에 캐싱함.
        /// </summary>
        /// <param name="data">초기화할 페이지 데이터</param>
        protected override void SetupData(EndingPage5Data data)
        {
            _data = data;
        }

        /// <summary>
        /// 페이지 진입 시 UI 초기화 및 세션 종료 API 호출을 수행함.
        /// </summary>
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
                // 설정값이 없을 경우 대체(Fallback)하지 않고 경고 로그를 남김
                if (_isAllFinished)
                {
                    if (_data.allFinishedText != null)
                    {
                        if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, _data.allFinishedText);
                    }
                    else
                    {
                        Debug.LogWarning("allFinishedText 누락됨.");
                    }
                }
                else
                {
                    if (_data.descriptionText != null)
                    {
                        if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, _data.descriptionText);
                    }
                    else
                    {
                        Debug.LogWarning("descriptionText 누락됨.");
                    }
                }
            }

            if (!_hasSentEndTime && SessionManager.Instance)
            {
                if (SessionManager.Instance.CurrentUserId == 0)
                {
                    Debug.LogWarning("CurrentUserId 누락으로 통신 보류");
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

        /// <summary>
        /// 엔딩 연출 시퀀스를 실행하고 API 통신 종료를 대기함.
        /// </summary>
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

        /// <summary>
        /// 사용자 세션 종료 및 퇴장 처리를 위한 API를 순차 호출함.
        /// </summary>
        private IEnumerator FinalizeSessionRoutine()
        {
            // 설정 누락 시 널 참조 에러 방지
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

        /// <summary>
        /// 지정된 URL로 API 요청을 보내고 실패 시 재시도 및 로컬 백업을 수행함.
        /// </summary>
        /// <param name="url">요청 URL</param>
        /// <param name="taskName">로깅용 작업명</param>
        private IEnumerator SendWithRetry(string url, string taskName)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 10;
                    yield return req.SendWebRequest();

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log($"{taskName} 성공");
                        yield break;
                    }

                    if (attempt < maxRetries - 1)
                    {
                        Debug.LogWarning($"{taskName} 실패: {req.error}. {retryDelay}초 후 재시도... ({attempt + 1}/{maxRetries})");
                        yield return CoroutineData.GetWaitForSeconds(retryDelay);
                    }
                    else
                    {
                        Debug.LogError($"{taskName} 최종 실패. 로컬 백업 시도.");
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
                string logContent = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [User:{userId}] [Task:{taskName}] [Error:{error}] [URL:{url}]\n";

                File.AppendAllText(filePath, logContent);
                Debug.Log($"로컬 백업 저장 완료: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"로컬 백업 저장 실패: {e.Message}");
            }
        }

        /// <summary>
        /// Image UI의 FillAmount 속성을 시간에 따라 보간하여 시각적 채움 효과를 연출함.
        /// </summary>
        /// <param name="t">대상 Image 컴포넌트</param>
        /// <param name="s">시작 값</param>
        /// <param name="e">종료 값</param>
        /// <param name="d">소요 시간</param>
        private IEnumerator FillImageRoutine(Image t, float s, float e, float d)
        {
            if (!t) yield break;

            float time = 0f;
            t.fillAmount = s;
            while (time < d)
            {
                time += Time.deltaTime;
                // ex: s=0, e=1, time=1, d=2 -> result=0.5
                t.fillAmount = Mathf.Lerp(s, e, time / d);
                yield return null;
            }

            t.fillAmount = e;
        }
    }
}