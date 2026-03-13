using System;
using System.Collections.Generic;
using UnityEngine;
using Wonjeong.Data;

namespace My.Scripts.Core.Data
{
    [Serializable]
    public class GridPageData
    {
        public TextSetting descriptionText1; 
        public TextSetting descriptionText2; 
        public TextSetting descriptionText3; 
        public TextSetting[] questions; 
        public List<Vector2Int> questionSpots; 
        public string warningMessage;
        public string resetMessage;
    }

    /// <summary> [Page 2] Q&A 및 완료 대기(Check) 통합 데이터 </summary>
    [Serializable]
    public class QnAPageData
    {
        public TextSetting descriptionText; 
        public TextSetting questionText; 
        public TextSetting[] answerTexts; 
        
        public TextSetting nicknamePlayerA; 
        public TextSetting nicknamePlayerB; 
        
        public string warningMessage;
        public string resetMessage;
    }

    [Serializable]
    public class TransitionPageData
    {
        public TextSetting descriptionText; 
        public TextSetting playerAName; 
        public TextSetting playerBName; 
        public string warningMessage;
        public string resetMessage;
    }
    
    [Serializable]
    public class CameraPageData { }
    
    [Serializable]
    public class TutorialPage8Data
    {
        public TextSetting introText; 
        public TextSetting countdownText; 
        public TextSetting startText; 
    }
}