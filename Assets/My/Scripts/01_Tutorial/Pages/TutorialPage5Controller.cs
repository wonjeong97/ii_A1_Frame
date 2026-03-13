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
    /// <summary> 튜토리얼 5페이지용 데이터 구조체 </summary>
    [Serializable]
    public class TutorialPage5Data
    {
        public TextSetting descriptionText; 
    }

    /// <summary> 
    /// 튜토리얼 5페이지 컨트롤러.
    /// 유저의 추가 조작 없이 안내 텍스트를 보여준 뒤, 지정된 시간(5초) 후 자동으로 다음 페이지로 전환합니다.
    /// </summary>
    public class TutorialPage5Controller : GamePage<TutorialPage5Data>
    {
        [Header("Page 5 UI")]
        [SerializeField] private Text descriptionText; 
        
        private Coroutine autoNextStepRoutine;

        /// <summary> JSON에서 로드한 안내 텍스트 데이터 주입 </summary>
        protected override void SetupData(TutorialPage5Data data)
        {
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
        }

        /// <summary> 페이지 진입 시 중복 실행을 방지하고 자동 전환(타이머) 코루틴 시작 </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            if (autoNextStepRoutine != null) StopCoroutine(autoNextStepRoutine);
            autoNextStepRoutine = StartCoroutine(AutoNextStep());
        }

        /// <summary> 유저가 텍스트를 충분히 읽고 인지할 수 있도록 일정시간 대기 후 완료 신호 전송 </summary>
        private IEnumerator AutoNextStep()
        {
            yield return CoroutineData.GetWaitForSeconds(4f);
            CompleteStep();
        }

        /// <summary> 페이지 퇴장 시 진행 중인 자동 전환 코루틴을 강제 중단하여 안전하게 메모리 해제 </summary>
        public override void OnExit()
        {
            if (autoNextStepRoutine != null)
            {
                StopCoroutine(autoNextStepRoutine);
                autoNextStepRoutine = null;
            }
            base.OnExit();
        }
    }
}