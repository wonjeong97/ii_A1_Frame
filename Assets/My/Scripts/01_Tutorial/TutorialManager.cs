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

    /// <summary> 튜토리얼 진행 관리 매니저 </summary>
    public class TutorialManager : BaseFlowManager
    {
        /// <summary> 설정 로드 및 페이지 데이터 주입 </summary>
        protected override void LoadSettings()
        {
            var setting = JsonLoader.Load<TutorialSetting>(GameConstants.Path.Tutorial);
            if (setting == null)
            {
                Debug.LogError($"[TutorialManager] JSON Load Failed");
                return;
            }

            // 각 페이지에 데이터 주입
            if (pages.Length > 0 && pages[0] != null) pages[0].SetupData(setting.page1);
            if (pages.Length > 1 && pages[1] != null) pages[1].SetupData(setting.page2);
            if (pages.Length > 2 && pages[2] != null) pages[2].SetupData(setting.page3);
            if (pages.Length > 3 && pages[3] != null) pages[3].SetupData(setting.page4);
            if (pages.Length > 4 && pages[4] != null) pages[4].SetupData(setting.page5);
            if (pages.Length > 5 && pages[5] != null) pages[5].SetupData(setting.page6);
            if (pages.Length > 6 && pages[6] != null) pages[6].SetupData(setting.page7);

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.StopBGM();
                SoundManager.Instance.PlayBGM("MainBGM"); // BGM 처음부터 다시 재생
            }
        }

        /// <summary> 튜토리얼 종료 처리 (실전 플레이 씬 이동) </summary>
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

        /// <summary> 페이지 전환 연출 (정보 전달 포함) </summary>
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

            // 1. 현재 페이지 퇴장
            if (current)
            {
                yield return StartCoroutine(FadePage(current, 1f, 0f));
                current.OnExit();
            }

            // 2. 다음 페이지 준비
            if (next)
            {
                next.OnEnter();
                HandleTriggerInfo(next, info); // 정보 전달
                
                // 3. 다음 페이지 등장
                // 1페이지는 전체 페이드 없이 즉시 노출
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

        /// <summary> 페이지별 트리거 정보 전달 처리 </summary>
        private void HandleTriggerInfo(GamePage page, int triggerInfo)
        {
            if (triggerInfo == 0) return;
            
            // TutorialPage3: 플레이어 체크 정보 전달
            if (page is TutorialPage4Controller p3)
            {
                if (triggerInfo == 1) p3.ActivatePlayerCheck(true);
                else if (triggerInfo == 2) p3.ActivatePlayerCheck(false);
            }
        }
    }
}