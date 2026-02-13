namespace My.Scripts.Global
{
    /// <summary> 게임 전역 상수 관리 클래스 </summary>
    public static class GameConstants
    {
        /// <summary> 씬 이름 상수 모음 </summary>
        public static class Scene
        { 
            public const string Title = "00_Title"; // 타이틀 씬
            public const string Tutorial = "01_Tutorial"; // 튜토리얼 씬
            public const string PlayTutorial = "02_Play_Tutorial"; // 플레이 튜토리얼 씬
            
            public const string PlayQ1_A = "Play_Q1_A"; // 질문 1 씬
            public const string PlayQ2_A = "Play_Q2_A"; // 질문 2 씬
            public const string PlayQ3_A = "Play_Q3_A"; // 질문 3 씬
            public const string PlayQ4_A = "Play_Q4_A"; // 질문 4 씬
            public const string PlayQ5_A = "Play_Q5_A"; // 질문 5 씬
            public const string PlayQ6_A = "Play_Q6_A"; // 질문 6 씬
            public const string PlayQ7_A = "Play_Q7_A"; // 질문 7 씬
            public const string PlayQ8_A = "Play_Q8_A"; // 질문 8 씬
            public const string PlayQ9_A = "Play_Q9_A"; // 질문 9 씬
            public const string PlayQ10_A = "Play_Q10_A"; // 질문 10 씬
            public const string PlayQ11_A = "Play_Q11_A"; // 질문 11 씬
            public const string PlayQ12_A = "Play_Q12_A"; // 질문 12 씬
            public const string PlayQ13_A = "Play_Q13_A"; // 질문 13 씬
            public const string PlayQ14_A = "Play_Q14_A"; // 질문 14 씬
            public const string PlayQ15_A = "Play_Q15_A"; // 질문 15 씬
            
            public const string Ending = "18_Ending"; // 엔딩 씬
        }

        /// <summary> 리소스 경로 상수 모음 </summary>
        public static class Path
        {
            public const string JsonSetting = "Settings"; // 기본 설정 JSON
            public const string Title = "JSON/Title"; // 타이틀 데이터
            public const string Tutorial = "JSON/Tutorial"; // 튜토리얼 데이터
            public const string PlayTutorial = "JSON/PlayTutorial"; // 플레이 튜토리얼 데이터
            public const string Ending = "JSON/Ending"; // 엔딩 데이터
        }
    }
}