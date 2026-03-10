using System;
using UnityEngine;
using My.Scripts.Global;
using Wonjeong.Utils;

namespace My.Scripts.Core.Data
{
    /// <summary> 
    /// 레벨 구성에 필요한 페이지 데이터들을 추상화한 인터페이스입니다.
    /// 표준 레벨과 튜토리얼 레벨 간의 데이터 호환성을 보장하기 위해 사용합니다.
    /// </summary>
    public interface ILevelSetting
    {
        GridPageData Page1 { get; set; }
        QnAPageData Page2 { get; set; }
        CheckPageData Page3 { get; set; }
        TransitionPageData Page4 { get; set; }
        TransitionPageData Page6 { get; set; }
    }

    /// <summary> 일반 플레이 모드에서 사용하는 표준 레벨 설정 클래스입니다. </summary>
    [Serializable]
    public class StandardLevelSetting : ILevelSetting
    {
        public GridPageData page1;
        public QnAPageData page2;
        public CheckPageData page3;
        public TransitionPageData page4;
        public TransitionPageData page6;
        
        // 인터페이스 구현을 통해 데이터 접근성을 통일함
        public GridPageData Page1 { get => page1; set => page1 = value; }
        public QnAPageData Page2 { get => page2; set => page2 = value; }
        public CheckPageData Page3 { get => page3; set => page3 = value; }
        public TransitionPageData Page4 { get => page4; set => page4 = value; }
        public TransitionPageData Page6 { get => page6; set => page6 = value; }
    }

    /// <summary> 튜토리얼 모드에 특화된 추가 페이지들을 포함하는 레벨 설정 클래스입니다. </summary>
    [Serializable]
    public class TutorialLevelSetting : ILevelSetting
    {
        public GridPageData page1;
        public QnAPageData page2;
        public CheckPageData page3;
        public TransitionPageData page4;
        public TransitionPageData page6;
        public TransitionPageData page7;
        public TutorialPage8Data page8;

        public GridPageData Page1 { get => page1; set => page1 = value; }
        public QnAPageData Page2 { get => page2; set => page2 = value; }
        public CheckPageData Page3 { get => page3; set => page3 = value; }
        public TransitionPageData Page4 { get => page4; set => page4 = value; }
        public TransitionPageData Page6 { get => page6; set => page6 = value; }
        public TransitionPageData Page7 { get => page7; set => page7 = value; }
        public TutorialPage8Data Page8 { get => page8; set => page8 = value; }
    }

    /// <summary>
    /// 레벨별 JSON 데이터를 로드하고 공통 데이터(PlayCommon)와 병합하는 정적 로더 클래스입니다.
    /// 개별 레벨 파일의 데이터 중복을 방지하고 통합 관리하기 위해 사용합니다.
    /// </summary>
    public static class LevelDataLoader
    {
        /// <summary> 지정된 레벨 ID와 유저 타입에 맞는 JSON 데이터를 로드하고 공통 설정을 병합합니다. </summary>
        public static StandardLevelSetting LoadStandardLevel(string levelID, UserType levelType)
        {
            // 모든 레벨에 공통으로 들어가는 기본 가이드 문구 로드
            StandardLevelSetting commonData = JsonLoader.Load<StandardLevelSetting>("JSON/PlayCommon");
            
            // 해당 레벨 고유의 문구 및 설정 로드
            string path = $"JSON/{levelType}/Play{levelID}_{levelType}";
            StandardLevelSetting specificData = JsonLoader.Load<StandardLevelSetting>(path);

            if (specificData == null)
            {
                Debug.LogError($"[LevelDataLoader] 레벨 데이터를 찾을 수 없습니다: {path}");
                return null;
            }

            MergeCommonData(specificData, commonData);
            return specificData;
        }

        /// <summary> 튜토리얼 씬 전용 JSON 데이터를 로드하고 공통 설정을 병합합니다. </summary>
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

