using System;
using UnityEngine;
using Wonjeong.Data;

namespace My.Scripts.Core.Data
{
    // [Page 1] 그리드 게임용 데이터
    [Serializable]
    public class GridPageData
    {
        public TextSetting descriptionText1;
        public TextSetting descriptionText2; // 경고/안내 멘트
        public TextSetting descriptionText3; // 시간 초과 경고
        public TextSetting[] questions;
    }

    // [Page 2] Q&A용 데이터
    [Serializable]
    public class QnAPageData
    {
        public TextSetting descriptionText;
        public TextSetting questionText;
        public TextSetting[] answerTexts;
    }

    // [Page 3] 체크(불 켜기)용 데이터
    [Serializable]
    public class CheckPageData
    {
        public TextSetting nicknamePlayerA;
        public TextSetting nicknamePlayerB;
    }

    // [Page 4, 6, 7] 단순 텍스트/전환용 데이터
    [Serializable]
    public class TransitionPageData
    {
        public TextSetting descriptionText;
        public TextSetting playerAName; // Q1 Intro용 (옵션)
        public TextSetting playerBName; // Q1 Intro용 (옵션)
    }
    
    // [Page 5] 카메라는 별도 데이터 없음
    [Serializable]
    public class CameraPageData { }
}