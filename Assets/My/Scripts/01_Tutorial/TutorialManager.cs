using System;
using System.Collections;
using My.Scripts._01_Tutorial.Pages;
using My.Scripts.Core;
using My.Scripts.Global;
using UnityEngine;
using Wonjeong.UI;
using Wonjeong.Utils;

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
    }

    // BaseFlowManager 상속
    public class TutorialManager : BaseFlowManager
    {
        // pages 배열은 부모에 있음

        protected override void LoadSettings()
        {
            var setting = JsonLoader.Load<TutorialSetting>(GameConstants.Path.Tutorial);
            if (setting == null)
            {
                Debug.LogError($"[TutorialManager] JSON Load Failed");
                return;
            }

            // Generic SetupData 활용
            if (pages.Length > 0) pages[0].SetupData(setting.page1);
            if (pages.Length > 1) pages[1].SetupData(setting.page2);
            if (pages.Length > 2) pages[2].SetupData(setting.page3);
            if (pages.Length > 3) pages[3].SetupData(setting.page4);
            if (pages.Length > 4) pages[4].SetupData(setting.page5);
            if (pages.Length > 5) pages[5].SetupData(setting.page6);
        }

        // 튜토리얼 종료 시
        protected override void OnAllFinished()
        {
            if (FadeManager.Instance != null)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ChangeScene(GameConstants.Scene.PlayTutorial);
                }
            }
            else
            {
                Debug.LogWarning("FadeManager Missing. Force loading.");
                UnityEngine.SceneManagement.SceneManager.LoadScene(GameConstants.Scene.PlayTutorial);
            }
        }

        // 튜토리얼은 TriggerInfo(플레이어1/2 입력 등) 전달이 중요함
        protected override IEnumerator TransitionRoutine(int targetIndex, int info)
        {
            isTransitioning = true;
            GamePage current = (currentPageIndex >= 0 && currentPageIndex < pages.Length) ? pages[currentPageIndex] : null;
            GamePage next = (targetIndex < pages.Length) ? pages[targetIndex] : null;

            // 1. 현재 페이지 퇴장
            if (current != null)
            {
                yield return StartCoroutine(FadePage(current, 1f, 0f));
                current.OnExit();
            }

            // 2. 다음 페이지 준비
            if (next != null)
            {
                next.OnEnter();
                HandleTriggerInfo(next, info); // 정보 전달
                
                // 3. 다음 페이지 등장
                yield return StartCoroutine(FadePage(next, 0f, 1f));
            }

            currentPageIndex = targetIndex;
            isTransitioning = false;
        }

        private void HandleTriggerInfo(GamePage page, int triggerInfo)
        {
            if (triggerInfo == 0) return;
            // TutorialPage3Controller 등에서 트리거 처리
            if (page is TutorialPage3Controller p3)
            {
                if (triggerInfo == 1) p3.ActivatePlayerCheck(true);
                else if (triggerInfo == 2) p3.ActivatePlayerCheck(false);
            }
        }
    }
}