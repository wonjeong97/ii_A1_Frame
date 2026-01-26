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
        public EndingPage3Data page3; 
    }

    public class EndingManager : MonoBehaviour
    {
        [Header("Pages")]
        [SerializeField] private EndingPage1Controller page1;
        [SerializeField] private EndingPage2Controller page2;
        [SerializeField] private EndingPage3Controller page3; 

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
                if (page3 != null) page3.SetupData(setting.page3);
            }
            else
            {
                Debug.LogWarning("[EndingManager] JSON/Ending 로드 실패");
            }
        }

        private void InitializePages()
        {
            // [Page 1]
            if (page1 != null)
            {
                page1.gameObject.SetActive(true);
                page1.OnEnter();
                page1.onStepComplete += (info) => 
                {
                    page1.OnExit();
                    // Page 1 -> Page 2 이동
                    if (page2 != null)
                    {
                        page2.gameObject.SetActive(true);
                        page2.OnEnter();
                    }
                    else ReturnToTitle();
                };
            }

            // [Page 2]
            if (page2 != null)
            {
                page2.gameObject.SetActive(false);
                page2.onStepComplete += (info) => 
                {
                    page2.OnExit();
                    // Page 2 -> Page 3 이동
                    if (page3 != null)
                    {
                        page3.gameObject.SetActive(true);
                        page3.OnEnter();
                    }
                    else ReturnToTitle();
                };
            }

            // [Page 3] 
            if (page3 != null)
            {
                page3.gameObject.SetActive(false);
                page3.onStepComplete += (info) => 
                {
                    // Page 3 완료 -> 타이틀로 이동
                    ReturnToTitle();
                };
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