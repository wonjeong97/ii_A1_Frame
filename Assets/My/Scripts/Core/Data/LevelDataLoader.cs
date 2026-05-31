using System;
using UnityEngine;
using My.Scripts.Global;
using Wonjeong.Data;
using Wonjeong.Utils;
using Cysharp.Text; 
using VContainer;   

namespace My.Scripts.Core.Data
{
    public interface ILevelSetting
    {
        GridPageData Page1 { get; set; }
        QnAPageData Page2 { get; set; }
        TransitionPageData Page4 { get; set; }
        TransitionPageData Page5 { get; set; }
        TransitionPageData Page6 { get; set; }
    }

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
    /// 레벨 데이터를 로드하고 공통 데이터를 병합하는 데이터 로더 서비스 클래스.
    /// 튜토리얼 다국어 락인 버그 해결 및 제로 얼로케이션 병합이 적용되었습니다.
    /// </summary>
    public class LevelDataLoader
    {
        private readonly SessionManager _sessionManager;

        [Inject]
        public LevelDataLoader(SessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        /// <summary>
        /// 일반 레벨 데이터를 JSON에서 로드하고 공통 데이터를 병합함.
        /// </summary>
        public StandardLevelSetting LoadStandardLevel(string levelID, UserType levelType)
        {
            string lang = string.IsNullOrWhiteSpace(_sessionManager?.CurrentLanguage) ? "ko" : _sessionManager.CurrentLanguage.Trim();
            string commonPath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.PlayCommon, lang);
            StandardLevelSetting commonData = JsonLoader.Load<StandardLevelSetting>(commonPath);

            string typeStr = levelType.ToString();
            char cartridge = typeStr[0]; 
            char relation = typeStr[1];  

            string path = ZString.Format("JSON/{0}/Cartridge_{1}/{2}/Play{3}_{4}.json", lang, cartridge, relation, levelID, typeStr);
            StandardLevelSetting specificData = JsonLoader.Load<StandardLevelSetting>(path);
            
            // 파일이 없어 빈 객체가 반환된 경우를 대비해 Page2 존재 여부로 실패를 판단.
            if (specificData == null || specificData.Page2 == null)
            {
                string fallbackPath = ZString.Format("JSON/{0}/Cartridge_{1}/1/Play{2}_{3}1.json", lang, cartridge, levelID, cartridge);
                Debug.LogWarning($"JSON 누락됨: {path}. 폴백 적용 -> {fallbackPath}");
                specificData = JsonLoader.Load<StandardLevelSetting>(fallbackPath);
            }

            // 폴백 이후에도 유효한 데이터가 없으면 로드 실패 처리.
            if (specificData == null || specificData.Page2 == null)
            {
                Debug.LogError($"레벨 데이터 로드 실패. 최종 경로: {path}");
                return null;
            }

            MergeCommonData(specificData, commonData);
            return specificData;
        }

        /// <summary> 튜토리얼 레벨 데이터를 JSON에서 로드하고 병합함. </summary>
        public TutorialLevelSetting LoadTutorialLevel()
        {
            // 세션 매니저로부터 현재 국가 코드를 실시간 추출하여 적용
            string lang = string.IsNullOrWhiteSpace(_sessionManager?.CurrentLanguage) ? "ko" : _sessionManager.CurrentLanguage.Trim();
            string commonPath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.PlayCommon, lang);
            StandardLevelSetting commonData = JsonLoader.Load<StandardLevelSetting>(commonPath);

            string tutorialPath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.PlayTutorial, lang);
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
        /// 공통 설정을 개별 설정 데이터와 결합합니다.
        /// </summary>
        private void MergeCommonData(ILevelSetting specific, StandardLevelSetting common)
        {
            if (common == null) return;

            if (specific.Page1 != null) MergeGridData(specific.Page1, common.Page1);
            else Debug.LogWarning("Page1 데이터 누락됨.");

            if (specific.Page2 != null) MergeQnAData(specific.Page2, common.Page2);
            else Debug.LogWarning("Page2 데이터 누락됨.");

            if (specific.Page4 != null) MergeTransitionData(specific.Page4, common.Page4);
            else Debug.LogWarning("Page4 데이터 누락됨.");

            if (specific.Page5 != null) MergeTransitionData(specific.Page5, common.Page5);
            else Debug.LogWarning("Page5 데이터 누락됨.");

            if (specific.Page6 != null) MergeTransitionData(specific.Page6, common.Page6);
            else Debug.LogWarning("Page6 데이터 누락됨.");
        }

        private void MergeGridData(GridPageData target, GridPageData source)
        {
            if (source == null) return;

            target.descriptionText1 = GetTextFallback(target.descriptionText1, source.descriptionText1);
            target.descriptionText2 = GetTextFallback(target.descriptionText2, source.descriptionText2);
            target.descriptionText3 = GetTextFallback(target.descriptionText3, source.descriptionText3);
            target.failText = GetTextFallback(target.failText, source.failText);
            
            target.warningMessage = GetStringFallback(target.warningMessage, source.warningMessage);
            target.resetMessage = GetStringFallback(target.resetMessage, source.resetMessage);
        }

        private void MergeQnAData(QnAPageData target, QnAPageData source)
        {
            if (source == null) return;

            target.descriptionText = GetTextFallback(target.descriptionText, source.descriptionText);
            target.nicknamePlayerA = GetTextFallback(target.nicknamePlayerA, source.nicknamePlayerA);
            target.nicknamePlayerB = GetTextFallback(target.nicknamePlayerB, source.nicknamePlayerB);
            
            if (target.answerTexts == null || target.answerTexts.Length == 0)
            {
                target.answerTexts = source.answerTexts;
            }

            target.warningMessage = GetStringFallback(target.warningMessage, source.warningMessage);
            target.resetMessage = GetStringFallback(target.resetMessage, source.resetMessage);
        }

        private void MergeTransitionData(TransitionPageData target, TransitionPageData source)
        {
            if (source == null) return;

            target.descriptionText = GetTextFallback(target.descriptionText, source.descriptionText);
            target.warningMessage = GetStringFallback(target.warningMessage, source.warningMessage);
            target.resetMessage = GetStringFallback(target.resetMessage, source.resetMessage);
        }

        private static TextSetting GetTextFallback(TextSetting target, TextSetting source)
        {
            if (source == null) return target;
            return (target == null || string.IsNullOrEmpty(target.text)) ? source : target;
        }

        private static string GetStringFallback(string target, string source)
        {
            return string.IsNullOrEmpty(target) ? source : target;
        }
    }
}