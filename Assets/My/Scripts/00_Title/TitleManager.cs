using My.Scripts.Global;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wonjeong.Data;
using Wonjeong.Utils;

namespace My.Scripts._00_Title
{
    public class TitleManager : MonoBehaviour
    {
        private bool _isTransitioning = false;
        private float _fadeTime = 1.0f;
    
        private void Start()
        {
            LoadSettings();
        }
        
        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);
            
            if (settings != null)
            {
                _fadeTime = settings.fadeTime;
            }
            else
            {
                Debug.LogWarning("[TitleManager] Settings.json 로드 실패.");
            }
        }
        
        private void Update()
        {
            if (_isTransitioning) return;

            // 플레이어 1 태그 시뮬레이션 (키보드 1번)
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ProcessTag(1);
            }
            // 플레이어 2 태그 시뮬레이션 (키보드 2번)
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                ProcessTag(2);
            }
        }

        private void ProcessTag(int playerID)
        {
            if (_isTransitioning) return;
            _isTransitioning = true; // 중복 호출 방지

            if (GameManager.Instance != null)
            {
                // 1. 필요한 데이터 설정 (누가 태그했는지)
                GameManager.Instance.firstTaggedPlayer = playerID;

                // 2. 공통 씬 이동 메서드 호출 (씬 이름 전달)
                GameManager.Instance.ChangeScene(GameConstants.Scene.Tutorial);
            }
            else
            {
                // 비상 시
                SceneManager.LoadScene(GameConstants.Scene.Tutorial);
            }
        }
    }
}