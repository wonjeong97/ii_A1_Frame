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
            
            if (TimeLapseRecorder.Instance)
            {
                Debug.Log("[TitleManager] 소스 이미지 정리");
                TimeLapseRecorder.Instance.ClearRecordingData();
            }

            _pollCoroutine = StartCoroutine(PollRoomStateRoutine());
        }

        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);

            if (settings == null)
            {
                Debug.LogWarning("[TitleManager] Settings.json 로드 실패.");
            }
        }

        private IEnumerator PollRoomStateRoutine()
        {
            while (!_isTransitioning)
            {
                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    yield return CoroutineData.GetWaitForSeconds(pollInterval);
                    continue;
                }

                // 상수 사용으로 모듈 코드 동적 매핑
                string requestUrl = $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code={GameConstants.Module.Code.ToLower()}";

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
                        
                        // "USING" 상수 사용
                        if (!string.IsNullOrEmpty(responseText) && responseText.IndexOf(GameConstants.Api.StatusUsing, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Debug.Log($"[TitleManager] RoomState 'USING' 감지. 튜토리얼로 이동.");
                            GoToTutorial();
                            yield break;
                        }
                    }
                }

                yield return CoroutineData.GetWaitForSeconds(pollInterval);
            }
        }

        private void Update()
        {
            if (_isTransitioning) return; 

            if (Input.GetKeyDown(KeyCode.Return))
            {
                GoToTutorial();
            }
        }

        private void GoToTutorial()
        {
            if (_isTransitioning) return;
            _isTransitioning = true; 

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