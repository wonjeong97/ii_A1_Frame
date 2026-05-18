using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts._01_Tutorial.Pages;
using My.Scripts.Core;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;
using ZLogger;

namespace My.Scripts._01_Tutorial
{
    [Serializable]
    public class TutorialSetting
    {
        public TutorialPage1Data page1;
        public TutorialPage2Data page2;
        public TutorialPage3Data page3;
        public TutorialPage4Data page4;
        public TutorialPage5Data page5;
        public TutorialPage6Data page6;
        public TutorialPage7Data page7;
    }

    public class TutorialManager : BaseFlowManager
    {
        // --- 의존성 주입 (DI) 변수 ---
        private IObjectResolver _resolver; // [추가] 수동 주입을 위한 리졸버
        private GameManager _gameManager;
        private SessionManager _sessionManager;
        private FadeManager _fadeManager;
        private ILogger<TutorialManager> _logger;

        [Inject]
        public void ConstructTutorial(
            IObjectResolver resolver, // [추가] VContainer로부터 주입 권한 수령
            GameManager gameManager, 
            SessionManager sessionManager, 
            FadeManager fadeManager,
            ILogger<TutorialManager> logger)
        {
            _resolver = resolver;
            _gameManager = gameManager;
            _sessionManager = sessionManager;
            _fadeManager = fadeManager;
            _logger = logger;
        }

        // BaseFlowManager의 InitializePages를 오버라이드하여 
        // 매니저가 직접 배열에 있는 모든 비활성 페이지들에 의존성(_uiManager 등)을 강제로 꽂아넣습니다.
        protected override void InitializePages()
        {
            if (_resolver != null)
            {
                foreach (GamePage page in pages)
                {
                    if (page)
                    {
                        _resolver.Inject(page); // _uiManager 등 모든 의존성이 완벽하게 채워짐
                    }
                }
                if (_logger != null) _logger.ZLogInformation($"[TutorialManager] 하위 UI 페이지들에 의존성 강제 주입 완료.");
            }
            else
            {
                Debug.LogError("[TutorialManager] IObjectResolver가 없습니다. 씬에 TutorialLifetimeScope가 있는지 확인하세요!");
            }

            base.InitializePages();
        }

        protected override void Start()
        {
            base.Start();
            
            if (_sessionManager != null)
                _sessionManager.OnLanguageChanged += HandleLanguageChanged;
        }

        protected override void OnDestroy()
        {   
            if (_sessionManager != null)
                _sessionManager.OnLanguageChanged -= HandleLanguageChanged;

            base.OnDestroy();
        }
        
        private void HandleLanguageChanged(string newLanguage)
        {
            _logger?.ZLogInformation($"[TutorialManager] 언어 변경 감지됨: {newLanguage}. JSON 설정을 다시 로드합니다.");
            
            LoadSettings(); 

            if (currentPageIndex >= 0 && currentPageIndex < pages.Length)
            {
                GamePage currentPage = pages[currentPageIndex];
                if (currentPage && currentPage.gameObject.activeSelf)
                {
                    currentPage.OnEnter();
                }
            }
        }

        public void SaveCurrentSettings()
        {
            TutorialSetting setting = new TutorialSetting
            {
                page1 = pages.Length > 0 && pages[0] ? pages[0].ExtractCurrentData() as TutorialPage1Data : null,
                page2 = pages.Length > 1 && pages[1] ? pages[1].ExtractCurrentData() as TutorialPage2Data : null,
                page3 = pages.Length > 2 && pages[2] ? pages[2].ExtractCurrentData() as TutorialPage3Data : null,
                page4 = pages.Length > 3 && pages[3] ? pages[3].ExtractCurrentData() as TutorialPage4Data : null,
                page5 = pages.Length > 4 && pages[4] ? pages[4].ExtractCurrentData() as TutorialPage5Data : null,
                page6 = pages.Length > 5 && pages[5] ? pages[5].ExtractCurrentData() as TutorialPage6Data : null,
                page7 = pages.Length > 6 && pages[6] ? pages[6].ExtractCurrentData() as TutorialPage7Data : null,
            };
            
            string lang = _sessionManager != null ? _sessionManager.CurrentLanguage : "ko";
            string path = GameConstants.Path.GetLocalizedPath(GameConstants.Path.Tutorial, lang);
            
            JsonLoader.SaveAsync(path, setting).Forget();
        }

