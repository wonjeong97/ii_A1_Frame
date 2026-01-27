using System;
using My.Scripts._18_Ending.Pages;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Utils;
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

    /// <summary> 엔딩 씬 진행 관리 매니저 </summary>
    public class EndingManager : BaseFlowManager
    {   
        [Header("Compositor")]
        [SerializeField] private PhotoCompositor[] compositors;
        
        protected override void Start()
        {
            base.Start(); // LoadSettings -> InitializePages -> StartFlow

            // 엔딩 씬 시작 시 백그라운드 합성 실행
          if (compositors != null && compositors.Length > 0)
            {
                // GameManager 등에서 가져온 플레이어 이름 조합
                string combinedName = GetCurrentPlayerNames(); 

                foreach (var compositor in compositors)
                {
                    if (compositor != null)
                    {
                        compositor.ProcessAndSave(combinedName);
                    }
                }
            }
        }
        
        private string GetCurrentPlayerNames()
        {
            if (GameManager.Instance != null) 
            {
                // GameManager에 이름을 저장해두었다고 가정
                // return GameManager.Instance.SavedNameString; 
            }
            return "아영길동"; 
        }
        
        /// <summary> 설정 로드 및 데이터 주입 </summary>
        protected override void LoadSettings()
        {   
            var setting = JsonLoader.Load<EndingLevelSetting>("JSON/Ending");
            
            if (setting != null)
            {
                // 페이지 배열 유효성 체크
                if (pages == null || pages.Length == 0)
                {
                    Debug.LogWarning("[EndingManager] pages 비어있음");
                    return;
                }

                // 각 페이지에 데이터 주입
                if (pages.Length > 0 && setting.page1 != null && pages[0] is EndingPage1Controller p1) p1.SetupData(setting.page1);
                if (pages.Length > 1 && setting.page2 != null && pages[1] is EndingPage2Controller p2) p2.SetupData(setting.page2);
                if (pages.Length > 2 && setting.page3 != null && pages[2] is EndingPage3Controller p3) p3.SetupData(setting.page3);
            }
            else
            {
                Debug.LogWarning("[EndingManager] JSON/Ending 로드 실패");
            }
        }

        /// <summary> 모든 페이지 종료 시 처리 (타이틀 이동) </summary>
        protected override void OnAllFinished()
        {
            Debug.Log("[EndingManager] 종료 -> 타이틀 이동");
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.Title);
            }
        }
    }
}