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

        // URL 조합을 쉽게 해주는 헬퍼 프로퍼티
        public string GetUserUrl => $"{baseUrl}{getUser}";
        public string UpdateTimeUrl => $"{baseUrl}{updateTime}";
        public string UpdateValueUrl => $"{baseUrl}{updateValue}";
    }
}