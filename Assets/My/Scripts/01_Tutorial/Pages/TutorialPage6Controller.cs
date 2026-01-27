using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage6Data
    {
        public TextSetting descriptionText1;
        public TextSetting descriptionText2;
    }

    /// <summary> 튜토리얼 6페이지 컨트롤러 (종료 대기) </summary>
    public class TutorialPage6Controller : GamePage<TutorialPage6Data>
    {
        [Header("Page 6 UI")]
        [SerializeField] private Text text1; // 설명 텍스트 1
        [SerializeField] private Text text2; // 설명 텍스트 2

        /// <summary> 데이터 설정 (텍스트 적용) </summary>
        protected override void SetupData(TutorialPage6Data data)
        {
            if (text1) UIManager.Instance.SetText(text1.gameObject, data.descriptionText1);
            if (text2) UIManager.Instance.SetText(text2.gameObject, data.descriptionText2);
        }

        /// <summary> 페이지 진입 (종료 시퀀스 시작) </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            StartCoroutine(EndSequence());
        }

        /// <summary> 4초 대기 후 완료 처리 </summary>
        private IEnumerator EndSequence()
        {
            yield return new WaitForSeconds(4.0f);
            CompleteStep();
        }
    }
}