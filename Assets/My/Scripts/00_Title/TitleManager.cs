using System;
using System.Collections;
using My.Scripts.Global;
using My.Scripts.Timelapse; 
using UnityEngine;
using UnityEngine.Networking; 
using UnityEngine.SceneManagement;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._00_Title
{
    /// <summary>
    /// 타이틀 화면 진입 대기 및 씬 전환을 관리합니다.
    /// 방 상태 API를 지속적으로 확인하여 유저 진입 시 튜토리얼로 자동 전환합니다.
    /// </summary>
    public class TitleManager : MonoBehaviour
    {
        [Header("Polling Settings")]
        [SerializeField] private float basePollInterval = 1.0f; // 기본 API 폴링 간격
        [SerializeField] private float maxPollInterval = 10.0f; // 통신 실패 시 최대 대기 시간 한도

        private float _currentPollInterval; // 현재 적용된 폴링 간격
        private bool _isTransitioning; // 중복 씬 로드 방지
        
        private Coroutine _soundCoroutine;
        private Coroutine _pollCoroutine;

        /// <summary>
        /// 초기 설정, BGM 재생, 찌꺼기 데이터 정리 후 API 폴링을 시작합니다.
        /// </summary>
        private void Start()
        {
            LoadSettings();

            if (_soundCoroutine == null)
            {
                _soundCoroutine = StartCoroutine(StartMainBGM());
            }
            
            // 이전 플레이어의 잔여 영상/이미지 데이터 초기화
            if (TimeLapseRecorder.Instance)
            {
                Debug.Log("[TitleManager] 소스 이미지 정리");
                TimeLapseRecorder.Instance.ClearRecordingData();
            }

            _pollCoroutine = StartCoroutine(PollRoomStateRoutine());
        }

        /// <summary>
        /// 로컬 JSON 설정 파일을 로드합니다.
        /// </summary>
        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);

            if (settings == null)
            {
                Debug.LogWarning("[TitleManager] Settings.json 로드 실패.");
            }
        }

        /// <summary>
        /// 방 상태를 확인하여 새 유저가 들어왔는지 감지합니다.
        /// </summary>
        private IEnumerator PollRoomStateRoutine()
        {
            _currentPollInterval = basePollInterval;

            while (!_isTransitioning)
            {
                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);
                    continue;
                }

                string requestUrl = $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code={GameConstants.Module.Code.ToLower()}";

                using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
                {
                    webRequest.timeout = 10;
                    
                    yield return webRequest.SendWebRequest();

                    if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                        webRequest.result == UnityWebRequest.Result.ProtocolError)
                    {
                        // 통신 실패 시 대기 시간을 2배로 증가시키고 최대치로 제한 (네트워크 부하 방지)
                        _currentPollInterval = Mathf.Min(_currentPollInterval * 2f, maxPollInterval);
                        Debug.LogWarning($"[TitleManager] 상태 체크 통신 실패: {webRequest.error}. 백오프 적용: {_currentPollInterval}초 후 재시도");
                    }
                    else
                    {
                        // 통신 성공 시 폴링 주기를 기본값으로 즉시 복구
                        _currentPollInterval = basePollInterval;

                        string responseText = webRequest.downloadHandler.text;
                        
                        // 서버에서 USING 반환 시 유저 입장으로 간주하고 자동 진행
                        if (!string.IsNullOrEmpty(responseText) && responseText.IndexOf(GameConstants.Api.StatusUsing, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Debug.Log($"[TitleManager] RoomState 'USING' 감지. 튜토리얼로 이동.");
                            GoToTutorial();
                            yield break;
                        }
                    }
                }

                // 동적으로 계산된 간격만큼 대기
                yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);
            }
        }

        /// <summary>
        /// 키보드 입력을 감지합니다.
        /// </summary>
        private void Update()
        {
            if (_isTransitioning) return; 

            // 관리자용 수동 입장 단축키
            if (Input.GetKeyDown(KeyCode.Return))
            {
                GoToTutorial();
            }
        }

        /// <summary>
        /// 튜토리얼 씬으로 전환합니다.
        /// </summary>
        private void GoToTutorial()
        {
            if (_isTransitioning) return;
            _isTransitioning = true; 

            SceneManager.LoadScene(GameConstants.Scene.Tutorial);
        }

        /// <summary>
        /// 타이틀 BGM을 지연 재생합니다.
        /// 이전 씬(엔딩)의 잔여 사운드와 겹치지 않도록 5초 대기합니다.
        /// </summary>
        private IEnumerator StartMainBGM()
        {
            if (!SoundManager.Instance) yield break;

            SoundManager.Instance.StopBGM();
            yield return CoroutineData.GetWaitForSeconds(5.0f);
            SoundManager.Instance.PlayBGM("MainBGM");
        }

        /// <summary>
        /// 오브젝트 파괴 시 백그라운드 코루틴을 정리합니다.
        /// </summary>
        private void OnDestroy()
        {   
            StopAllCoroutines();
            _soundCoroutine = null;
            _pollCoroutine = null;
        }
    }
}