        protected override void LoadSettings()
        {
            string lang = _sessionManager ? _sessionManager.CurrentLanguage : "ko";
            string path = GameConstants.Path.GetLocalizedPath(GameConstants.Path.Tutorial, lang);
            TutorialSetting setting = JsonLoader.Load<TutorialSetting>(path);

            if (setting == null)
            {
                _logger?.ZLogError($"[TutorialManager] JSON 데이터 로드 실패. 경로: {path}");
                return;
            }

            AssignPageDataDirect(setting);
        }

        private void AssignPageDataDirect(TutorialSetting setting)
        {
            if (pages == null || pages.Length == 0) return;

            TrySetupPage(0, setting.page1);
            TrySetupPage(1, setting.page2);
            TrySetupPage(2, setting.page3);
            TrySetupPage(3, setting.page4);
            TrySetupPage(4, setting.page5);
            TrySetupPage(5, setting.page6);
            TrySetupPage(6, setting.page7);
        }

        private void TrySetupPage(int index, object pageData)
        {
            if (index >= pages.Length) return;

            if (pages[index] && pageData != null)
            {
                pages[index].SetupData(pageData);
            }
        }

        protected override void OnAllFinished()
        {
            if (_fadeManager)
            {
                if (_gameManager)
                {
                    _gameManager.ChangeScene(GameConstants.Scene.PlayTutorial);
                }
                else
                {
                    _logger?.ZLogWarning($"[TutorialManager] GameManager Missing. Force loading.");
                    SceneLoader.LoadAsync(GameConstants.Scene.PlayTutorial).Forget();
                }
            }
            else
            {
                _logger?.ZLogWarning($"[TutorialManager] FadeManager Missing. Force loading.");
                SceneLoader.LoadAsync(GameConstants.Scene.PlayTutorial).Forget();
            }
        }

        protected override async UniTaskVoid TransitionAsync(int targetIndex, int info, CancellationToken token)
        {
            isTransitioning = true;
            try
            {
                GamePage current = (currentPageIndex >= 0 && currentPageIndex < pages.Length) ? pages[currentPageIndex] : null;
                
                if (targetIndex < 0 || targetIndex >= pages.Length) return;
                
                GamePage next = pages[targetIndex];

                if (current)
                {
                    await FadePageAsync(current, 1f, 0f, 0.5f, token);
                    current.OnExit();
                }

                if (next)
                {
                    next.OnEnter();
                    HandleTriggerInfo(next, info); 
                    
                    if (targetIndex == 0)
                    {
                        next.SetAlpha(1f);
                    }
                    else
                    {
                        await FadePageAsync(next, 0f, 1f, 0.5f, token);
                    }
                }

                currentPageIndex = targetIndex;
            }
            catch (OperationCanceledException) { }
            finally
            {
                isTransitioning = false;
            }
        }

        private void HandleTriggerInfo(GamePage page, int triggerInfo)
        {
            if (triggerInfo == 0) return;

            if (page is ITriggerReceiver receiver)
            {
                receiver.ReceiveTrigger(triggerInfo);
            }
        }
    }

    public static class TutorialPageUtils
    {
        public static TextSetting BuildTextSetting(Text txt, TextSetting original, string overrideText = null)
        {
            if (txt == null) return original;
            
            RectTransform rt = txt.rectTransform;
            
            return new TextSetting
            {
                name      = original?.name ?? txt.gameObject.name,
                position  = rt != null ? rt.anchoredPosition  : (original?.position ?? Vector2.zero),
                size      = rt != null ? rt.sizeDelta          : (original?.size     ?? Vector2.zero),
                rotation  = rt != null ? rt.localEulerAngles   : (original?.rotation ?? Vector3.zero),
                scale     = rt != null ? rt.localScale          : (original?.scale    ?? Vector3.one),
                text      = overrideText ?? txt.text,
                fontName  = original?.fontName ?? string.Empty,
                fontSize  = txt.fontSize,
                fontColor = txt.color,
                alignment = txt.alignment,
                isBold    = original?.isBold ?? false,
            };
        }
    }
}