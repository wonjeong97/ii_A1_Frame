using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks; 
using My.Scripts.Core.Data;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json; 
using My.Scripts.Global;
using Wonjeong.Utils;

namespace My.Scripts.Core
{
    /// <summary> 서버에서 전달되는 플레이어 고유 색상 코드 매핑용 열거형 </summary>
    public enum ColorData
    {   
        NotSet = -1,
        Cyan = 0, Pink = 1, Orange = 2, Green = 3, Red = 4, Yellow = 5
    }
    
    /// <summary> API 응답 데이터 중 세션에 필요한 핵심 유저 정보 컨테이너 </summary>
    public struct UserData
    {
        public string CARTRIDGE;
        public int IDX_USER; 
        public string UID_LEFT;
        public string UID_RIGHT;
        public string LANG;
        public int RELATION;
        
        public ColorData COLOR_LEFT; 
        public ColorData COLOR_RIGHT;

        public string RESERVATION_FIRST_NAME_LEFT;
        public string RESERVATION_LAST_NAME_LEFT;
        public string RESERVATION_FIRST_NAME_RIGHT;
        public string RESERVATION_LAST_NAME_RIGHT;
        
        public int PIECE_A1; public int PIECE_A2; public int PIECE_A3;
        public int PIECE_B1; public int PIECE_B2; public int PIECE_B3;
        public int PIECE_C1; public int PIECE_C2; public int PIECE_C3;
        public int PIECE_D1; public int PIECE_D2; public int PIECE_D3;
    }

    /// <summary> 서버 JSON 테이블 구조 역직렬화용 클래스 </summary>
    public class ApiTableResponse
    {
        public List<string> COLUMNS { get; set; }
        public List<List<object>> DATA { get; set; } 
    }

    /// <summary> 
    /// 서버 API 통신 및 유저 데이터 파싱 매니저.
    /// 비동기 처리를 통해 통신 중 프레임 드랍을 방지하고 작업 완료를 보장함.
    /// </summary>
    public class APIManager : MonoBehaviour
    {
        private string userUid;

        /// <summary> 레거시 동기 코드 호환용 래퍼 </summary>
        /// <param name="uid">조회할 유저 UID</param>
        public void FetchData(string uid)
        {
            FetchDataAsync(uid).Forget();
        }
        
        /// <summary> 
        /// UID 기반 유저 데이터 비동기 요청.
        /// </summary>
        /// <param name="uid">유저 고유 식별자</param>
        /// <returns>통신 및 데이터 설정 성공 여부</returns>
        [ContextMenu("Fetch API Data")]
        public async UniTask<bool> FetchDataAsync(string uid)
        {
            userUid = uid;
            ApiSettings config = null;
            
            // 싱글톤 참조 시 암시적 불리언 검사 활용
            if (GameManager.Instance) config = GameManager.Instance.ApiConfig;
            if (config == null) config = JsonLoader.Load<ApiSettings>(GameConstants.Path.ApiSetting);

            if (config == null)
            {
                Debug.LogError("[APIManager] API 설정을 찾을 수 없습니다.");
                return false;
            }

            string requestUrl = $"{config.GetUserUrl}?uid={userUid}";
            
            using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
            {
                webRequest.timeout = 10; // 네트워크 지연으로 인한 무한 대기 방지
                await webRequest.SendWebRequest().ToUniTask();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[APIManager] 통신 실패: {webRequest.error}");
                    return false;
                }
                
                return await ParseAndProcessDataAsync(webRequest.downloadHandler.text);
            }
        }

