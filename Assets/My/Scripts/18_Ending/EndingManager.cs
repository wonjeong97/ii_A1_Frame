using System;
using System.Collections;
using My.Scripts._18_Ending.Pages;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Timelapse;
using My.Scripts.Utils;
using UnityEngine;
using Wonjeong.Utils; 

namespace My.Scripts._18_Ending
{
    /// <summary> JSON 파싱용 엔딩 전체 설정 데이터 컨테이너 </summary>
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
    /// 엔딩 씬의 전체적인 흐름과 백그라운드 리소스 작업(이미지 합성, 업로드)을 관리합니다.
    /// 모든 페이지가 종료된 후 데이터 무결성을 위해 작업 완료 여부를 체크합니다.
    /// </summary>
    public class EndingManager : BaseFlowManager
    {   
        [Header("Compositor")]
        [SerializeField] private PhotoCompositor[] compositors;
        
        /// <summary> 엔딩 씬 진입 즉시 시간이 소요되는 이미지 합성 작업을 백그라운드에서 개시합니다. </summary>
        protected override void Start()
        {
            base.Start(); 

            if (compositors != null && compositors.Length > 0)
            {
                string userIdStr = GetUserIdString(); 
                foreach (PhotoCompositor compositor in compositors)
                {
                    if (compositor) compositor.ProcessAndSave(userIdStr);
                }
            }
        }
        
        /// <summary> 현재 세션의 유저 ID를 파일 식별용 문자열로 변환합니다. </summary>
        private string GetUserIdString()
        {
            if (GameManager.Instance && SessionManager.Instance) 
                return SessionManager.Instance.CurrentUserId.ToString(); 
            return "0"; 
        }
        
        /// <summary> 각 엔딩 페이지 컨트롤러에 필요한 JSON 데이터를 주입합니다. </summary>
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
        }

        /// <summary> 
        /// 모든 페이지 시퀀스가 완료되었을 때 실행됩니다.
        /// 즉시 이동하지 않고 백그라운드 작업 완료를 대기하는 가드 로직을 실행합니다.
        /// </summary>
        protected override void OnAllFinished()
        {
            Debug.Log("[EndingManager] 모든 연출 종료. 백그라운드 리소스 정리 대기 시작...");
            StartCoroutine(WaitAndReturnToTitleRoutine());
        }

        /// <summary> 
        /// 이미지 합성, 업로드 및 영상 변환 작업이 모두 완료될 때까지 대기한 후 타이틀로 전환합니다.
        /// 네트워크 지연 등으로 인한 무한 대기를 방지하기 위해 10초 타임아웃을 적용합니다.
        /// </summary>
        private IEnumerator WaitAndReturnToTitleRoutine()
        {
            float timeout = 10.0f; // 최대 대기 허용 시간
            float startWaitTime = Time.time;

            while (Time.time - startWaitTime < timeout)
            {
                bool isAnyBusy = false;

                // 1. 사진 합성 및 업로드 진행 상태 확인
                if (compositors != null)
                {
                    foreach (PhotoCompositor compositor in compositors)
                    {
                        if (compositor && compositor.IsProcessing)
                        {
                            isAnyBusy = true;
                            break;
                        }
                    }
                }

                // 2. 타임랩스/리얼타임 영상 변환 진행 상태 확인
                if (!isAnyBusy && TimeLapseRecorder.Instance && TimeLapseRecorder.Instance.IsProcessing)
                {
                    isAnyBusy = true;
                }

                // 모든 백그라운드 프로세스가 종료되면 대기 종료
                if (!isAnyBusy) break;

                yield return null; // 다음 프레임까지 대기
            }

            if (Time.time - startWaitTime >= timeout)
                Debug.LogWarning("[EndingManager] 작업 대기 타임아웃 발생. 데이터 유실 가능성이 있으나 강제 종료합니다.");

            // 세션 종료 및 타이틀로 복귀
            if (GameManager.Instance) GameManager.Instance.ChangeScene(GameConstants.Scene.Title);
        }
    }
}