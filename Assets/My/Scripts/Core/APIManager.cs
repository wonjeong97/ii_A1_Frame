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
    public enum ColorData
    {   
        NotSet = -1,
        Cyan = 0, Pink = 1, Orange = 2, Green = 3, Red = 4, Yellow = 5
    }
    
    public struct UserData
    {
        public string CARTRIDGE;
        public int IDX_USER;
        public string BLOCK_CODE;
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

    public class ApiTableResponse
    {
        public List<string> COLUMNS { get; set; }
        public List<List<object>> DATA { get; set; } 
    }

    /// <summary>
    /// API 서버와 통신하여 유저의 진행 데이터를 조회하고 세션에 동기화함.
    /// </summary>
    public class APIManager : MonoBehaviour
    {
        private string userUid;

        [Header("API Retry Settings")]
        [SerializeField] private int maxRetries = 10;
        [SerializeField] private float retryDelay = 1.0f;

        /// <summary>
        /// 유저 데이터 조회를 백그라운드 태스크로 실행함.
        /// </summary>
        /// <param name="uid">조회할 유저의 고유 식별자</param>
        public void FetchData(string uid)
        {
            FetchDataAsync(uid).Forget();
        }
        
        /// <summary>
        /// API 서버에 유저 데이터를 요청하고 네트워크 실패 시 지정된 횟수만큼 재시도함.
        /// </summary>
        /// <param name="uid">조회할 유저의 고유 식별자</param>
        /// <returns>조회 및 처리 성공 여부</returns>
        [ContextMenu("Fetch API Data")]
        public async UniTask<bool> FetchDataAsync(string uid)
        {
            userUid = uid;
            ApiSettings config = null;

            if (GameManager.Instance)
            {
                config = GameManager.Instance.ApiConfig;
            }

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
                Debug.LogError("API 설정을 찾을 수 없음.");
                return false;
            }

            // ex: config.GetUserUrl = "http://api.test.com/user", userUid = "12345" -> "http://api.test.com/user?uid=12345"
            string requestUrl = $"{config.GetUserUrl}?uid={userUid}";

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
                {
                    webRequest.timeout = 10; 
                    await webRequest.SendWebRequest().ToUniTask();

                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        return await ParseAndProcessDataAsync(webRequest.downloadHandler.text);
                    }

                    if (attempt < maxRetries - 1)
                    {
                        Debug.LogWarning($"유저 데이터 조회 실패 ({attempt + 1}/{maxRetries}): {webRequest.error}. {retryDelay}초 후 재시도.");
                        await UniTask.Delay(TimeSpan.FromSeconds(retryDelay));
                    }
                    else
                    {
                        Debug.LogError($"유저 데이터 조회 최종 실패: {webRequest.error}");
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 응답받은 JSON 문자열을 역직렬화하고 세션 매니저 객체에 값을 매핑함.
        /// </summary>
        /// <param name="jsonString">API 응답 JSON 문자열</param>
        /// <returns>파싱 및 동기화 성공 여부</returns>
        public async UniTask<bool> ParseAndProcessDataAsync(string jsonString)
        {
            try
            {
                // 메인 스레드 프리징 방지를 위해 스레드 풀에서 파싱 처리함.
                ApiTableResponse response = await UniTask.RunOnThreadPool(() => JsonConvert.DeserializeObject<ApiTableResponse>(jsonString));

                if (response != null && response.DATA != null && response.DATA.Count > 0)
                {
                    List<object> firstRow = response.DATA[0];

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
                    userData.BLOCK_CODE = ParseStringSafe(colMap, firstRow, "BLOCK_CODE");

                    Debug.Log($"유저 데이터 로드 완료\n" +
                              $"- 유저 인덱스(IDX_USER): {userData.IDX_USER}\n" +
                              $"- 이름 (L/R): {userData.RESERVATION_FIRST_NAME_LEFT} / {userData.RESERVATION_FIRST_NAME_RIGHT}\n" +
                              $"- UID (L/R): {userData.UID_LEFT} / {userData.UID_RIGHT}\n" +
                              $"- 컬러 (L/R): {userData.COLOR_LEFT} / {userData.COLOR_RIGHT}\n" +
                              $"- 언어/관계: {userData.LANG} / {userData.RELATION}\n" +
                              $"- 카트리지: {userData.CARTRIDGE}\n" +
                              $"- 블록 코드: {userData.BLOCK_CODE}");

                    if (SessionManager.Instance)
                    {   
                        SessionManager.Instance.CurrentUserId = userData.IDX_USER;
                        SessionManager.Instance.BlockCode = userData.BLOCK_CODE;
                        SessionManager.Instance.Cartridge = userData.CARTRIDGE; 
                        SessionManager.Instance.PlayerAUid = userData.UID_LEFT;
                        SessionManager.Instance.PlayerBUid = userData.UID_RIGHT;
                        
                        SessionManager.Instance.PieceA1 = ParseIntSafe(colMap, firstRow, "PIECE_A1");
                        SessionManager.Instance.PieceA2 = ParseIntSafe(colMap, firstRow, "PIECE_A2");
                        SessionManager.Instance.PieceA3 = ParseIntSafe(colMap, firstRow, "PIECE_A3");
                        SessionManager.Instance.PieceB1 = ParseIntSafe(colMap, firstRow, "PIECE_B1");
                        SessionManager.Instance.PieceB2 = ParseIntSafe(colMap, firstRow, "PIECE_B2");
                        SessionManager.Instance.PieceB3 = ParseIntSafe(colMap, firstRow, "PIECE_B3");
                        SessionManager.Instance.PieceC1 = ParseIntSafe(colMap, firstRow, "PIECE_C1");
                        SessionManager.Instance.PieceC2 = ParseIntSafe(colMap, firstRow, "PIECE_C2");
                        SessionManager.Instance.PieceC3 = ParseIntSafe(colMap, firstRow, "PIECE_C3");
                        SessionManager.Instance.PieceD1 = ParseIntSafe(colMap, firstRow, "PIECE_D1");
                        SessionManager.Instance.PieceD2 = ParseIntSafe(colMap, firstRow, "PIECE_D2");
                        SessionManager.Instance.PieceD3 = ParseIntSafe(colMap, firstRow, "PIECE_D3");

                        if (!string.IsNullOrWhiteSpace(userData.LANG)) 
                        {
                            SessionManager.Instance.CurrentLanguage = userData.LANG.Trim();
                        }
                        else
                        {
                            Debug.LogWarning("LANG 데이터 누락됨.");
                        }

                        if (!string.IsNullOrEmpty(userData.RESERVATION_FIRST_NAME_LEFT))
                        {
                            SessionManager.Instance.PlayerAFirstName = userData.RESERVATION_FIRST_NAME_LEFT;
                        }
                        else
                        {
                            Debug.LogWarning("RESERVATION_FIRST_NAME_LEFT 누락됨.");
                        }

                        if (!string.IsNullOrEmpty(userData.RESERVATION_FIRST_NAME_RIGHT))
                        {
                            SessionManager.Instance.PlayerBFirstName = userData.RESERVATION_FIRST_NAME_RIGHT;
                        }
                        else
                        {
                            Debug.LogWarning("RESERVATION_FIRST_NAME_RIGHT 누락됨.");
                        }
                        
                        SessionManager.Instance.PlayerAColor = userData.COLOR_LEFT;
                        SessionManager.Instance.PlayerBColor = userData.COLOR_RIGHT;

                        // # TODO: 문자열 할당(ToUpper, 보간) 및 Enum.TryParse에서 발생하는 GC 억제를 위해 매핑 테이블 캐싱 고려.
                        string cartridgeStr = userData.CARTRIDGE;
                        if (string.IsNullOrWhiteSpace(cartridgeStr))
                        {
                            Debug.LogWarning("CARTRIDGE 누락됨. 기본값 'A' 사용.");
                            cartridgeStr = "A";
                        }
                        else
                        {
                            cartridgeStr = cartridgeStr.Trim().ToUpper();
                        }

                        int relationNum = userData.RELATION;
                        if (relationNum < 1 || relationNum > 6)
                        {
                            Debug.LogWarning("RELATION 값 범위를 벗어남. 기본값 1 사용.");
                            relationNum = 1;
                        }

                        // ex: cartridgeStr="C", relationNum=4 -> combinedTypeStr="C4"
                        string combinedTypeStr = $"{cartridgeStr}{relationNum}"; 
                        
                        if (Enum.TryParse(combinedTypeStr, out UserType parsedType))
                        {
                            SessionManager.Instance.CurrentUserType = parsedType;
                        }
                        else
                        {
                            Debug.LogWarning($"알 수 없는 타입 조합: {combinedTypeStr}. 기본값 A1 적용.");
                            SessionManager.Instance.CurrentUserType = UserType.A1;
                        }

                        int endCount = 0;
                        string currentModuleEnd = $"END_{GameConstants.Module.Code.ToUpper()}"; 

                        // 타 콘텐츠 진행도 확인 루프
                        foreach (string colName in response.COLUMNS)
                        {
                            if (colName.StartsWith("END_"))
                            {
                                if (colName.Equals(currentModuleEnd, StringComparison.OrdinalIgnoreCase) ||
                                    colName.StartsWith("END_Z", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                string endValue = ParseStringSafe(colMap, firstRow, colName);
                                
                                if (!string.IsNullOrWhiteSpace(endValue) && !endValue.Equals("null", StringComparison.OrdinalIgnoreCase))
                                {
                                    endCount++;
                                }
                            }
                        }

                        SessionManager.Instance.ClearedEndCount = endCount;
                        SessionManager.Instance.IsOtherCartridgeContentsCleared = (endCount >= 3);
                        Debug.Log($"타 콘텐츠 완료 개수: {endCount}개 (Z계열 제외, 3개 이상 완료 판정: {SessionManager.Instance.IsOtherCartridgeContentsCleared})");

                        return true; 
                    }
                }
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"JSON 파싱 중 에러 발생: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 동적 데이터 배열에서 정수형 값을 안전하게 추출함.
        /// </summary>
        /// <param name="map">컬럼명 인덱스 맵</param>
        /// <param name="row">데이터 배열</param>
        /// <param name="col">추출할 컬럼명</param>
        /// <returns>파싱된 정수형 값 (실패 시 0)</returns>
        private int ParseIntSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null)
            {
                string valStr = row[idx].ToString().Trim();
                if (int.TryParse(valStr, out int val)) return val;
            }
            return 0; 
        }

        /// <summary>
        /// 동적 데이터 배열에서 문자열 값을 안전하게 추출함.
        /// </summary>
        /// <param name="map">컬럼명 인덱스 맵</param>
        /// <param name="row">데이터 배열</param>
        /// <param name="col">추출할 컬럼명</param>
        /// <returns>파싱된 문자열 (실패 시 Empty)</returns>
        private string ParseStringSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null) 
                return row[idx].ToString();
            return string.Empty; 
        }

        /// <summary>
        /// 동적 데이터 배열에서 색상 열거형 값을 안전하게 추출함.
        /// </summary>
        /// <param name="map">컬럼명 인덱스 맵</param>
        /// <param name="row">데이터 배열</param>
        /// <param name="col">추출할 컬럼명</param>
        /// <returns>파싱된 색상 열거형 값 (실패 시 NotSet)</returns>
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
    }
}