        /// <summary> 
        /// JSON 파싱 및 SessionManager 데이터 주입.
        /// </summary>
        /// <param name="jsonString">서버 응답 JSON</param>
        /// <returns>파싱 및 유효성 검사 성공 여부</returns>
        public async UniTask<bool> ParseAndProcessDataAsync(string jsonString)
        {
            try
            {
                // 무거운 역직렬화 연산은 백그라운드 스레드에서 처리하여 UI 프리징 차단
                ApiTableResponse response = await UniTask.RunOnThreadPool(() => JsonConvert.DeserializeObject<ApiTableResponse>(jsonString));

                if (response != null && response.DATA != null && response.DATA.Count > 0)
                {
                    List<object> firstRow = response.DATA[0];
                    UserData userData = new UserData();

                    // 서버 데이터 불일치 대응을 위한 안전한 파싱 로직 적용
                    userData.IDX_USER = ParseIntSafe(response, firstRow, "IDX_USER");
                    userData.CARTRIDGE = ParseStringSafe(response, firstRow, "CARTRIDGE"); 
                    userData.UID_LEFT = ParseStringSafe(response, firstRow, "UID_LEFT");
                    userData.UID_RIGHT = ParseStringSafe(response, firstRow, "UID_RIGHT");
                    userData.LANG = ParseStringSafe(response, firstRow, "LANG");
                    userData.RELATION = ParseIntSafe(response, firstRow, "RELATION");

                    userData.RESERVATION_FIRST_NAME_LEFT = ParseStringSafe(response, firstRow, "RESERVATION_FIRST_NAME_LEFT");
                    userData.RESERVATION_FIRST_NAME_RIGHT = ParseStringSafe(response, firstRow, "RESERVATION_FIRST_NAME_RIGHT");
                    
                    userData.COLOR_LEFT = ParseColorSafe(response, firstRow, "COLOR_LEFT");
                    userData.COLOR_RIGHT = ParseColorSafe(response, firstRow, "COLOR_RIGHT");

                    userData.PIECE_A1 = ParseIntSafe(response, firstRow, "PIECE_A1");
                    userData.PIECE_A2 = ParseIntSafe(response, firstRow, "PIECE_A2");
                    userData.PIECE_A3 = ParseIntSafe(response, firstRow, "PIECE_A3");
                    userData.PIECE_B1 = ParseIntSafe(response, firstRow, "PIECE_B1");
                    userData.PIECE_B2 = ParseIntSafe(response, firstRow, "PIECE_B2");
                    userData.PIECE_B3 = ParseIntSafe(response, firstRow, "PIECE_B3");
                    userData.PIECE_C1 = ParseIntSafe(response, firstRow, "PIECE_C1");
                    userData.PIECE_C2 = ParseIntSafe(response, firstRow, "PIECE_C2");
                    userData.PIECE_C3 = ParseIntSafe(response, firstRow, "PIECE_C3");
                    userData.PIECE_D1 = ParseIntSafe(response, firstRow, "PIECE_D1");
                    userData.PIECE_D2 = ParseIntSafe(response, firstRow, "PIECE_D2");
                    userData.PIECE_D3 = ParseIntSafe(response, firstRow, "PIECE_D3");

                    if (SessionManager.Instance)
                    {   
                        SessionManager.Instance.CurrentUserId = userData.IDX_USER;
                        SessionManager.Instance.Cartridge = userData.CARTRIDGE; 
                        SessionManager.Instance.PlayerAUid = userData.UID_LEFT;
                        SessionManager.Instance.PlayerBUid = userData.UID_RIGHT;

                        if (!string.IsNullOrWhiteSpace(userData.LANG)) SessionManager.Instance.CurrentLanguage = userData.LANG.Trim();

                        // 정수형 관계 코드를 내부 UserType으로 변환
                        switch (userData.RELATION)
                        {
                            case 1: SessionManager.Instance.CurrentUserType = UserType.A; break;
                            case 2: SessionManager.Instance.CurrentUserType = UserType.B; break;
                            case 3: SessionManager.Instance.CurrentUserType = UserType.C; break;
                            case 4: SessionManager.Instance.CurrentUserType = UserType.D; break;
                            case 5: SessionManager.Instance.CurrentUserType = UserType.E; break;
                            case 6: SessionManager.Instance.CurrentUserType = UserType.F; break;
                            default: SessionManager.Instance.CurrentUserType = UserType.A; break;
                        }

                        if (!string.IsNullOrEmpty(userData.RESERVATION_FIRST_NAME_LEFT))
                            SessionManager.Instance.PlayerAFirstName = userData.RESERVATION_FIRST_NAME_LEFT;
                        if (!string.IsNullOrEmpty(userData.RESERVATION_FIRST_NAME_RIGHT))
                            SessionManager.Instance.PlayerBFirstName = userData.RESERVATION_FIRST_NAME_RIGHT;
                        
                        SessionManager.Instance.PlayerAColor = userData.COLOR_LEFT;
                        SessionManager.Instance.PlayerBColor = userData.COLOR_RIGHT;
                        
                        // 음수 값 방지를 위해 Mathf.Max 활용
                        SessionManager.Instance.PieceA1 = Mathf.Max(0, userData.PIECE_A1);
                        SessionManager.Instance.PieceA2 = Mathf.Max(0, userData.PIECE_A2);
                        SessionManager.Instance.PieceA3 = Mathf.Max(0, userData.PIECE_A3);
                        SessionManager.Instance.PieceB1 = Mathf.Max(0, userData.PIECE_B1);
                        SessionManager.Instance.PieceB2 = Mathf.Max(0, userData.PIECE_B2);
                        SessionManager.Instance.PieceB3 = Mathf.Max(0, userData.PIECE_B3);
                        SessionManager.Instance.PieceC1 = Mathf.Max(0, userData.PIECE_C1);
                        SessionManager.Instance.PieceC2 = Mathf.Max(0, userData.PIECE_C2);
                        SessionManager.Instance.PieceC3 = Mathf.Max(0, userData.PIECE_C3);
                        SessionManager.Instance.PieceD1 = Mathf.Max(0, userData.PIECE_D1);
                        SessionManager.Instance.PieceD2 = Mathf.Max(0, userData.PIECE_D2);
                        SessionManager.Instance.PieceD3 = Mathf.Max(0, userData.PIECE_D3);

                        SessionManager.Instance.IsOtherCartridgeContentsCleared = false;
                        if (!string.IsNullOrWhiteSpace(userData.CARTRIDGE))
                        {
                            // 카트리지 그룹 내 다른 게임 완료 정보 동기화
                            await CheckOtherCartridgeContentsAsync(userData.CARTRIDGE, response, firstRow);
                        }
                        return true; 
                    }
                }
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[APIManager] JSON 파싱 중 에러 발생: {e.Message}");
                return false;
            }
        }

