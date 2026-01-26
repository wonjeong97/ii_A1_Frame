using My.Scripts.Global;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wonjeong.Data;
using Wonjeong.Utils;

namespace My.Scripts._00_Title
{
    /// <summary> 타이틀 화면 입력 처리 및 씬 전환 매니저 </summary>
    public class TitleManager : MonoBehaviour
    {
        private bool _isTransitioning = false; // 중복 전환 방지 플래그
        private float _fadeTime = 1.0f; // 페이드 시간 (설정값)
    
        private void Start()
        {
            LoadSettings();
        }
        
        /// <summary> JSON 설정 파일 로드 </summary>
        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);
            
            if (settings != null)
            {
                _fadeTime = settings.fadeTime; // 설정값 적용
            }
            else
            {
                Debug.LogWarning("[TitleManager] Settings.json 로드 실패.");
            }
        }
        
        /// <summary> 입력 감지 (태그 시뮬레이션) </summary>
        private void Update()
        {
            if (_isTransitioning) return; // 전환 중이면 입력 무시

            // 플레이어 1 태그 (키보드 1번)
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ProcessTag(1);
            }
            // 플레이어 2 태그 (키보드 2번)
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                ProcessTag(2);
            }
        }

        /// <summary> 태그 처리 및 튜토리얼 씬 이동 </summary>
        private void ProcessTag(int playerID)
        {
            if (_isTransitioning) return;
            _isTransitioning = true; // 중복 호출 방지

            if (GameManager.Instance != null)
            {
                // 1. 태그한 플레이어 정보 저장
                GameManager.Instance.firstTaggedPlayer = playerID;

                // 2. 튜토리얼 씬으로 이동
                GameManager.Instance.ChangeScene(GameConstants.Scene.Tutorial);
            }
            else
            {
                // 비상 시 (매니저 없을 경우)
                SceneManager.LoadScene(GameConstants.Scene.Tutorial);
            }
        }
    }
}