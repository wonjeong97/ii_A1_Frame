using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using My.Scripts._01_Tutorial.Pages;
using My.Scripts.Core;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial
{
    /// <summary> JSON 파싱용 튜토리얼 전체 설정 데이터 컨테이너 </summary>
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

    /// <summary>
    /// 튜토리얼 씬의 전반적인 페이지 흐름(1~7)을 제어하고 데이터를 분배하는 매니저입니다.
    /// </summary>
    public class TutorialManager : BaseFlowManager
    {
        protected override void Start()
        {
            base.Start();
            if (GameManager.Instance)
                GameManager.Instance.OnInspectorClosed += SaveCurrentSettings;
            
            if (SessionManager.Instance)
                SessionManager.Instance.OnLanguageChanged += HandleLanguageChanged;
        }

        protected override void OnDestroy()
        {   
            base.OnDestroy();
            if (GameManager.Instance)
                GameManager.Instance.OnInspectorClosed -= SaveCurrentSettings;
            
            if (SessionManager.Instance)
                SessionManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
        }
        
        private void HandleLanguageChanged(string newLanguage)
        {
            Debug.Log($"[TutorialManager] 언어 변경 감지됨: {newLanguage}. JSON 설정을 다시 로드합니다.");
            LoadSettings(); // 변경된 언어 경로의 JSON으로 재로드 및 각 페이지 SetupData 재실행
        }

        private void SaveCurrentSettings()
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
            string path = GameConstants.Path.GetLocalizedPath(GameConstants.Path.Tutorial);
            JsonLoader.Save(setting, path);
        }

        /// <summary>
        /// JSON 설정 파일을 로드하여 각 튜토리얼 페이지에 데이터를 주입함.
        /// 데이터를 배열로 래핑하여 루프로 처리함으로써 코드 중복을 제거함.
        /// </summary>
        protected override void LoadSettings()
        {
            string path = GameConstants.Path.GetLocalizedPath(GameConstants.Path.Tutorial);
            TutorialSetting setting = JsonLoader.Load<TutorialSetting>(path);

            if (setting == null)
            {
                Debug.LogError("[TutorialManager] JSON 데이터 로드 실패. 경로: " + path);
                return;
            }

            // 개별 필드로 구성된 페이지 데이터를 배열로 묶어 반복 처리가 가능하도록 함.
            object[] pageDataArray = new object[]
            {
                setting.page1, setting.page2, setting.page3,
                setting.page4, setting.page5, setting.page6, setting.page7
            };

            // 할당된 페이지 수와 데이터 구조체의 필드 수 중 작은 값을 기준으로 순회함.
            int maxCount = Mathf.Min(pages.Length, pageDataArray.Length);

            for (int i = 0; i < maxCount; i++)
            {
                GamePage page = pages[i];
                object data = pageDataArray[i];

                if (page)
                {
                    if (data != null)
                    {
                        page.SetupData(data);
                    }
                    else
                    {
                        Debug.LogWarning("[TutorialManager] " + (i + 1) + "번 페이지 데이터가 JSON에 누락됨.");
                    }
                }
                else
                {
                    Debug.LogWarning("[TutorialManager] " + i + "번 인덱스의 페이지 컴포넌트가 인스펙터에서 누락됨.");
                }
            }
        }

        /// <summary> 모든 튜토리얼 과정이 완료되면 본 게임(PlayTutorial) 씬으로 부드럽게 전환합니다. </summary>
        protected override void OnAllFinished()
        {
            if (FadeManager.Instance)
            {
                if (GameManager.Instance)
                {
                    GameManager.Instance.ChangeScene(GameConstants.Scene.PlayTutorial);
                }
                else
                {
                    Debug.LogWarning("GameManager Missing. Force loading.");
                    SceneLoader.LoadAsync(GameConstants.Scene.PlayTutorial).Forget();
                }
            }
            else
            {
                Debug.LogWarning("FadeManager Missing. Force loading.");
                SceneLoader.LoadAsync(GameConstants.Scene.PlayTutorial).Forget();
            }
        }

        /// <summary> 현재 페이지를 페이드아웃하고 다음 페이지를 페이드인하는 시각적 전환 연출을 수행합니다. </summary>
        protected override async UniTaskVoid TransitionAsync(int targetIndex, int info, CancellationToken token)
        {
            isTransitioning = true;
            try
            {
                GamePage current = (currentPageIndex >= 0 && currentPageIndex < pages.Length) ? pages[currentPageIndex] : null;
                
                if (targetIndex < 0 || targetIndex >= pages.Length)
                {
                    Debug.LogWarning($"[TutorialManager] Invalid targetIndex: {targetIndex}");
                    return;
                }
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

        /// <summary> 이전 페이지의 특정 조작 결과(info)를 다음 페이지의 초기 상태에 반영합니다. </summary>
        private void HandleTriggerInfo(GamePage page, int triggerInfo)
        {
            if (triggerInfo == 0) return;

            // 공용 인터페이스를 통해 강한 결합도(Coupling) 없이 안전하게 트리거 전달
            if (page is ITriggerReceiver receiver)
            {
                receiver.ReceiveTrigger(triggerInfo);
            }
        }
    }

    /// <summary> 튜토리얼 페이지 컨트롤러가 UI 컴포넌트에서 TextSetting을 추출할 때 사용하는 공용 헬퍼 </summary>
    public static class TutorialPageUtils
    {
        /// <summary>
        /// Text 컴포넌트와 RectTransform의 현재 런타임 값을 읽어 TextSetting을 구성한다.
        /// fontName, isBold처럼 역추적이 불가한 필드는 original에서 그대로 유지한다.
        /// overrideText를 지정하면 text 필드를 컴포넌트 값 대신 해당 값으로 고정한다
        /// ({nameA} 등 템플릿 변수가 런타임에 치환된 경우 원본 템플릿 보존용).
        /// </summary>
        public static TextSetting BuildTextSetting(Text txt, TextSetting original, string overrideText = null)
        {
            if (txt == null) return original;
            RectTransform rt = txt.GetComponent<RectTransform>();
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