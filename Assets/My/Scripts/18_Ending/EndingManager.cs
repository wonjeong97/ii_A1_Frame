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
    /// <summary> JSON 파일(Ending.json) 역직렬화를 위한 데이터 래핑 컨테이너 </summary>
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
    /// 엔딩 씬의 전체 페이지 흐름을 제어하고, 
    /// 화면 밖에서 진행되는 무거운 리소스 처리(사진 합성, 영상 인코딩 및 업로드)의 동기화를 책임지는 매니저입니다.
    /// </summary>
    public class EndingManager : BaseFlowManager
    {   
        [Header("Compositor")]
        [SerializeField] private PhotoCompositor[] compositors;
        
        /// <summary> 씬 진입 즉시 대기 시간을 줄이기 위해 사진 합성 작업을 백그라운드에서 선제적으로 가동합니다. </summary>
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
        
        /// <summary> 로컬 파일 저장 및 서버 매핑에 사용할 현재 유저의 고유 식별자 문자열을 안전하게 반환합니다. </summary>
        private string GetUserIdString()
        {
            if (GameManager.Instance && SessionManager.Instance) 
                return SessionManager.Instance.CurrentUserId.ToString(); 
            return "0"; 
        }
        
        /// <summary> 각 엔딩 페이지 컨트롤러(1~5)에 필요한 텍스트 및 UI 설정 JSON 데이터를 로드하여 주입합니다. </summary>
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
        /// 모든 페이지 시퀀스가 완료되었을 때 즉시 전환하지 않고, 
        /// 데이터 유실을 막기 위해 백그라운드 작업 종료 대기 가드 로직을 실행합니다.
        /// </summary>
        protected override void OnAllFinished()
        {
            Debug.Log("[EndingManager] 모든 연출 종료. 백그라운드 리소스 정리 및 업로드 대기 시작...");
            StartCoroutine(WaitAndReturnToTitleRoutine());
        }

        /// <summary> 
        /// 사진 합성, 영상 인코딩, 영상 서버 업로드가 모두 끝날 때까지 대기한 후 타이틀 씬으로 돌아갑니다.
        /// 무한 대기(프리징)를 방지하기 위해 최대 300초(5분)의 넉넉한 하드 타임아웃을 적용합니다.
        /// </summary>
        private IEnumerator WaitAndReturnToTitleRoutine()
        {
            // 영상 업로드는 수십 초 이상 걸릴 수 있으므로 타임아웃을 10초에서 300초로 증가
            float timeout = 300.0f; 
            float startWaitTime = Time.time;

            while (Time.time - startWaitTime < timeout)
            {
                bool isAnyBusy = false;

                // 1. 사진 합성 모듈 작업 상태 체크
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

                // 2. 비디오 인코딩 및 서버 업로드 작업 상태 체크 (IsUploading 포함됨)
                if (!isAnyBusy && TimeLapseRecorder.Instance && TimeLapseRecorder.Instance.IsProcessing)
                {
                    isAnyBusy = true;
                }

                // 모든 작업(인코딩, 합성, 서버 업로드)이 끝났다면 즉시 루프 탈출
                if (!isAnyBusy) break;

                yield return null; 
            }

            if (Time.time - startWaitTime >= timeout)
                Debug.LogWarning("[EndingManager] 작업 대기 타임아웃(300초) 발생. 강제로 타이틀로 복귀합니다.");
            
            if (SessionManager.Instance) 
            {
                SessionManager.Instance.ClearSession();
            }

            // 모든 정리가 완료된 후 안전하게 타이틀로 씬 전환
            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.Title);
            }
        }
    }
}