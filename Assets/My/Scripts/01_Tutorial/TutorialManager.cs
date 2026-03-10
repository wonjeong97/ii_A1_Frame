using System;
using System.Collections;
using My.Scripts._01_Tutorial.Pages;
using My.Scripts.Core;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        /// <summary> 로컬 JSON에서 튜토리얼 텍스트 데이터를 읽어와 각 페이지 컨트롤러에 미리 주입합니다. </summary>
        protected override void LoadSettings()
        {
            TutorialSetting setting = JsonLoader.Load<TutorialSetting>(GameConstants.Path.Tutorial);
            if (setting == null)
            {
                Debug.LogError($"[TutorialManager] JSON Load Failed");
                return;
            }

            if (pages.Length > 0 && pages[0]) pages[0].SetupData(setting.page1);
            if (pages.Length > 1 && pages[1]) pages[1].SetupData(setting.page2);
            if (pages.Length > 2 && pages[2]) pages[2].SetupData(setting.page3);
            if (pages.Length > 3 && pages[3]) pages[3].SetupData(setting.page4);
            if (pages.Length > 4 && pages[4]) pages[4].SetupData(setting.page5);
            if (pages.Length > 5 && pages[5]) pages[5].SetupData(setting.page6);
            if (pages.Length > 6 && pages[6]) pages[6].SetupData(setting.page7);
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
                    SceneManager.LoadScene(GameConstants.Scene.PlayTutorial);
                }
            }
            else
            {
                Debug.LogWarning("FadeManager Missing. Force loading.");
                SceneManager.LoadScene(GameConstants.Scene.PlayTutorial);
            }
        }

        /// <summary> 현재 페이지를 페이드아웃하고 다음 페이지를 페이드인하는 시각적 전환 연출을 수행합니다. </summary>
        protected override IEnumerator TransitionRoutine(int targetIndex, int info)
        {
            isTransitioning = true;
            GamePage current = (currentPageIndex >= 0 && currentPageIndex < pages.Length) ? pages[currentPageIndex] : null;
            
            if (targetIndex < 0 || targetIndex >= pages.Length)
            {
                Debug.LogWarning($"[TutorialManager] Invalid targetIndex: {targetIndex}");
                isTransitioning = false;
                yield break;
            }
            GamePage next = pages[targetIndex];

            if (current)
            {
                yield return StartCoroutine(FadePage(current, 1f, 0f));
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
                    yield return StartCoroutine(FadePage(next, 0f, 1f));
                }
            }

            currentPageIndex = targetIndex;
            isTransitioning = false;
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
}