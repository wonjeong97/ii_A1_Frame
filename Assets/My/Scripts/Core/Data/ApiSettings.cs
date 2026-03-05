using System;

namespace My.Scripts.Core.Data
{
    /// <summary> API.json 데이터를 매핑하는 클래스 </summary>
    [Serializable]
    public class ApiSettings
    {
        public string baseUrl;
        public string getUser;
        public string updateTime;
        public string updateValue;
        public string updatePiece;
        public string checkRoomState;
        public string getCurrentRoomUser;
        public string uploadFile;
        public string exitRoom;
        public string resetStart;

        private static string BuildUrl(string baseUrl, string path, string fallbackPath = null)
        {
            string finalPath = string.IsNullOrWhiteSpace(path) ? fallbackPath : path;
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(finalPath))
                return string.Empty;

            return $"{baseUrl.TrimEnd('/')}/{finalPath.TrimStart('/')}";
        }

        // URL 조합을 쉽게 해주는 헬퍼 프로퍼티 (모두 BuildUrl을 거치도록 통일)
        public string GetUserUrl => BuildUrl(baseUrl, getUser, "/getUser.cfm");
        public string UpdateTimeUrl => BuildUrl(baseUrl, updateTime, "/updateTime.cfm");
        public string UpdateValueUrl => BuildUrl(baseUrl, updateValue, "/updateValue.cfm");
        public string UpdatePieceUrl => BuildUrl(baseUrl, updatePiece, "/updatePiece.cfm");
        public string CheckRoomStateUrl => BuildUrl(baseUrl, checkRoomState, "/checkRoomState.cfm");
        public string GetCurrentRoomUserUrl => BuildUrl(baseUrl, getCurrentRoomUser, "/getCurrentRoomUser.cfm");
        public string UploadFileUrl => BuildUrl(baseUrl, uploadFile, "/uploadFile.cfm");
        public string ExitRoomUrl => BuildUrl(baseUrl, exitRoom, "/exitRoom.cfm");
        public string ResetStartUrl => BuildUrl(baseUrl, resetStart, "/resetStart.cfm");
    }
}