        /// <summary> 카트리지 묶음 콘텐츠의 클리어 상태 추가 조회 </summary>
        private async UniTask CheckOtherCartridgeContentsAsync(string cartridgeStr, ApiTableResponse firstApiResponse, List<object> firstApiRow)
        {
            if (!GameManager.Instance || GameManager.Instance.ApiConfig == null) return;

            string url = $"{GameManager.Instance.ApiConfig.GetCartridgeContentUrl}?cartridge={UnityWebRequest.EscapeURL(cartridgeStr)}";
            
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                await req.SendWebRequest().ToUniTask();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    string targetListStr = req.downloadHandler.text;
                    if (SessionManager.Instance)
                    {
                        SessionManager.Instance.IsOtherCartridgeContentsCleared = ParseOtherCartridgeClearState(targetListStr, firstApiResponse, firstApiRow);
                    }
                }
            }
        }

        /// <summary> 컬럼 인덱스 매핑을 통한 안전한 정수 파싱 </summary>
        private int ParseIntSafe(ApiTableResponse response, List<object> row, string colName)
        {
            int index = response.COLUMNS.IndexOf(colName);
            if (index != -1 && row.Count > index && row[index] != null)
            {
                string valStr = row[index].ToString().Trim();
                if (int.TryParse(valStr, out int val)) return val;
            }
            return 0; 
        }

        /// <summary> 컬럼 명칭 기반 안전한 문자열 추출 </summary>
        private string ParseStringSafe(ApiTableResponse response, List<object> row, string colName)
        {
            int index = response.COLUMNS.IndexOf(colName);
            if (index != -1 && row.Count > index && row[index] != null) return row[index].ToString();
            return string.Empty; 
        }

        /// <summary> 서버의 컬러 인덱스 데이터를 내부 열거형으로 안전 변환 </summary>
        private ColorData ParseColorSafe(ApiTableResponse response, List<object> row, string colName)
        {
            int index = response.COLUMNS.IndexOf(colName);
            if (index != -1 && row.Count > index && row[index] != null)
            {
                if (int.TryParse(row[index].ToString(), out int val))
                {
                    if (val >= (int)ColorData.NotSet && val <= (int)ColorData.Yellow) return (ColorData)val;   
                }
            }
            return ColorData.NotSet; 
        }

        /// <summary> 타 모듈 클리어 데이터를 분석하여 카트리지 완성 여부 판단 </summary>
        private bool ParseOtherCartridgeClearState(string targetListStr, ApiTableResponse firstApiResponse, List<object> firstApiRow)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetListStr)) return false;
                
                string[] targetCodes = targetListStr.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string target in targetCodes)
                {
                    string expectedCode = target.Trim().ToUpper(); 
                    
                    // 현재 진행 중인 모듈 코드는 제외하고 검사
                    if (expectedCode == (SessionManager.Instance ? SessionManager.Instance.CurrentModuleCode.ToUpper() : "A1")) continue;
                    
                    string endValue = ParseStringSafe(firstApiResponse, firstApiRow, $"END_{expectedCode}");
                    
                    // 하나라도 클리어 기록(Time)이 없으면 미완료로 판단
                    if (string.IsNullOrWhiteSpace(endValue) || endValue.Equals("null", StringComparison.OrdinalIgnoreCase)) return false; 
                }
                return true;
            }
            catch { return false; }
        }
    }
}