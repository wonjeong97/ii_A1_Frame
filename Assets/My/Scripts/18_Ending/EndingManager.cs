using System;
using My.Scripts._18_Ending.Pages;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Timelapse;
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
        public EndingPage4Data page4;
    }

    /// <summary> 
    /// 엔딩 씬의 전체적인 흐름(페이지 전환)과 백그라운드 리소스 작업을 관리하는 매니저입니다.
    /// 사용자가 엔딩을 보는 동안 사진 합성 및 타임랩스 영상 변환을 비동기로 처리합니다.
    /// </summary>
    public class EndingManager : BaseFlowManager
    {   
        [Header("Compositor")]
        [SerializeField] private PhotoCompositor[] compositors;
        
        protected override void Start()
        {
            base.Start(); 

            // 엔딩 씬 진입 즉시 무거운 리소스 작업(사진 합성)을 시작합니다.
            // 사용자가 앞선 엔딩 페이지들을 읽는 동안 백그라운드에서 결과물을 완성하기 위함입니다.
            if (compositors != null && compositors.Length > 0)
            {
                string combinedName = GetCurrentPlayerNames(); 

                foreach (var compositor in compositors)
                {
                    if (compositor != null)
                    {
                        compositor.ProcessAndSave(combinedName);
                    }
                }
            }
          
            // 타임랩스 영상 변환도 병렬로 시작하여, 엔딩 종료 시점에는 파일 생성이 완료되어 있도록 합니다.
            if (TimeLapseRecorder.Instance != null)
            {
                Debug.Log("[EndingManager] 타임랩스 영상 백그라운드 변환 시작");
                TimeLapseRecorder.Instance.ConvertToVideo();
            }
        }
        
        /// <summary>
        /// 현재 플레이어들의 이름을 가져와 파일명 등에 사용할 수 있도록 조합합니다.
        /// </summary>
        /// <returns>조합된 플레이어 이름 문자열</returns>
        private string GetCurrentPlayerNames()
        {
            // # TODO: 현재는 하드코딩된 이름을 반환 중. GameManager에 저장된 실제 유저 데이터를 연동해야 함.
            if (GameManager.Instance != null) 
            {
                // return GameManager.Instance.SavedNameString; 
            }
            return "아영길동"; 
        }
        
        /// <summary>
        /// JSON 파일에서 엔딩 씬 설정을 로드하고, 각 페이지 컨트롤러에 맞는 데이터를 주입합니다.
        /// </summary>
        protected override void LoadSettings()
        {   
            var setting = JsonLoader.Load<EndingLevelSetting>("JSON/Ending");
            
            if (setting != null)
            {
                if (pages == null || pages.Length == 0) return;

                // BaseFlowManager는 GamePage[] 배열로 관리하므로, 
                // 구체적인 파생 클래스(EndingPageXController)로 캐스팅하여 전용 데이터를 설정합니다.
                if (pages.Length > 0 && setting.page1 != null && pages[0] is EndingPage1Controller p1) p1.SetupData(setting.page1);
                if (pages.Length > 1 && setting.page2 != null && pages[1] is EndingPage2Controller p2) p2.SetupData(setting.page2);
                if (pages.Length > 2 && setting.page3 != null && pages[2] is EndingPage3Controller p3) p3.SetupData(setting.page3);
                if (pages.Length > 3 && setting.page4 != null && pages[3] is EndingPage4Controller p4) p4.SetupData(setting.page4);
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