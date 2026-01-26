using System;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage2Data
    {
        public TextSetting descriptionText;
        public TextSetting nicknamePlayerA;
        public TextSetting nicknamePlayerB;
    }

    // GamePage<TutorialPage2Data> 상속
    public class TutorialPage2Controller : GamePage<TutorialPage2Data>
    {
        [Header("Page 2 UI")]
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text nicknameA;
        [SerializeField] private Text nicknameB;

        // SetupData 오버라이드
        protected override void SetupData(TutorialPage2Data data)
        {
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                CompleteStep(1); 
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                CompleteStep(2); 
            }
        }
    }
}