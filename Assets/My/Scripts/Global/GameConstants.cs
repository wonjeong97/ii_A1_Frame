namespace My.Scripts.Global
{
    /// <summary> 게임 전역 상수 관리 클래스 </summary>
    public static class GameConstants
    {
        /// <summary> 씬 이름 상수 모음 </summary>
        public static class Scene
        { 
            public const string Title = "00_Title"; 
            public const string Tutorial = "01_Tutorial"; 
            public const string PlayTutorial = "02_Play_Tutorial"; 
            public const string Ending = "18_Ending"; 
        }

        /// <summary> 리소스 경로 상수 모음 </summary>
        public static class Path
        {
            public const string JsonSetting = "Settings"; 
            public const string Title = "JSON/Title"; 
            public const string Tutorial = "JSON/Tutorial"; 
            public const string PlayTutorial = "JSON/PlayTutorial"; 
            public const string Ending = "JSON/Ending"; 
            public const string ApiSetting = "JSON/API";
        }
        
        /// <summary> 모듈 및 레벨 상수 모음 </summary>
        public static class Module
        {
            public const string Code = "A1"; 
        }

        public static class Level
        {
            public const string Tutorial = "Tutorial";
            public const string Q1 = "Q1";
            public const string Q15 = "Q15";
        }

        /// <summary> 아두이노 하드웨어 통신 상수 모음 </summary>
        public static class Hardware
        {
            public const string LeftArduino = "Left_Arduino";
            public const string RightArduino = "Right_Arduino";
            public const string LightArduino = "Light";

            // 송신 커맨드 (PC -> 아두이노)
            public const string CmdLedAllOff = "LEDAllOff";
            public const string CmdLedAllOn = "LEDAllOn";
            public const string CmdSoundOn = "SoundOn";
            public const string CmdLedShotOn = "LEDShotOn";
            public const string CmdLedShotOff = "LEDShotOff";

            // 수신 커맨드 (아두이노 -> PC)
            public const string InputShotOn = "ShotOn";
            public const string Input1On = "1On";
            public const string Input2On = "2On";
            public const string Input3On = "3On";
            public const string Input4On = "4On";
            public const string Input5On = "5On";
            public const string InputOnSuffix = "On";
        }

        /// <summary> API 상태 상수 모음 </summary>
        public static class Api
        {
            public const string StatusEmpty = "EMPTY";
            public const string StatusUsing = "USING";
        }
    }
}