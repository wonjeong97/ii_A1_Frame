using System;

namespace My.Scripts.Core.Data
{
    /// <summary> 
    /// 외부 JSON(API.json) 설정 데이터를 매핑하고 조합하는 클래스입니다.
    /// 서버 환경(BaseUrl)에 따라 각 기능별 엔드포인트를 안전하게 생성하는 역할을 수행합니다.
    /// </summary>
    [Serializable]
    public class ApiSettings
    {
        // JSON 키값과 1:1 매핑되어 서버 엔드포인트 경로를 보관합니다.
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
        public string getCartridgeContent;

        /// <summary> 
        /// 슬래시 중복이나 누락을 방지하며 완전한 URL을 구성합니다.
        /// 데이터가 비어있을 경우를 대비해 하드코딩된 기본 경로(Fallback)를 적용합니다.
        /// </summary>
        private static string BuildUrl(string baseUrl, string path, string fallbackPath = null)
        {
            // 경로 데이터가 유효하지 않으면 폴백 경로를 우선 사용함
            string finalPath = string.IsNullOrWhiteSpace(path) ? fallbackPath : path;
            
            // 베이스 URL이나 최종 경로가 모두 없으면 통신 불가 상태로 판단함
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(finalPath))
                return string.Empty;

            // 문자열 트림을 통해 URL 조합 시 발생할 수 있는 "//" 중복 오류 방지
            return $"{baseUrl.TrimEnd('/')}/{finalPath.TrimStart('/')}";
        }

        // 각 도메인 로직에서 즉시 사용 가능한 완성형 URL 프로퍼티들입니다.
        /// <summary> 유저 정보 조회용 URL </summary>
        public string GetUserUrl => BuildUrl(baseUrl, getUser, "/getUser.cfm");
        
        /// <summary> 세션 시작/종료 시간 기록용 URL </summary>
        public string UpdateTimeUrl => BuildUrl(baseUrl, updateTime, "/updateTime.cfm");
        
        /// <summary> 게임 내 변수(점수 등) 업데이트용 URL </summary>
        public string UpdateValueUrl => BuildUrl(baseUrl, updateValue, "/updateValue.cfm");
        
        /// <summary> 마음 조각 획득 데이터 동기화용 URL </summary>
        public string UpdatePieceUrl => BuildUrl(baseUrl, updatePiece, "/updatePiece.cfm");
        
        /// <summary> 현재 룸의 활성화 상태 체크용 URL </summary>
        public string CheckRoomStateUrl => BuildUrl(baseUrl, checkRoomState, "/checkRoomState.cfm");
        
        /// <summary> 룸 내 진입 중인 유저 확인용 URL </summary>
        public string GetCurrentRoomUserUrl => BuildUrl(baseUrl, getCurrentRoomUser, "/getCurrentRoomUser.cfm");
        
        /// <summary> 이미지 및 파일 서버 업로드용 URL </summary>
        public string UploadFileUrl => BuildUrl(baseUrl, uploadFile, "/uploadFile.cfm");
        
        /// <summary> 세션 종료 및 퇴장 처리용 URL </summary>
        public string ExitRoomUrl => BuildUrl(baseUrl, exitRoom, "/exitRoom.cfm");
        
        /// <summary> 룸 상태 초기화용 URL </summary>
        public string ResetStartUrl => BuildUrl(baseUrl, resetStart, "/resetStart.cfm");
        
        /// <summary> 카트리지별 콘텐츠 클리어 상태 조회용 URL </summary>
        public string GetCartridgeContentUrl => BuildUrl(baseUrl, getCartridgeContent, "/getCartridgeContent.cfm");
    }
}