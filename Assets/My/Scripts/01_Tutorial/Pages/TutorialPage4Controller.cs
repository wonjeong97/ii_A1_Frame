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
    public class TutorialPage4Data
    {
        public TextSetting descriptionText;
    }

    // GamePage<TutorialPage4Data> 상속
    public class TutorialPage4Controller : GamePage<TutorialPage4Data>
    {
        [Header("Page 4 UI")]
        [SerializeField] private Text descriptionText;

        // SetupData 오버라이드
        protected override void SetupData(TutorialPage4Data data)
        {
            if (descriptionText) 
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            StartCoroutine(AutoNextStep());
        }

        private IEnumerator AutoNextStep()
        {
            yield return new WaitForSeconds(5.0f);
            CompleteStep();
        }
    }
}