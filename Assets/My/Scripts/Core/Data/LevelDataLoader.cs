using System;
using UnityEngine;
using My.Scripts.Global;
using Wonjeong.Utils;

namespace My.Scripts.Core.Data
{
    /// <summary>
    /// 공통 레벨 설정 인터페이스.
    /// </summary>
    public interface ILevelSetting
    {
        GridPageData Page1 { get; set; }
        QnAPageData Page2 { get; set; }
        TransitionPageData Page4 { get; set; }
        TransitionPageData Page5 { get; set; }
        TransitionPageData Page6 { get; set; }
    }

    /// <summary>
    /// 일반 레벨용 데이터 컨테이너.
    /// </summary>
    [Serializable]
    public class StandardLevelSetting : ILevelSetting
    {
        public GridPageData page1;
        public QnAPageData page2;
        public TransitionPageData page4;
        public TransitionPageData page5;
        public TransitionPageData page6;

        public GridPageData Page1 { get => page1; set => page1 = value; }
        public QnAPageData Page2 { get => page2; set => page2 = value; }

        public TransitionPageData Page4 { get => page4; set => page4 = value; }
        public TransitionPageData Page5 { get => page5; set => page5 = value; }
        public TransitionPageData Page6 { get => page6; set => page6 = value; }
    }

    /// <summary>
    /// 튜토리얼 레벨용 확장 데이터 컨테이너.
    /// </summary>
    [Serializable]
    public class TutorialLevelSetting : ILevelSetting
    {
        public GridPageData page1;
        public QnAPageData page2;
        public TransitionPageData page4;
        public TransitionPageData page5;
        public TransitionPageData page6;
        public TransitionPageData page7;
        public TutorialPage8Data page8;

        public GridPageData Page1 { get => page1; set => page1 = value; }
        public QnAPageData Page2 { get => page2; set => page2 = value; }
        public TransitionPageData Page4 { get => page4; set => page4 = value; }
        public TransitionPageData Page5 { get => page5; set => page5 = value; }
        public TransitionPageData Page6 { get => page6; set => page6 = value; }

        public TransitionPageData Page7 { get => page7; set => page7 = value; }
        public TutorialPage8Data Page8 { get => page8; set => page8 = value; }
    }

    /// <summary>
    /// 레벨 데이터를 로드하고 공통 데이터를 병합함.
    /// </summary>
    public static class LevelDataLoader
    {
        /// <summary>
        /// 일반 레벨 데이터를 JSON에서 로드하고 병합함.
        /// </summary>
        /// <param name="levelID">레벨 식별자</param>
        /// <param name="levelType">유저 타입 정보</param>
        /// <returns>병합된 레벨 설정 객체</returns>
        public static StandardLevelSetting LoadStandardLevel(string levelID, UserType levelType)
        {
            // 1. 공통 데이터 로드 경로 수정
            string commonPath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.PlayCommon);
            StandardLevelSetting commonData = JsonLoader.Load<StandardLevelSetting>(commonPath);

            string typeStr = levelType.ToString();
            string cartridge = typeStr.Substring(0, 1);
            string relation = typeStr.Substring(1);
            string lang = SessionManager.Instance ? SessionManager.Instance.CurrentLanguage : "ko";

            // 2. 카트리지 데이터 경로에 언어(lang) 추가
            // 예: "JSON/ko/Cartridge_A/1/PlayQ1_A1"
            string path = $"JSON/{lang}/Cartridge_{cartridge}/{relation}/Play{levelID}_{levelType}";
            StandardLevelSetting specificData = JsonLoader.Load<StandardLevelSetting>(path);

            if (specificData == null)
            {
                string fallbackType = $"{cartridge}1";
                string fallbackPath = $"JSON/{lang}/Cartridge_{cartridge}/1/Play{levelID}_{fallbackType}";
                Debug.LogWarning($"JSON 누락됨: {path}. 폴백 적용 -> {fallbackPath}");
                specificData = JsonLoader.Load<StandardLevelSetting>(fallbackPath);
            }

            if (specificData == null)
            {
                Debug.LogError($"레벨 데이터 로드 실패. 경로: {path}");
                return null;
            }

            MergeCommonData(specificData, commonData);
            return specificData;
        }

