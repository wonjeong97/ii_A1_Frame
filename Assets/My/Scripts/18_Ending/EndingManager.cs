using System;
using My.Scripts._18_Ending.Pages;
using My.Scripts.Global;
using UnityEngine;
using Wonjeong.Utils; 

namespace My.Scripts._18_Ending
{
    [Serializable]
    public class EndingLevelSetting
    {
        public EndingPage1Data page1;
        public EndingPage2Data page2;
    }

    public class EndingManager : MonoBehaviour
    {
        [Header("Pages")]
        [SerializeField] private EndingPage1Controller page1;
        [SerializeField] private EndingPage2Controller page2;

        private void Start()
        {
            LoadSettings();
            InitializePages();
        }

        private void LoadSettings()
        {
            var setting = JsonLoader.Load<EndingLevelSetting>("JSON/Ending");
            
            if (setting != null)
            {
                if (page1 != null) page1.SetupData(setting.page1);
                if (page2 != null) page2.SetupData(setting.page2);
            }
            else
            {
                Debug.LogWarning("[EndingManager] JSON/Ending 로드 실패");
            }
        }

        private void InitializePages()
        {
            // Page 1 설정
            if (page1 != null)
            {
                page1.gameObject.SetActive(true);
                page1.OnEnter();
                // Page 1 완료 시 -> Page 2로 이동
                page1.onStepComplete += (info) => 
                {
                    page1.OnExit();
                    if (page2 != null)
                    {
                        page2.gameObject.SetActive(true);
                        page2.OnEnter();
                    }
                    else ReturnToTitle();
                };
            }

            // Page 2 설정
            if (page2 != null)
            {
                page2.gameObject.SetActive(false); // 처음엔 꺼둠
            }
        }

        private void ReturnToTitle()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.Title);
            }
        }
    }
}