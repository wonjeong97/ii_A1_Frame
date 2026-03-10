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
    
    /// <summary> API 응답 데이터 중 세션 관리에 필요한 유저 정보 구조체 </summary>
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

    /// <summary> 서버 JSON 테이블 구조(COLUMNS/DATA) 역직렬화용 클래스 </summary>
    public class ApiTableResponse
    {
        public List<string> COLUMNS { get; set; }
        public List<List<object>> DATA { get; set; } 
    }

    /// <summary> 
    /// 서버 API 통신 및 유저 데이터 파싱 매니저.
    /// 비동기 처리를 통해 통신 중 프레임 드랍을 방지하고 작업 완료 시점을 명시적으로 보장합니다.
    /// </summary>
    public class APIManager : MonoBehaviour
    {
        private string userUid;

        /// <summary> 레거시 동기 코드 호환용 래퍼 </summary>
        public void FetchData(string uid)
        {
            FetchDataAsync(uid).Forget();
        }
        
        /// <summary> 
        /// UID 기반 유저 데이터 비동기 요청 및 전역 설정 동기화.
        /// </summary>
        /// <param name="uid">유저 고유 식별자</param>
        /// <returns>통신 및 데이터 설정 성공 여부</returns>
        [ContextMenu("Fetch API Data")]
        public async UniTask<bool> FetchDataAsync(string uid)
        {
            userUid = uid;
            ApiSettings config = GameManager.Instance ? GameManager.Instance.ApiConfig : null;

            // 로드한 설정이 로컬 변수에만 머물지 않도록 GameManager 전역 상태에 역주입함
            if (config == null)
            {
                config = JsonLoader.Load<ApiSettings>(GameConstants.Path.ApiSetting);
                if (GameManager.Instance && config != null) 
                {
                    GameManager.Instance.ApiConfig = config;
                }
            }

            if (config == null)
            {
                Debug.LogError("[APIManager] API 설정을 찾을 수 없습니다.");
                return false;
            }

            string requestUrl = $"{config.GetUserUrl}?uid={userUid}";
            
            using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
            {
                webRequest.timeout = 10; 
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
        /// JSON 파싱 및 세션 할당. O(n*m) 탐색 비용을 줄이기 위해 컬럼 맵을 사전 빌드합니다.
        /// </summary>
        /// <param name="jsonString">서버 응답 JSON</param>
        /// <returns>파싱 및 유효성 검사 성공 여부</returns>
        public async UniTask<bool> ParseAndProcessDataAsync(string jsonString)
        {
            try
            {
                ApiTableResponse response = await UniTask.RunOnThreadPool(() => JsonConvert.DeserializeObject<ApiTableResponse>(jsonString));

                if (response != null && response.DATA != null && response.DATA.Count > 0)
                {
                    List<object> firstRow = response.DATA[0];

                    // IndexOf 반복 호출을 방지하기 위해 Dictionary 기반 인덱스 맵 생성
                    Dictionary<string, int> colMap = new Dictionary<string, int>();
                    for (int i = 0; i < response.COLUMNS.Count; i++)
                    {
                        colMap[response.COLUMNS[i]] = i;
                    }

                    UserData userData = new UserData();
                    userData.IDX_USER = ParseIntSafe(colMap, firstRow, "IDX_USER");
                    userData.CARTRIDGE = ParseStringSafe(colMap, firstRow, "CARTRIDGE"); 
                    userData.UID_LEFT = ParseStringSafe(colMap, firstRow, "UID_LEFT");
                    userData.UID_RIGHT = ParseStringSafe(colMap, firstRow, "UID_RIGHT");
                    userData.LANG = ParseStringSafe(colMap, firstRow, "LANG");
                    userData.RELATION = ParseIntSafe(colMap, firstRow, "RELATION");
                    userData.RESERVATION_FIRST_NAME_LEFT = ParseStringSafe(colMap, firstRow, "RESERVATION_FIRST_NAME_LEFT");
                    userData.RESERVATION_FIRST_NAME_RIGHT = ParseStringSafe(colMap, firstRow, "RESERVATION_FIRST_NAME_RIGHT");
                    userData.COLOR_LEFT = ParseColorSafe(colMap, firstRow, "COLOR_LEFT");
                    userData.COLOR_RIGHT = ParseColorSafe(colMap, firstRow, "COLOR_RIGHT");

                    // 입장 유저 데이터 확인용 로그 복구
                    Debug.Log($"[APIManager] 유저 데이터 로드 완료!\n" +
                              $"- 유저 인덱스(IDX_USER): {userData.IDX_USER}\n" +
                              $"- 이름 (L/R): {userData.RESERVATION_FIRST_NAME_LEFT} / {userData.RESERVATION_FIRST_NAME_RIGHT}\n" +
                              $"- UID (L/R): {userData.UID_LEFT} / {userData.UID_RIGHT}\n" +
                              $"- 컬러 (L/R): {userData.COLOR_LEFT} / {userData.COLOR_RIGHT}\n" +
                              $"- 언어/관계: {userData.LANG} / {userData.RELATION}\n" +
                              $"- 카트리지: {userData.CARTRIDGE}");

                    if (SessionManager.Instance)
                    {   
                        SessionManager.Instance.CurrentUserId = userData.IDX_USER;
                        SessionManager.Instance.Cartridge = userData.CARTRIDGE; 
                        SessionManager.Instance.PlayerAUid = userData.UID_LEFT;
                        SessionManager.Instance.PlayerBUid = userData.UID_RIGHT;

                        if (!string.IsNullOrWhiteSpace(userData.LANG)) 
                            SessionManager.Instance.CurrentLanguage = userData.LANG.Trim();

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
                        
                        // 데이터 로드 완료 가시성 확보를 위해 Piece 데이터 일괄 주입
                        userData.PIECE_A1 = ParseIntSafe(colMap, firstRow, "PIECE_A1");
                        SessionManager.Instance.PieceA1 = Mathf.Max(0, userData.PIECE_A1);
                        // ... (기타 PIECE 필드 동일 패턴으로 할당)

                        SessionManager.Instance.IsOtherCartridgeContentsCleared = false;
                        if (!string.IsNullOrWhiteSpace(userData.CARTRIDGE))
                        {
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

        /// <summary> 카트리지 묶음 콘텐츠의 클리어 상태 추가 조회 및 전역 설정 역주입 확인 </summary>
        private async UniTask CheckOtherCartridgeContentsAsync(string cartridgeStr, ApiTableResponse firstApiResponse, List<object> firstApiRow)
        {
            ApiSettings config = GameManager.Instance ? GameManager.Instance.ApiConfig : null;
            if (config == null)
            {
                config = JsonLoader.Load<ApiSettings>(GameConstants.Path.ApiSetting);
                if (GameManager.Instance && config != null) GameManager.Instance.ApiConfig = config;
            }
            if (config == null) return;

            string url = $"{config.GetCartridgeContentUrl}?cartridge={UnityWebRequest.EscapeURL(cartridgeStr)}";
            
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

        /// <summary> 사전 빌드된 맵을 사용하여 빠른 컬럼 데이터 추출 (정수) </summary>
        private int ParseIntSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null)
            {
                string valStr = row[idx].ToString().Trim();
                if (int.TryParse(valStr, out int val)) return val;
            }
            return 0; 
        }

        /// <summary> 사전 빌드된 맵을 사용하여 빠른 컬럼 데이터 추출 (문자열) </summary>
        private string ParseStringSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null) 
                return row[idx].ToString();
            return string.Empty; 
        }

        /// <summary> 사전 빌드된 맵을 사용하여 빠른 컬럼 데이터 추출 (컬러) </summary>
        private ColorData ParseColorSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null)
            {
                if (int.TryParse(row[idx].ToString(), out int val))
                {
                    if (val >= (int)ColorData.NotSet && val <= (int)ColorData.Yellow) 
                        return (ColorData)val;   
                }
            }
            return ColorData.NotSet; 
        }

        /// <summary> 타 모듈 클리어 데이터를 분석하여 카트리지 완성 여부 판단 </summary>
        private bool ParseOtherCartridgeClearState(string targetListStr, ApiTableResponse resp, List<object> row)
        {
            if (string.IsNullOrWhiteSpace(targetListStr)) return false;

            Dictionary<string, int> map = new Dictionary<string, int>();
            for (int i = 0; i < resp.COLUMNS.Count; i++) map[resp.COLUMNS[i]] = i;

            foreach (string target in targetListStr.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string code = target.Trim().ToUpper(); 
                if (code == (SessionManager.Instance ? SessionManager.Instance.CurrentModuleCode.ToUpper() : "A1")) 
                    continue;
                
                string val = ParseStringSafe(map, row, $"END_{code}");
                if (string.IsNullOrWhiteSpace(val) || val.Equals("null", StringComparison.OrdinalIgnoreCase)) 
                    return false; 
            }
            return true;
        }
    }
}