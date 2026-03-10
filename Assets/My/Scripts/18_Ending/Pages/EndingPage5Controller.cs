using System;
using System.Collections;
using System.IO; // 로컬 백업(파일 쓰기)을 위해 추가
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
    /// <summary> 엔딩 5페이지용 데이터 구조체 </summary>
    [Serializable]
    public class EndingPage5Data
    {
        public TextSetting descriptionText; // 일반 엔딩 텍스트
        public TextSetting allFinishedText; // 특별 엔딩 텍스트
    }

    /// <summary>
    /// 엔딩 씬의 마지막 페이지 컨트롤러.
    /// 카트리지 내 모든 콘텐츠의 클리어 여부를 판단하여 일반/진엔딩 분기를 처리하고 세션을 종료합니다.
    /// </summary>
    public class EndingPage5Controller : GamePage<EndingPage5Data>
    {
        [Header("UI References")] 
        [SerializeField] private Text descriptionText;
        [SerializeField] private Image redLineImage;

        [Header("API Retry Settings")]
        [SerializeField] private int maxRetries = 3; // 통신 실패 시 최대 재시도 횟수
        [SerializeField] private float baseRetryDelay = 2.0f; // 재시도 간 기본 대기 시간

        private bool _isAllFinished;
        private bool _hasSentEndTime;
        private bool _isApiFinalized; // 필수 API 처리(재시도 포함) 완료 여부
        
        private EndingPage5Data _data;

        /// <summary> JSON 설정 데이터 캐싱 </summary>
        protected override void SetupData(EndingPage5Data data)
        {
            _data = data;
        }

        /// <summary> 
        /// 페이지 진입 시 최종 클리어 상태를 평가하고 UI를 초기화합니다.
        /// 지수 백오프 재시도 로직이 포함된 세션 종료 프로세스를 시작합니다.
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

            // 카트리지 내 다른 콘텐츠 완수 여부 확인
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

            // 세션 종료 API 호출 시퀀스 시작
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

        /// <summary> 
        /// 연출 시퀀스를 가동하며, 연출이 종료되더라도 통신 재시도가 진행 중이라면 완료를 대기합니다.
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
            
            // 데이터 누락 방지를 위해 모든 통신 시도가 끝날 때까지 대기
            while (!_isApiFinalized)
            {
                yield return null;
            }
            
            CompleteStep();
        }

        /// <summary> 종료 시간 기록 및 방 퇴장 처리를 순차적으로 실행하며 실패 시 지수 백오프로 재시도합니다. </summary>
        private IEnumerator FinalizeSessionRoutine()
        {
            if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
            {
                _isApiFinalized = true;
                yield break;
            }

            int userId = SessionManager.Instance.CurrentUserId;
            string code = GameConstants.Module.Code.ToLower();

            // 1. 종료 시간 업데이트 시도
            string timeUrl = $"{GameManager.Instance.ApiConfig.UpdateTimeUrl}?idx_user={userId}&option=end&code={code}";
            yield return StartCoroutine(SendWithRetry(timeUrl, "종료 시간 기록"));

            // 2. 방 퇴장 업데이트 시도
            string exitUrl = $"{GameManager.Instance.ApiConfig.ExitRoomUrl}?code={code}&idx_user={userId}";
            yield return StartCoroutine(SendWithRetry(exitUrl, "방 퇴장 처리"));

            _isApiFinalized = true;
        }

        /// <summary>
        /// 특정 API를 지정된 횟수만큼 재시도하며 전송합니다. 
        /// 모든 재시도가 실패하면 로컬 텍스트 파일로 데이터를 백업합니다.
        /// </summary>
        private IEnumerator SendWithRetry(string url, string taskName)
        {
            int attempt = 0;
            float currentDelay = baseRetryDelay;

            while (attempt < maxRetries)
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

                    attempt++;
                    if (attempt < maxRetries)
                    {
                        Debug.LogWarning($"[EndingPage5] {taskName} 실패: {req.error}. {currentDelay}초 후 재시도... ({attempt}/{maxRetries})");
                        yield return CoroutineData.GetWaitForSeconds(currentDelay);
                        currentDelay *= 2f; // 지수 백오프 적용
                    }
                    else
                    {
                        // 서버 통신 최종 실패 시 로컬 파일 시스템에 백업 로그 생성
                        Debug.LogError($"[EndingPage5] {taskName} 최종 실패. 로컬 백업을 시도합니다.");
                        SaveBackupLocally(taskName, url, req.error);
                    }
                }
            }
        }

        /// <summary> 
        /// 통신 실패 데이터를 로컬 'Backups' 폴더에 날짜별로 저장하여 사후 관리를 지원합니다. 
        /// </summary>
        private void SaveBackupLocally(string taskName, string url, string error)
        {
            try
            {
                string userId = SessionManager.Instance ? SessionManager.Instance.CurrentUserId.ToString() : "Unknown";
                string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
                
                // 실행 기기의 루트 경로 획득
                string dataPath = Application.dataPath;
                DirectoryInfo parentDir = Directory.GetParent(dataPath);
                string rootPath = parentDir != null ? parentDir.FullName : dataPath;
                
                string directoryPath = Path.Combine(rootPath, "Backups", dateFolder);
                if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

                string filePath = Path.Combine(directoryPath, "api_backup_logs.txt");
                string logContent = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [User:{userId}] [Task:{taskName}] [Error:{error}] [URL:{url}]\n";

                // 기존 파일이 있으면 하단에 이어 쓰기
                File.AppendAllText(filePath, logContent);
                Debug.Log($"[EndingPage5] 로컬 백업 저장 완료: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EndingPage5] 로컬 백업 저장 실패: {e.Message}");
            }
        }

        /// <summary> 이미지 Fill 연출 코루틴 </summary>
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