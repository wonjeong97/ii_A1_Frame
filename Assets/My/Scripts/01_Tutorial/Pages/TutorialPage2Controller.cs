using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary> 튜토리얼 2페이지용 데이터 구조체 </summary>
    [Serializable]
    public class TutorialPage2Data
    {
        public TextSetting descriptionText;
    }

    /// <summary> 
    /// 튜토리얼 2페이지 컨트롤러.
    /// 본격적인 시작 전 메인 BGM으로 교체하고 지정된 시간 동안 대기한 후 자동 전환합니다.
    /// </summary>
    public class TutorialPage2Controller : GamePage<TutorialPage2Data>
    {
        [Header("Page 2 UI")]
        [SerializeField] private Text descriptionText; 

        /// <summary> JSON에서 로드한 텍스트 데이터 주입 </summary>
        protected override void SetupData(TutorialPage2Data data)
        {
            if (descriptionText) 
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            }
        }

        /// <summary> 페이지 진입 시 UI 표시 및 BGM 전환, 타이머 시작 </summary>
        public override void OnEnter()
        {
            base.OnEnter(); 
            
            // 페이드인 없이 즉시 텍스트 표시
            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = 1f;
                descriptionText.color = c;
            }

            // 타이틀 BGM 종료 후 메인 플레이 BGM으로 자연스럽게 교체
            if (SoundManager.Instance)
            {
                SoundManager.Instance.StopBGM();
                SoundManager.Instance.PlayBGM("MainBGM");
                
                SoundManager.Instance.PlaySFX("공통_6");
            }
            
            StartCoroutine(WaitAndNextRoutine());
        }

        /// <summary> 텍스트와 사운드 연출을 유저가 인지할 수 있도록 일정 시간 대기 </summary>
        private IEnumerator WaitAndNextRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(3.0f);

            CompleteStep();
        }
    }
}