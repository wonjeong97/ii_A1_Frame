using System;
using UnityEngine;
using My.Scripts.Global;
using Wonjeong.Utils;

namespace My.Scripts.Core.Data
{
    public interface ILevelSetting
    {
        GridPageData Page1 { get; set; }
        QnAPageData Page2 { get; set; }
        TransitionPageData Page4 { get; set; } 
        TransitionPageData Page5 { get; set; } 
    }

    [Serializable]
    public class StandardLevelSetting : ILevelSetting
    {
        public GridPageData page1;
        public QnAPageData page2;
        public TransitionPageData page4; 
        public TransitionPageData page6; 
        
        public GridPageData Page1 { get => page1; set => page1 = value; }
        public QnAPageData Page2 { get => page2; set => page2 = value; }
        
        public TransitionPageData Page4 { get => page4; set => page4 = value; }
        public TransitionPageData Page5 { get => page6; set => page6 = value; }
    }

    [Serializable]
    public class TutorialLevelSetting : ILevelSetting
    {
        public GridPageData page1;
        public QnAPageData page2;
        public TransitionPageData page4;
        public TransitionPageData page6;
        public TransitionPageData page7;
        public TutorialPage8Data page8;

        public GridPageData Page1 { get => page1; set => page1 = value; }
        public QnAPageData Page2 { get => page2; set => page2 = value; }
        public TransitionPageData Page4 { get => page4; set => page4 = value; }
        public TransitionPageData Page5 { get => page6; set => page6 = value; }
        
        public TransitionPageData Page7 { get => page7; set => page7 = value; }
        public TutorialPage8Data Page8 { get => page8; set => page8 = value; }
    }

    public static class LevelDataLoader
    {
        public static StandardLevelSetting LoadStandardLevel(string levelID, UserType levelType)
        {
            StandardLevelSetting commonData = JsonLoader.Load<StandardLevelSetting>("JSON/PlayCommon");
            
            // UserType(예: A1, C3)에서 카트리지 문자와 관계 숫자를 분리하여 새로운 폴더 경로를 생성합니다.
            string typeStr = levelType.ToString();
            string cartridge = typeStr.Substring(0, 1); 
            string relation = typeStr.Substring(1);     
            
            string path = $"JSON/Cartridge_{cartridge}/{relation}/Play{levelID}_{levelType}";
            
            StandardLevelSetting specificData = JsonLoader.Load<StandardLevelSetting>(path);

            if (specificData == null)
            {
                Debug.LogError($"[LevelDataLoader] 레벨 데이터를 찾을 수 없습니다: {path}");
                return null;
            }

            MergeCommonData(specificData, commonData);
            return specificData;
        }

        public static TutorialLevelSetting LoadTutorialLevel()
        {
            StandardLevelSetting commonData = JsonLoader.Load<StandardLevelSetting>("JSON/PlayCommon");
            TutorialLevelSetting specificData = JsonLoader.Load<TutorialLevelSetting>(GameConstants.Path.PlayTutorial);

            if (specificData == null)
            {
                Debug.LogError($"[LevelDataLoader] 튜토리얼 데이터를 찾을 수 없습니다: {GameConstants.Path.PlayTutorial}");
                return null;
            }

            MergeCommonData(specificData, commonData);
            return specificData;
        }

        private static void MergeCommonData(ILevelSetting specific, StandardLevelSetting common)
        {   
            if (common == null) return;

            if (specific.Page1 == null) specific.Page1 = new GridPageData();
            if (common.Page1 != null)
            {
                if (specific.Page1.descriptionText1 == null || string.IsNullOrEmpty(specific.Page1.descriptionText1.text)) specific.Page1.descriptionText1 = common.Page1.descriptionText1;
                if (specific.Page1.descriptionText2 == null || string.IsNullOrEmpty(specific.Page1.descriptionText2.text)) specific.Page1.descriptionText2 = common.Page1.descriptionText2;
                if (specific.Page1.descriptionText3 == null || string.IsNullOrEmpty(specific.Page1.descriptionText3.text)) specific.Page1.descriptionText3 = common.Page1.descriptionText3;
                if (string.IsNullOrEmpty(specific.Page1.warningMessage)) specific.Page1.warningMessage = common.Page1.warningMessage;
                if (string.IsNullOrEmpty(specific.Page1.resetMessage)) specific.Page1.resetMessage = common.Page1.resetMessage;
            }

            if (specific.Page2 == null) specific.Page2 = new QnAPageData();
            if (common.Page2 != null)
            {
                if (specific.Page2.descriptionText == null || string.IsNullOrEmpty(specific.Page2.descriptionText.text)) specific.Page2.descriptionText = common.Page2.descriptionText;
                if (specific.Page2.answerTexts == null || specific.Page2.answerTexts.Length == 0) specific.Page2.answerTexts = common.Page2.answerTexts;
                if (string.IsNullOrEmpty(specific.Page2.warningMessage)) specific.Page2.warningMessage = common.Page2.warningMessage;
                if (string.IsNullOrEmpty(specific.Page2.resetMessage)) specific.Page2.resetMessage = common.Page2.resetMessage;
                
                if (specific.Page2.nicknamePlayerA == null || string.IsNullOrEmpty(specific.Page2.nicknamePlayerA.text)) 
                    specific.Page2.nicknamePlayerA = common.Page2.nicknamePlayerA;
                
                if (specific.Page2.nicknamePlayerB == null || string.IsNullOrEmpty(specific.Page2.nicknamePlayerB.text)) 
                    specific.Page2.nicknamePlayerB = common.Page2.nicknamePlayerB;
            }

            if (specific.Page4 == null) specific.Page4 = new TransitionPageData();
            if (common.Page4 != null)
            {
                if (specific.Page4.descriptionText == null || string.IsNullOrEmpty(specific.Page4.descriptionText.text))
                    specific.Page4.descriptionText = common.Page4.descriptionText;
                if (string.IsNullOrEmpty(specific.Page4.warningMessage)) specific.Page4.warningMessage = common.Page4.warningMessage;
                if (string.IsNullOrEmpty(specific.Page4.resetMessage)) specific.Page4.resetMessage = common.Page4.resetMessage;
            }


            if (specific.Page5 == null) specific.Page5 = new TransitionPageData();
            if (common.Page5 != null)
            {
                if (specific.Page5.descriptionText == null || string.IsNullOrEmpty(specific.Page5.descriptionText.text))
                    specific.Page5.descriptionText = common.Page5.descriptionText;
                if (string.IsNullOrEmpty(specific.Page5.warningMessage)) specific.Page5.warningMessage = common.Page5.warningMessage;
                if (string.IsNullOrEmpty(specific.Page5.resetMessage)) specific.Page5.resetMessage = common.Page5.resetMessage;
            }
        }
    }
}