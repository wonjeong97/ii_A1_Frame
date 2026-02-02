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
    [Serializable]
    public class TutorialPage7Data
    {
        public TextSetting descriptionText1;
        public TextSetting descriptionText2;
    }

    /// <summary> 튜토리얼 7페이지 컨트롤러 </summary>
    public class TutorialPage7Controller : GamePage<TutorialPage7Data>
    {
        [Header("Page 7 UI")]
        [SerializeField] private Text text1; // 설명 텍스트 1
        [SerializeField] private Text text2; // 설명 텍스트 2
        
        private Coroutine _endSequenceRoutine;

        /// <summary> 데이터 설정 (텍스트 적용) </summary>
        protected override void SetupData(TutorialPage7Data data)
        {
            if (text1) UIManager.Instance.SetText(text1.gameObject, data.descriptionText1);
            if (text2) UIManager.Instance.SetText(text2.gameObject, data.descriptionText2);
        }

        /// <summary> 페이지 진입 (종료 시퀀스 시작) </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            // 재진입 시 중복 실행 방지
            if (_endSequenceRoutine != null)
            {
                StopCoroutine(_endSequenceRoutine);
            }
            _endSequenceRoutine = StartCoroutine(EndSequence());
        }

        /// <summary> 4초 대기 후 완료 처리 </summary>
        private IEnumerator EndSequence()
        {
            yield return CoroutineData.GetWaitForSeconds(4.0f);
            CompleteStep();
            _endSequenceRoutine = null; // 완료 후 참조 해제
        }

        // 페이지 퇴장 시 실행 중인 코루틴 정리
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