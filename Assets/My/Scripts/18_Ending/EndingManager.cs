using System;
using My.Scripts._18_Ending.Pages;
using UnityEngine;
using Wonjeong.Utils; // JsonLoader 사용

namespace My.Scripts._18_Ending
{
    // [추가] 엔딩 씬 전체 데이터 구조
    [Serializable]
    public class EndingLevelSetting
    {
        public EndingPage1Data page1;
    }

    public class EndingManager : MonoBehaviour
    {
        [Header("Pages")]
        [SerializeField] private EndingPage1Controller page1;

        private void Start()
        {
            LoadSettings();
            InitializePages();
        }

        // JSON 로드 및 데이터 주입
        private void LoadSettings()
        {
            // JSON 파일 경로: Resources/JSON/Ending.json
            var setting = JsonLoader.Load<EndingLevelSetting>("JSON/Ending");
            
            if (setting != null)
            {
                if (page1 != null) page1.SetupData(setting.page1);
            }
            else
            {
                Debug.LogWarning("[EndingManager] JSON/Ending 로드 실패 (파일이 없거나 형식이 잘못됨)");
            }
        }

        private void InitializePages()
        {
            if (page1 != null)
            {
                page1.gameObject.SetActive(true);
                page1.OnEnter();
            }
            else
            {
                Debug.LogError("[EndingManager] Page1 Controller가 연결되지 않았습니다.");
            }
        }
    }
}