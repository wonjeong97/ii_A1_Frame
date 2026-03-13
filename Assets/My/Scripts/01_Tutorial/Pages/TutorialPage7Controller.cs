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
    /// <summary> 튜토리얼 7페이지용 데이터 구조체 </summary>
    [Serializable]
    public class TutorialPage7Data
    {
        public TextSetting descriptionText1;
        public TextSetting descriptionText2;
    }

    /// <summary> 
    /// 튜토리얼 7페이지 컨트롤러.
    /// 유저 조작 없이 두 개의 안내 텍스트를 보여준 뒤, 지정된 시간 후 자동으로 다음 페이지로 전환합니다.
    /// </summary>
    public class TutorialPage7Controller : GamePage<TutorialPage7Data>
    {
        [Header("Page 7 UI")]
        [SerializeField] private Text text1; 
        [SerializeField] private Text text2; 
        
        private Coroutine _endSequenceRoutine;

        /// <summary> JSON에서 로드한 두 개의 안내 텍스트 데이터 주입 </summary>
        protected override void SetupData(TutorialPage7Data data)
        {
            if (text1) UIManager.Instance.SetText(text1.gameObject, data.descriptionText1);
            if (text2) UIManager.Instance.SetText(text2.gameObject, data.descriptionText2);
        }

        /// <summary> 페이지 진입 시 중복 실행을 방지하고 자동 전환(타이머) 코루틴 시작 </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            if (_endSequenceRoutine != null)
            {
                StopCoroutine(_endSequenceRoutine);
            }
            _endSequenceRoutine = StartCoroutine(EndSequence());
        }

        /// <summary> 유저가 텍스트를 충분히 인지할 수 있도록 3초간 대기 후 완료 신호 전송 </summary>
        private IEnumerator EndSequence()
        {
            yield return CoroutineData.GetWaitForSeconds(3.0f);
            CompleteStep();
            _endSequenceRoutine = null; 
        }

        /// <summary> 페이지 퇴장 시 진행 중인 대기 코루틴을 강제 중단하여 안전하게 메모리 해제 </summary>
        public override void OnExit()
        {
            if (_endSequenceRoutine != null)
            {
                StopCoroutine(_endSequenceRoutine);
                _endSequenceRoutine = null;
            }
            base.OnExit();
        }
    }
}