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
    /// <summary> 타이틀 화면 입력 처리 및 씬 전환 매니저 </summary>
    public class TitleManager : MonoBehaviour
    {
        private readonly float pollInterval = 1.0f; // 폴링 간격
        private bool _isTransitioning; // 중복 전환 방지 플래그
        
        private Coroutine _soundCoroutine;
        private Coroutine _pollCoroutine; // 폴링 코루틴 추적용

        private void Start()
        {
            LoadSettings();

            if (_soundCoroutine == null)
            {
                _soundCoroutine = StartCoroutine(StartMainBGM());
            }
            
            // 타이틀 진입 시 혹시 남아있을 수 있는 이전 촬영 데이터 정리
            if (TimeLapseRecorder.Instance != null)
            {
                Debug.Log("[TitleManager] 소스 이미지 정리");
                TimeLapseRecorder.Instance.ClearRecordingData();
            }

            // 1초마다 상태를 확인하는 코루틴 시작
            _pollCoroutine = StartCoroutine(PollRoomStateRoutine());
        }

        /// <summary> JSON 설정 파일 로드 </summary>
        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);

            if (settings == null)
            {
                Debug.LogWarning("[TitleManager] Settings.json 로드 실패.");
            }
        }

        /// <summary> 1초마다 API를 호출하여 "USING" 상태를 체크하는 코루틴 </summary>
        private IEnumerator PollRoomStateRoutine()
        {
            // 전환 중이 아닐 때만 계속해서 무한 반복
            while (!_isTransitioning)
            {
                // GameManager에서 API 설정을 가져와 URL을 동적으로 조합합니다.
                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    yield return CoroutineData.GetWaitForSeconds(pollInterval);
                    continue;
                }

                // ApiConfig를 활용하여 URL 조합
                string requestUrl = $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code=a1";

                using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
                {
                    webRequest.timeout = 10;
                    
                    yield return webRequest.SendWebRequest();

                    if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                        webRequest.result == UnityWebRequest.Result.ProtocolError)
                    {
                        Debug.LogWarning($"[TitleManager] 상태 체크 통신 실패: {webRequest.error}");
                    }
                    else
                    {
                        string responseText = webRequest.downloadHandler.text;
                        
                        // "USING" 텍스트가 포함되어 있는지 확인
                        if (!string.IsNullOrEmpty(responseText) && responseText.IndexOf("USING", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Debug.Log($"[TitleManager] RoomState 'USING' 감지. 튜토리얼로 이동.");
                            GoToTutorial();
                            yield break;
                        }
                    }
                }

                // 다음 호출까지 1초 대기
                yield return CoroutineData.GetWaitForSeconds(pollInterval);
            }
        }

        /// <summary> 입력 감지 (태그 시뮬레이션) </summary>
        private void Update()
        {
            if (_isTransitioning) return; // 전환 중이면 입력 무시

            // 테스트용 엔터키 입력
            if (Input.GetKeyDown(KeyCode.Return))
            {
                GoToTutorial();
            }
        }

        /// <summary> 태그 처리 및 튜토리얼 씬 이동 </summary>
        private void GoToTutorial()
        {
            if (_isTransitioning) return;
            _isTransitioning = true; // 중복 호출 방지

            SceneManager.LoadScene(GameConstants.Scene.Tutorial);
        }

        private IEnumerator StartMainBGM()
        {
            if (!SoundManager.Instance) yield break;

            SoundManager.Instance.StopBGM();
            yield return CoroutineData.GetWaitForSeconds(5.0f);
            SoundManager.Instance.PlayBGM("MainBGM");
        }

        private void OnDestroy()
        {   
            StopAllCoroutines();
            _soundCoroutine = null;
            _pollCoroutine = null;
        }
    }
}