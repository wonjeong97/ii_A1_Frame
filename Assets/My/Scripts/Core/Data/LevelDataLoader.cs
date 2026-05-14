using System;
using UnityEngine;
using My.Scripts.Global;
using Wonjeong.Data;
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
        /// 개별 레벨 데이터의 누락된 항목을 공통 설정으로 보완함.
        /// </summary>
        private static void MergeCommonData(ILevelSetting specific, StandardLevelSetting common)
        {
            if (common == null)
            {
                return;
            }

            // 각 페이지별로 안전한 병합 처리를 수행함.
            SafeMerge(specific.Page1, common.Page1, MergeGridData, "Page1");
            SafeMerge(specific.Page2, common.Page2, MergeQnAData, "Page2");
            SafeMerge(specific.Page4, common.Page4, MergeTransitionData, "Page4");
            SafeMerge(specific.Page5, common.Page5, MergeTransitionData, "Page5");
            SafeMerge(specific.Page6, common.Page6, MergeTransitionData, "Page6");
        }

        /// <summary>
        /// 대상 객체의 존재 여부를 확인하고, 유효할 경우에만 병합 액션을 실행함.
        /// 데이터가 누락된 경우 일관된 형식의 경고 로그를 남김.
        /// </summary>
        private static void SafeMerge<T>(T target, T source, Action<T, T> mergeAction, string label) where T : class
        {
            if (target != null)
            {
                mergeAction(target, source);
            }
            else
            {
                Debug.LogWarning($"{label} 데이터 누락됨.");
            }
        }

        /// <summary> 그리드 페이지의 텍스트 및 메시지 설정을 병합함. </summary>
        private static void MergeGridData(GridPageData target, GridPageData source)
        {
            if (source == null) return;

            target.descriptionText1 = GetTextFallback(target.descriptionText1, source.descriptionText1);
            target.descriptionText2 = GetTextFallback(target.descriptionText2, source.descriptionText2);
            target.descriptionText3 = GetTextFallback(target.descriptionText3, source.descriptionText3);
            target.failText = GetTextFallback(target.failText, source.failText);
            
            target.warningMessage = GetStringFallback(target.warningMessage, source.warningMessage);
            target.resetMessage = GetStringFallback(target.resetMessage, source.resetMessage);
        }

        /// <summary> QnA 페이지의 질문, 답변 및 닉네임 설정을 병합함. </summary>
        private static void MergeQnAData(QnAPageData target, QnAPageData source)
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

        /// <summary> 트랜지션 안내 페이지의 텍스트 및 메시지 설정을 병합함. </summary>
        private static void MergeTransitionData(TransitionPageData target, TransitionPageData source)
        {
            if (source == null) return;

            target.descriptionText = GetTextFallback(target.descriptionText, source.descriptionText);
            target.warningMessage = GetStringFallback(target.warningMessage, source.warningMessage);
            target.resetMessage = GetStringFallback(target.resetMessage, source.resetMessage);
        }

        /// <summary>
        /// 대상 TextSetting이 null이거나 텍스트가 비어있을 경우 원본 데이터를 반환함.
        /// </summary>
        private static TextSetting GetTextFallback(TextSetting target, TextSetting source)
        {
            if (source == null) return target;
            return (target == null || string.IsNullOrEmpty(target.text)) ? source : target;
        }

        /// <summary>
        /// 문자열 값이 비어있을 경우 원본 문자열을 반환함.
        /// </summary>
        private static string GetStringFallback(string target, string source)
        {
            return string.IsNullOrEmpty(target) ? source : target;
        }
    }
}