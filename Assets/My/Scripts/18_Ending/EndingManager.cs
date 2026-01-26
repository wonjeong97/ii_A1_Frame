using System;
using My.Scripts._18_Ending.Pages;
using My.Scripts.Core;
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

    public class EndingManager : BaseFlowManager
    {
        protected override void LoadSettings()
        {
            var setting = JsonLoader.Load<EndingLevelSetting>("JSON/Ending");
            
            if (setting != null)
            {
                if (pages.Length > 0 && pages[0] is EndingPage1Controller p1) p1.SetupData(setting.page1);
                if (pages.Length > 1 && pages[1] is EndingPage2Controller p2) p2.SetupData(setting.page2);
                if (pages.Length > 2 && pages[2] is EndingPage3Controller p3) p3.SetupData(setting.page3);
            }
            else
            {
                Debug.LogWarning("[EndingManager] JSON/Ending 로드 실패");
            }
        }

        // 모든 페이지가 끝났을 때의 동작
        protected override void OnAllFinished()
        {
            Debug.Log("[EndingManager] 모든 엔딩 페이지 종료 -> 타이틀로 이동");
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.Title);
            }
        }
    }
}