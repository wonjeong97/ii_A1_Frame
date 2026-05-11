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
            public const string Tutorial = "Tutorial"; 
            public const string PlayTutorial = "PlayTutorial"; 
            public const string Ending = "Ending"; 
            public const string ApiSetting = "API";
            public const string PlayCommon = "PlayCommon";
            public const string HueConfig = "HueConfig";
            
            /// <summary>
            /// 파일 성격에 따라 전역 JSON 또는 언어별 JSON 경로를 반환합니다.
            /// </summary>
            public static string GetLocalizedPath(string fileName)
            {
                // 1. 전역 설정 파일(API, HueConfig)은 언어 폴더를 거치지 않고 JSON 폴더에서 직접 참조
                if (fileName == ApiSetting || fileName == HueConfig)
                {
                    return $"JSON/{fileName}";
                }

                // 2. Settings.json은 루트 폴더에 있으므로 그대로 반환 (필요시)
                if (fileName == JsonSetting) return fileName;

                // 3. 그 외 나머지는 현재 설정된 언어 폴더(ko/en/jp) 경로 반환
                string lang = SessionManager.Instance ? SessionManager.Instance.CurrentLanguage : "ko";
                return $"JSON/{lang}/{fileName}";
            }
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
            public const string LightArduino = "Arduino_Light";

            // 송신 커맨드 (PC -> 아두이노)
            public const string CmdLedAllOff = "LEDAllOff";
            public const string CmdLedAllOn = "LEDAllOn";
            public const string CmdSoundOn = "SoundOn";
            public const string CmdLedShotOn = "LEDShotOn";
            public const string CmdLedShotOff = "LEDShotOff";
            public const string CmdLightOn = "Light_On";
            public const string CmdLightOff = "Light_Off";

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