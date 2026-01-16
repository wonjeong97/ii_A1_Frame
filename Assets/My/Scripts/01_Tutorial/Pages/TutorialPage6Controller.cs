using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
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

    public class TutorialPage6Controller : TutorialPageBase
    {
        [Header("Page 6 UI")]
        [SerializeField] private Text text1;
        [SerializeField] private Text text2;

        public override void SetupData(object data)
        {
            var pageData = data as TutorialPage6Data;
            if (pageData == null) return;

            if (text1) UIManager.Instance.SetText(text1.gameObject, pageData.descriptionText1);
            if (text2) UIManager.Instance.SetText(text2.gameObject, pageData.descriptionText2);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            // 시작하자마자 종료 시퀀스 가동
            StartCoroutine(EndSequence());
        }

        private IEnumerator EndSequence()
        {
            // 4초 동안 대기 (고정)
            yield return new WaitForSeconds(4.0f);
            
            // 완료 신호 -> 매니저가 전체 페이드 아웃 처리
            CompleteStep();
        }
    }
}