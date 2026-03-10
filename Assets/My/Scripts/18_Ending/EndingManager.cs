using System;
using My.Scripts._18_Ending.Pages;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Timelapse;
using My.Scripts.Utils;
using UnityEngine;
using UnityEngine.Serialization;
using Wonjeong.Utils; 

namespace My.Scripts._18_Ending
{
    [Serializable]
    public class EndingLevelSetting
    {
        public EndingPage1Data page1;
        public EndingPage2Data page2;
        public EndingPage3Data page3;
        public EndingPage4Data page4;
        public EndingPage5Data page5;
    }

    /// <summary> 
    /// 엔딩 씬의 전체적인 흐름(페이지 전환)과 백그라운드 리소스 작업을 관리하는 매니저입니다.
    /// 사용자가 엔딩을 보는 동안 사진 합성을 비동기로 처리합니다.
    /// </summary>
    public class EndingManager : BaseFlowManager
    {   
        [Header("Compositor")]
        [SerializeField] private PhotoCompositor[] compositors;
        
        protected override void Start()
        {
            base.Start(); 

            // 엔딩 씬 진입 즉시 무거운 리소스 작업(사진 합성)을 시작합니다.
            if (compositors != null && compositors.Length > 0)
            {
                string userIdStr = GetUserIdString(); 

                foreach (PhotoCompositor compositor in compositors)
                {
                    if (compositor)
                    {
                        compositor.ProcessAndSave(userIdStr);
                    }
                }
            }
        }
        
        /// <summary>
        /// 파일명 식별자를 위해 유저 인덱스(User ID)를 문자열로 가져옵니다.
        /// </summary>
        private string GetUserIdString()
        {
            if (GameManager.Instance && SessionManager.Instance) 
            {
                return SessionManager.Instance.CurrentUserId.ToString(); 
            }
            return "0"; 
        }
        
        /// <summary>
        /// JSON 파일에서 엔딩 씬 설정을 로드하고, 각 페이지 컨트롤러에 맞는 데이터를 주입합니다.
        /// </summary>
        protected override void LoadSettings()
        {   
            EndingLevelSetting setting = JsonLoader.Load<EndingLevelSetting>("JSON/Ending");
            
            if (setting != null)
            {
                if (pages == null || pages.Length == 0) return;

                if (pages.Length > 0 && setting.page1 != null && pages[0] is EndingPage1Controller p1) p1.SetupData(setting.page1);
                if (pages.Length > 1 && setting.page2 != null && pages[1] is EndingPage2Controller p2) p2.SetupData(setting.page2);
                if (pages.Length > 2 && setting.page3 != null && pages[2] is EndingPage3Controller p3) p3.SetupData(setting.page3);
                if (pages.Length > 3 && setting.page4 != null && pages[3] is EndingPage4Controller p4) p4.SetupData(setting.page4);
                if (pages.Length > 4 && setting.page5 != null && pages[4] is EndingPage5Controller p5) p5.SetupData(setting.page5);
            }
            else
            {
                Debug.LogWarning("[EndingManager] JSON/Ending 로드 실패");
            }
        }

        protected override void OnAllFinished()
        {
            Debug.Log("[EndingManager] 종료 -> 타이틀 이동");
            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.Title);
            }
        }
    }
}