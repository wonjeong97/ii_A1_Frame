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

    // GamePage<TutorialPage6Data> 상속
    public class TutorialPage6Controller : GamePage<TutorialPage6Data>
    {
        [Header("Page 6 UI")]
        [SerializeField] private Text text1;
        [SerializeField] private Text text2;

        // SetupData 오버라이드
        protected override void SetupData(TutorialPage6Data data)
        {
            if (text1) UIManager.Instance.SetText(text1.gameObject, data.descriptionText1);
            if (text2) UIManager.Instance.SetText(text2.gameObject, data.descriptionText2);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            StartCoroutine(EndSequence());
        }

        private IEnumerator EndSequence()
        {
            yield return new WaitForSeconds(4.0f);
            CompleteStep();
        }
    }
}