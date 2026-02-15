using System;
using System.Collections;
using My.Scripts.Global;
using My.Scripts.Timelapse; // TimeLapseRecorder 사용을 위해 추가
using UnityEngine;
using UnityEngine.SceneManagement;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._00_Title
{
    /// <summary> 타이틀 화면 입력 처리 및 씬 전환 매니저 </summary>
    public class TitleManager : MonoBehaviour
    {
        private bool _isTransitioning = false; // 중복 전환 방지 플래그
        private float _fadeTime = 1.0f; // 페이드 시간 (설정값)
        
        private Coroutine _soundCoroutine;

        private void Start()
        {
            LoadSettings();

            if (_soundCoroutine == null)
            {
                _soundCoroutine = StartCoroutine(StartMainBGM());
            }
            
            // 타이틀 진입 시 혹시 남아있을 수 있는 이전 촬영 데이터(소스 이미지) 정리
            if (TimeLapseRecorder.Instance != null)
            {
                Debug.Log("[TitleManager] 이전 세션 데이터 정리 (ClearRecordingData)");
                TimeLapseRecorder.Instance.ClearRecordingData();
            }
        }

        /// <summary> JSON 설정 파일 로드 </summary>
        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);

            if (settings == null)
            {
                Debug.LogWarning("[TitleManager] Settings.json 로드 실패.");
                return;
            }

            _fadeTime = settings.fadeTime; // 설정값 적용
        }

        /// <summary> 입력 감지 (태그 시뮬레이션) </summary>
        private void Update()
        {
            if (_isTransitioning) return; // 전환 중이면 입력 무시

            // 플레이어 1 태그 (키보드 1번)
            if (Input.GetKeyDown(KeyCode.Return))
            {
                ProcessTag(1);
            }
            // // 플레이어 2 태그 (키보드 2번)
            // else if (Input.GetKeyDown(KeyCode.Return))
            // {
            //     ProcessTag(2);
            // }
        }

        /// <summary> 태그 처리 및 튜토리얼 씬 이동 </summary>
        private void ProcessTag(int playerID)
        {
            if (_isTransitioning) return;
            _isTransitioning = true; // 중복 호출 방지

            SceneManager.LoadScene(GameConstants.Scene.Tutorial);
        }

        public void OnClickTypeButton(string typeStr)
        {
            if (_isTransitioning) return;
            if (GameManager.Instance == null)
            {
                Debug.LogError("[TitleManager] GameManager.Instance is null. Cannot set UserType.");
                return;
            }

            // 버튼의 OnClick 이벤트에 연결 (인자로 "A", "B" 등 전달)
            // Enum 파싱 실패 시 피드백 제공
            if (Enum.TryParse(typeStr, out UserType selectedType))
            {
                _isTransitioning = true; // 전환 시작

                GameManager.Instance.currentUserType = selectedType;
                Debug.Log($"유저 타입 설정됨: {selectedType}");

                // 타입 설정 후 튜토리얼로 이동
                SceneManager.LoadScene(GameConstants.Scene.Tutorial);
            }
            else
            {
                Debug.LogWarning($"[TitleManager] Invalid UserType string: {typeStr}. Please check button arguments.");
            }
        }

        private IEnumerator StartMainBGM()
        {
            if (SoundManager.Instance == null) yield break;

            SoundManager.Instance.StopBGM();
            yield return CoroutineData.GetWaitForSeconds(5.0f);
            SoundManager.Instance.PlayBGM("MainBGM");
        }

        private void OnDestroy()
        {   
            StopAllCoroutines();
            _soundCoroutine = null;
        }
    }
}