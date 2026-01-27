using System;
using UnityEngine;
using Wonjeong.Data;

namespace My.Scripts.Core.Data
{
    /// <summary> [Page 1] 그리드 게임용 데이터 </summary>
    [Serializable]
    public class GridPageData
    {
        public TextSetting descriptionText1; // 설명 텍스트 1
        public TextSetting descriptionText2; // 경고/안내 멘트
        public TextSetting descriptionText3; // 시간 초과 경고
        public TextSetting[] questions; // 질문 목록
    }

    /// <summary> [Page 2] Q&A용 데이터 </summary>
    [Serializable]
    public class QnAPageData
    {
        public TextSetting descriptionText; // 설명 텍스트
        public TextSetting questionText; // 질문 텍스트
        public TextSetting[] answerTexts; // 답변 텍스트 목록
    }

    /// <summary> [Page 3] 체크(불 켜기)용 데이터 </summary>
    [Serializable]
    public class CheckPageData
    {
        public TextSetting nicknamePlayerA; // 플레이어 A 닉네임
        public TextSetting nicknamePlayerB; // 플레이어 B 닉네임
    }

    /// <summary> [Page 4, 6, 7] 단순 텍스트/전환용 데이터 </summary>
    [Serializable]
    public class TransitionPageData
    {
        public TextSetting descriptionText; // 설명 텍스트
        public TextSetting playerAName; // Q1 Intro용 플레이어 A 이름 (옵션)
        public TextSetting playerBName; // Q1 Intro용 플레이어 B 이름 (옵션)
    }
    
    /// <summary> [Page 5] 카메라용 데이터 (빈 클래스) </summary>
    [Serializable]
    public class CameraPageData { }
}