        /// <summary>
        /// 튜토리얼 레벨 데이터를 JSON에서 로드하고 병합함.
        /// </summary>
        /// <returns>병합된 튜토리얼 설정 객체</returns>
        public static TutorialLevelSetting LoadTutorialLevel()
        {
            string commonPath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.PlayCommon);
            StandardLevelSetting commonData = JsonLoader.Load<StandardLevelSetting>(commonPath);

            string tutorialPath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.PlayTutorial);
            TutorialLevelSetting specificData = JsonLoader.Load<TutorialLevelSetting>(tutorialPath);

            if (specificData == null)
            {
                Debug.LogError($"튜토리얼 데이터 로드 실패. 경로: {tutorialPath}");
                return null;
            }

            MergeCommonData(specificData, commonData);
            return specificData;
        }

        /// <summary>
        /// 개별 데이터의 누락된 항목을 공통 데이터로 덮어씌움.
        /// </summary>
        /// <param name="specific">개별 레벨 설정</param>
        /// <param name="common">공통 레벨 설정</param>
        private static void MergeCommonData(ILevelSetting specific, StandardLevelSetting common)
        {
            if (common == null) return;

            // # TODO: 필드가 늘어날 경우를 대비해 리플렉션(Reflection) 기반의 자동 병합 로직 고려
            if (specific.Page1 == null)
            {
                Debug.LogWarning("Page1 데이터 누락됨.");
            }
            else if (common.Page1 != null)
            {
                if (specific.Page1.descriptionText1 == null || string.IsNullOrEmpty(specific.Page1.descriptionText1.text)) specific.Page1.descriptionText1 = common.Page1.descriptionText1;
                if (specific.Page1.descriptionText2 == null || string.IsNullOrEmpty(specific.Page1.descriptionText2.text)) specific.Page1.descriptionText2 = common.Page1.descriptionText2;
                if (specific.Page1.descriptionText3 == null || string.IsNullOrEmpty(specific.Page1.descriptionText3.text)) specific.Page1.descriptionText3 = common.Page1.descriptionText3;
                if (specific.Page1.failText == null || string.IsNullOrEmpty(specific.Page1.failText.text)) specific.Page1.failText = common.Page1.failText;
                if (string.IsNullOrEmpty(specific.Page1.warningMessage)) specific.Page1.warningMessage = common.Page1.warningMessage;
                if (string.IsNullOrEmpty(specific.Page1.resetMessage)) specific.Page1.resetMessage = common.Page1.resetMessage;
            }

            if (specific.Page2 == null)
            {
                Debug.LogWarning("Page2 데이터 누락됨.");
            }
            else if (common.Page2 != null)
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

            if (specific.Page4 == null)
            {
                Debug.LogWarning("Page4 데이터 누락됨.");
            }
            else if (common.Page4 != null)
            {
                if (specific.Page4.descriptionText == null || string.IsNullOrEmpty(specific.Page4.descriptionText.text))
                    specific.Page4.descriptionText = common.Page4.descriptionText;
                if (string.IsNullOrEmpty(specific.Page4.warningMessage)) specific.Page4.warningMessage = common.Page4.warningMessage;
                if (string.IsNullOrEmpty(specific.Page4.resetMessage)) specific.Page4.resetMessage = common.Page4.resetMessage;
            }

            if (specific.Page5 == null)
            {
                Debug.LogWarning("Page5 데이터 누락됨.");
            }
            else if (common.Page5 != null)
            {
                if (specific.Page5.descriptionText == null || string.IsNullOrEmpty(specific.Page5.descriptionText.text))
                    specific.Page5.descriptionText = common.Page5.descriptionText;
                if (string.IsNullOrEmpty(specific.Page5.warningMessage)) specific.Page5.warningMessage = common.Page5.warningMessage;
                if (string.IsNullOrEmpty(specific.Page5.resetMessage)) specific.Page5.resetMessage = common.Page5.resetMessage;
            }

            if (specific.Page6 == null)
            {
                Debug.LogWarning("Page6 데이터 누락됨.");
            }
            else if (common.Page6 != null)
            {
                if (specific.Page6.descriptionText == null || string.IsNullOrEmpty(specific.Page6.descriptionText.text))
                    specific.Page6.descriptionText = common.Page6.descriptionText;
                if (string.IsNullOrEmpty(specific.Page6.warningMessage)) specific.Page6.warningMessage = common.Page6.warningMessage;
                if (string.IsNullOrEmpty(specific.Page6.resetMessage)) specific.Page6.resetMessage = common.Page6.resetMessage;
            }
        }
    }
}