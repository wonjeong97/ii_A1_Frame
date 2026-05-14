using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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

        /// <summary> 씬 진입 즉시 사진 합성 작업을 백그라운드에서 실행함. </summary>
        protected override void Start()
        {
            base.Start();

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

        /// <summary> 현재 유저의 고유 식별자 문자열을 반환함. </summary>
        private string GetUserIdString()
        {
            if (GameManager.Instance && SessionManager.Instance)
            {
                return SessionManager.Instance.CurrentUserId.ToString();
            }

            return "0";
        }

        /// <summary> 엔딩 설정 JSON 데이터를 로드하여 각 페이지에 주입함. </summary>
        protected override void LoadSettings()
        {
            string path = GameConstants.Path.GetLocalizedPath(GameConstants.Path.Ending);
            EndingLevelSetting setting = JsonLoader.Load<EndingLevelSetting>(path);

            if (setting == null)
            {
                Debug.LogError(string.Format("[EndingManager] 설정 로드 실패: {0}", path));
                return;
            }

            object[] pageDataArray =
            {
                setting.page1, setting.page2, setting.page3, setting.page4, setting.page5
            };

            int limit = Mathf.Min(pages.Length, pageDataArray.Length);
            for (int i = 0; i < limit; i++)
            {
                GamePage page = pages[i];
                object data = pageDataArray[i];

                if (page && data != null)
                {
                    page.SetupData(data);
                }
            }
        }

        /// <summary> 모든 페이지 완료 시 백그라운드 작업 종료를 비동기로 대기함. </summary>
        protected override void OnAllFinished()
        {
            Debug.Log("[EndingManager] 모든 연출 종료. 리소스 정리 대기 시작.");
            WaitAndReturnToTitleAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// 사진 합성 및 영상 처리가 완료될 때까지 비동기로 대기 후 타이틀로 복귀함.
        /// IEnumerator를 제거하여 상태 머신 할당 오버헤드를 없앰.
        /// </summary>
        private async UniTaskVoid WaitAndReturnToTitleAsync(CancellationToken token)
        {
            const float timeoutSeconds = 300.0f;
            float elapsed = 0f;

            // 작업이 진행 중인 동안 논블로킹(Non-blocking) 대기 루프 실행
            while (elapsed < timeoutSeconds)
            {
                if (!IsAnyProcessBusy())
                {
                    break;
                }

                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            if (elapsed >= timeoutSeconds)
            {
                Debug.LogWarning("[EndingManager] 작업 완료 대기 타임아웃 발생.");
            }

            FinalizeAndReturn();
        }

        /// <summary> 사진 합성이나 영상 처리가 진행 중인지 확인함. </summary>
        private bool IsAnyProcessBusy()
        {
            if (compositors != null)
            {
                foreach (PhotoCompositor compositor in compositors)
                {
                    if (compositor && compositor.IsProcessing)
                    {
                        return true;
                    }
                }
            }

            if (TimeLapseRecorder.Instance && TimeLapseRecorder.Instance.IsProcessing)
            {
                return true;
            }

            return false;
        }

        /// <summary> 세션을 정리하고 타이틀 씬으로 복귀함. </summary>
        private void FinalizeAndReturn()
        {
            if (SessionManager.Instance)
            {
                SessionManager.Instance.ClearSession();
            }

            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.Title);
            }
            else
            {
                SceneLoader.LoadAsync(GameConstants.Scene.Title).Forget();
            }
        }
    }
}