        /// <summary> 
        /// 고유 데이터(specific)에 값이 비어있는 경우 공통 데이터(common)를 덮어씌워 보완합니다. 
        /// JSON 파일마다 중복된 텍스트를 적지 않아도 되도록 설계되었습니다.
        /// </summary>
        private static void MergeCommonData(ILevelSetting specific, StandardLevelSetting common)
        {   
            if (common == null) return;

            // 1페이지(Grid): 고유 값이 없으면 공통 가이드/경고 문구 주입
            if (specific.Page1 == null) specific.Page1 = new GridPageData();
            if (common.Page1 != null)
            {
                if (specific.Page1.descriptionText1 == null || string.IsNullOrEmpty(specific.Page1.descriptionText1.text)) specific.Page1.descriptionText1 = common.Page1.descriptionText1;
                if (specific.Page1.descriptionText2 == null || string.IsNullOrEmpty(specific.Page1.descriptionText2.text)) specific.Page1.descriptionText2 = common.Page1.descriptionText2;
                if (specific.Page1.descriptionText3 == null || string.IsNullOrEmpty(specific.Page1.descriptionText3.text)) specific.Page1.descriptionText3 = common.Page1.descriptionText3;
                if (string.IsNullOrEmpty(specific.Page1.warningMessage)) specific.Page1.warningMessage = common.Page1.warningMessage;
                if (string.IsNullOrEmpty(specific.Page1.resetMessage)) specific.Page1.resetMessage = common.Page1.resetMessage;
            }

            // 2페이지(QnA): 공통 질문 텍스트 및 경고 팝업 설정 상속
            if (specific.Page2 == null) specific.Page2 = new QnAPageData();
            if (common.Page2 != null)
            {
                if (specific.Page2.descriptionText == null || string.IsNullOrEmpty(specific.Page2.descriptionText.text)) specific.Page2.descriptionText = common.Page2.descriptionText;
                if (specific.Page2.answerTexts == null || specific.Page2.answerTexts.Length == 0) specific.Page2.answerTexts = common.Page2.answerTexts;
                if (string.IsNullOrEmpty(specific.Page2.warningMessage)) specific.Page2.warningMessage = common.Page2.warningMessage;
                if (string.IsNullOrEmpty(specific.Page2.resetMessage)) specific.Page2.resetMessage = common.Page2.resetMessage;
            }

            // 3페이지(Check): 플레이어 닉네임 표시 및 대기 안내문 상속
            if (specific.Page3 == null) specific.Page3 = new CheckPageData();
            if (common.Page3 != null)
            {
                if (specific.Page3.nicknamePlayerA == null) specific.Page3.nicknamePlayerA = common.Page3.nicknamePlayerA;
                if (specific.Page3.nicknamePlayerB == null) specific.Page3.nicknamePlayerB = common.Page3.nicknamePlayerB;
                if (specific.Page3.waitText == null) specific.Page3.waitText = common.Page3.waitText;
                if (string.IsNullOrEmpty(specific.Page3.warningMessage)) specific.Page3.warningMessage = common.Page3.warningMessage;
                if (string.IsNullOrEmpty(specific.Page3.resetMessage)) specific.Page3.resetMessage = common.Page3.resetMessage;
            }

            // 4페이지(Transition): 전환 안내 텍스트 상속
            if (specific.Page4 == null) specific.Page4 = new TransitionPageData();
            if (common.Page4 != null)
            {
                if (specific.Page4.descriptionText == null) specific.Page4.descriptionText = common.Page4.descriptionText;
                if (string.IsNullOrEmpty(specific.Page4.warningMessage)) specific.Page4.warningMessage = common.Page4.warningMessage;
                if (string.IsNullOrEmpty(specific.Page4.resetMessage)) specific.Page4.resetMessage = common.Page4.resetMessage;
            }

            // 6페이지(Transition): 종료 안내 텍스트 상속
            if (specific.Page6 == null) specific.Page6 = new TransitionPageData();
            if (common.Page6 != null)
            {
                if (specific.Page6.descriptionText == null) specific.Page6.descriptionText = common.Page6.descriptionText;
            }
        }
    }
}