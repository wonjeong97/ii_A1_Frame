using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging; // [추가] 로깅 네임스페이스
using My.Scripts._18_Ending.Pages;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Timelapse;
using My.Scripts.Utils;
using UnityEngine;
using VContainer;
using Wonjeong.Utils;
using ZLogger; // [추가] 고성능 ZLogger 네임스페이스

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
    /// 엔딩 씬의 전체 페이지 흐름을 제어하고, 
    /// 화면 밖에서 진행되는 무거운 리소스 처리(사진 합성, 영상 인코딩 및 업로드)의 동기화를 책임지는 매니저입니다.
    /// ZLogger 연동 및 페이지 바인딩 복잡도 최적화가 적용되었습니다.
    /// </summary>
    public class EndingManager : BaseFlowManager
    {
        [Header("Compositor")]
        [SerializeField] private PhotoCompositor[] compositors;
        
        private bool _settingsLoaded;

        // --- 의존성 주입 (DI) 변수 ---
        private IObjectResolver _resolver;
        private GameManager _gameManager;
        private SessionManager _sessionManager;
        private TimeLapseRecorder _timeLapseRecorder;
        private ILogger<EndingManager> _logger; 

        [Inject]
        public void ConstructEnding(
            IObjectResolver resolver,
            GameManager gameManager, 
            SessionManager sessionManager, 
            TimeLapseRecorder timeLapseRecorder,
            ILogger<EndingManager> logger) 
        {
            _resolver = resolver;
            _gameManager = gameManager;
            _sessionManager = sessionManager;
            _timeLapseRecorder = timeLapseRecorder;
            _logger = logger;
        }

        protected override void Start()
        {
            if (_resolver == null)
            {
                _logger?.ZLogError($"[EndingManager] IObjectResolver가 없습니다. EndingLifetimeScope 세팅을 확인하세요!");
                return;
            }
            
            // 1. Ending Page에 의존성 주입
            if (_resolver != null && pages != null)
            {
                foreach (GamePage page in pages)
                {
                    if (page) _resolver.Inject(page);
                }
            }

            // 2. 데이터 로드 & 텍스트 바인딩
            base.Start();
            if (!_settingsLoaded) return;

            // 3. PhotoCompositor를 통한 사진 합성
            ProcessPhotoCompositor();
        }

        private void ProcessPhotoCompositor()
        {
            if (compositors != null && compositors.Length > 0)
            {
                string userIdStr = GetUserIdString();
                
                for (int i = 0; i < compositors.Length; i++)
                {
                    if (compositors[i])
                    {
                        compositors[i].ProcessAndSave(userIdStr);
                    }
                }
            }
        }
        
        private string GetUserIdString()
        {
            if (_sessionManager != null && _sessionManager.CurrentUserId != 0)
            {
                return _sessionManager.CurrentUserId.ToString();
            }
            return "0";
        }

        protected override void LoadSettings()
        {
            string lang = _sessionManager != null ? _sessionManager.CurrentLanguage : "ko";
            string path = GameConstants.Path.GetLocalizedPath(GameConstants.Path.Ending, lang);
            EndingLevelSetting setting = JsonLoader.Load<EndingLevelSetting>(path);

            if (setting == null)
            {   
                _settingsLoaded = false;
                _logger?.ZLogError($"[EndingManager] 설정 로드 실패: {path}");
                return;
            }

            AssignPageDataDirect(setting);
            _settingsLoaded = true;
        }

        /// <summary> 런타임 배열 생성이 전혀 없는 초고속 평탄화 데이터 셋 </summary>
        private void AssignPageDataDirect(EndingLevelSetting setting)
        {
            if (pages == null || pages.Length == 0) return;

            TrySetupPage(0, setting.page1);
            TrySetupPage(1, setting.page2);
            TrySetupPage(2, setting.page3);
            TrySetupPage(3, setting.page4);
            TrySetupPage(4, setting.page5);
        }

        /// <summary> 
        /// 단일 페이지 데이터 바인딩을 안전하게 처리하는 마이크로 헬퍼 메서드.
        /// </summary>
        private void TrySetupPage(int index, object pageData)
        {
            if (index >= pages.Length) return;

            if (pages[index] != null && pageData != null)
            {
                pages[index].SetupData(pageData);
            }
            else if (pageData == null)
            {
                _logger?.ZLogWarning($"[EndingManager] JSON 내 Page {index + 1} 데이터가 누락되었습니다.");
            }
        }

        protected override void OnAllFinished()
        {
            _logger?.ZLogInformation($"[EndingManager] 모든 연출 종료. 리소스 정리 대기 시작.");
            WaitAndReturnToTitleAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid WaitAndReturnToTitleAsync(CancellationToken token)
        {
            try
            {
                const float timeoutSeconds = 60.0f;
                float elapsed = 0f;
                const int pollingIntervalMs = 500; 

                while (elapsed < timeoutSeconds)
                {
                    if (!IsAnyProcessBusy())
                    {
                        break;
                    }

                    await UniTask.Delay(pollingIntervalMs, ignoreTimeScale: true, cancellationToken: token);
                    elapsed += (pollingIntervalMs / 1000f);
                }

                if (elapsed >= timeoutSeconds)
                {
                    _logger?.ZLogWarning($"[EndingManager] 작업 완료 대기 타임아웃 발생. 강제 복귀 진행.");
                }

                FinalizeAndReturn();
            }
            catch (OperationCanceledException)
            {
                // 씬 전환/파괴 시 발생하는 정상적인 비동기 취소 예외 처리
            }
        }

        private bool IsAnyProcessBusy()
        {
            if (compositors != null)
            {
                for (int i = 0; i < compositors.Length; i++)
                {
                    if (compositors[i] && compositors[i].IsProcessing)
                    {
                        return true;
                    }
                }
            }

            if (_timeLapseRecorder && _timeLapseRecorder.IsProcessing)
            {
                return true;
            }

            return false;
        }

        private void FinalizeAndReturn()
        {
            if (_sessionManager)
            {
                _sessionManager.ClearSession();
            }

            if (_gameManager)
            {
                _gameManager.ChangeScene(GameConstants.Scene.Title);
            }
            else
            {
                SceneLoader.LoadAsync(GameConstants.Scene.Title).Forget();
            }
        }
    }
}