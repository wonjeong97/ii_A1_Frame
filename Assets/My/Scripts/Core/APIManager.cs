using System;
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
        /// <summary> 
        /// 카트리지와 관계 조합에 따른 UserType 캐싱 테이블 (가비지 할당 방지용)
        /// </summary>
        private readonly static UserType[,] UserTypeCache = new UserType[4, 5]
        {
            { UserType.A1, UserType.A2, UserType.A3, UserType.A4, UserType.A5 },
            { UserType.B1, UserType.B2, UserType.B3, UserType.B4, UserType.B5 },
            { UserType.C1, UserType.C2, UserType.C3, UserType.C4, UserType.C5 },
            { UserType.D1, UserType.D2, UserType.D3, UserType.D4, UserType.D5 }
        };
        
        private string userUid;

        [Header("API Retry Settings")]
        [SerializeField] private int maxRetries;
        [SerializeField] private float retryDelay;

        /// <summary>
        /// 유저 데이터 조회를 백그라운드 태스크로 실행함.
        /// </summary>
        /// <param name="uid">조회할 유저의 고유 식별자</param>
        public void FetchData(string uid)
        {
            FetchDataAsync(uid).Forget();
        }

#if UNITY_EDITOR
        [ContextMenu("Fill Debug Session")]
        public void FillDebugSession()
        {
            if (!SessionManager.Instance) return;
            SessionManager.Instance.CurrentUserId = -1;
            SessionManager.Instance.PlayerAFirstName = "fork";
            SessionManager.Instance.PlayerBFirstName = "you";
            SessionManager.Instance.PlayerAColor = ColorData.Green;
            SessionManager.Instance.PlayerBColor = ColorData.Yellow;
            SessionManager.Instance.CurrentLanguage = "ko";
            SessionManager.Instance.CurrentUserType = UserType.A1;
            SessionManager.Instance.BlockCode = "A1,B1,C1,D1";
            Debug.Log("[Debug] 테스트 세션 주입 완료");
        }
#endif
        
        /// <summary>
        /// API 서버에 유저 데이터를 요청하고 네트워크 실패 시 지정된 횟수만큼 재시도함.
        /// </summary>
        /// <param name="uid">조회할 유저의 고유 식별자</param>
        /// <returns>조회 및 처리 성공 여부</returns>
        public async UniTask<bool> FetchDataAsync(string uid)
        {
            userUid = uid;
            ApiSettings config = EnsureApiConfigLoaded();

            if (config == null || string.IsNullOrEmpty(config.GetUserUrl))
            {
                Debug.LogError("API 설정을 찾을 수 없거나 GetUserUrl이 누락되었습니다.");
                return false;
            }

            string requestUrl = $"{config.GetUserUrl}?uid={userUid}";

            return await ExecuteFetchRequestAsync(requestUrl);
        }
        
        /// <summary>
        /// API 설정(ApiSettings)이 로드되어 있는지 확인하고, 없을 경우 JSON에서 동적으로 로드함.
        /// </summary>
        private ApiSettings EnsureApiConfigLoaded()
        {
            if (GameManager.Instance && GameManager.Instance.ApiConfig != null)
            {
                return GameManager.Instance.ApiConfig;
            }

            string apiPath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.ApiSetting);
            ApiSettings config = JsonLoader.Load<ApiSettings>(apiPath);
            if (GameManager.Instance && config != null)
            {
                GameManager.Instance.ApiConfig = config;
            }

            return config;
        }
        
        /// <summary>
        /// 재시도 로직을 포함하여 실제 HTTP GET 요청을 수행하고 결과를 파싱함.
        /// </summary>
        private async UniTask<bool> ExecuteFetchRequestAsync(string requestUrl)
        {
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

                if (!IsValidResponse(response))
                {
                    return false;
                }

                Dictionary<string, int> colMap = BuildColumnMap(response.COLUMNS);
                List<object> firstRow = response.DATA[0];

                UserData userData = ExtractUserData(colMap, firstRow);
                LogExtractedData(userData);

                if (SessionManager.Instance)
                {
                    ApplyToSession(userData, response.COLUMNS, colMap, firstRow);
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"JSON 파싱 중 에러 발생: {e.Message}");
                return false;
            }
        }
        
        /// <summary> 응답 객체가 파싱에 필요한 최소한의 유효한 데이터를 포함하는지 검사함. </summary>
        private bool IsValidResponse(ApiTableResponse response)
        {
            return response != null && response.DATA != null && response.DATA.Count > 0 && response.COLUMNS != null;
        }
        
        /// <summary> 컬럼명을 인덱스로 빠르게 찾기 위한 딕셔너리 맵을 생성함. </summary>
        private Dictionary<string, int> BuildColumnMap(List<string> columns)
        {
            Dictionary<string, int> colMap = new Dictionary<string, int>();
            for (int i = 0; i < columns.Count; i++)
            {
                colMap[columns[i]] = i;
            }
            return colMap;
        }
        
        /// <summary> 파싱된 행 데이터에서 UserData 구조체로 필드값들을 추출함. </summary>
        private UserData ExtractUserData(Dictionary<string, int> colMap, List<object> firstRow)
        {
            UserData data = new UserData();
            data.IDX_USER = ParseIntSafe(colMap, firstRow, "IDX_USER");
            data.CARTRIDGE = ParseStringSafe(colMap, firstRow, "CARTRIDGE");
            data.UID_LEFT = ParseStringSafe(colMap, firstRow, "UID_LEFT");
            data.UID_RIGHT = ParseStringSafe(colMap, firstRow, "UID_RIGHT");
            data.LANG = ParseStringSafe(colMap, firstRow, "LANG");
            data.RELATION = ParseIntSafe(colMap, firstRow, "RELATION");
            data.RESERVATION_FIRST_NAME_LEFT = ParseStringSafe(colMap, firstRow, "RESERVATION_FIRST_NAME_LEFT");
            data.RESERVATION_FIRST_NAME_RIGHT = ParseStringSafe(colMap, firstRow, "RESERVATION_FIRST_NAME_RIGHT");
            data.COLOR_LEFT = ParseColorSafe(colMap, firstRow, "COLOR_LEFT");
            data.COLOR_RIGHT = ParseColorSafe(colMap, firstRow, "COLOR_RIGHT");
            data.BLOCK_CODE = ParseStringSafe(colMap, firstRow, "BLOCK_CODE");
            return data;
        }
        
        /// <summary> 개발 디버깅을 위해 로드된 핵심 유저 데이터를 로그로 출력함. </summary>
        private void LogExtractedData(UserData data)
        {
            Debug.Log($"유저 데이터 로드 완료\n" +
                      $"- 유저 인덱스(IDX_USER): {data.IDX_USER}\n" +
                      $"- 이름 (L/R): {data.RESERVATION_FIRST_NAME_LEFT} / {data.RESERVATION_FIRST_NAME_RIGHT}\n" +
                      $"- UID (L/R): {data.UID_LEFT} / {data.UID_RIGHT}\n" +
                      $"- 컬러 (L/R): {data.COLOR_LEFT} / {data.COLOR_RIGHT}\n" +
                      $"- 언어/관계: {data.LANG} / {data.RELATION}\n" +
                      $"- 카트리지: {data.CARTRIDGE}\n" +
                      $"- 블록 코드: {data.BLOCK_CODE}");
        }
        
        /// <summary> 추출된 UserData를 SessionManager의 상태값에 동기화함. </summary>
        private void ApplyToSession(UserData userData, List<string> columns, Dictionary<string, int> colMap, List<object> firstRow)
        {
            SessionManager session = SessionManager.Instance;

            session.CurrentUserId = userData.IDX_USER;
            session.BlockCode = userData.BLOCK_CODE;
            session.Cartridge = userData.CARTRIDGE;
            session.PlayerAUid = userData.UID_LEFT;
            session.PlayerBUid = userData.UID_RIGHT;

            ApplyPiecesToSession(session, colMap, firstRow);
            ApplyDefaultsToSession(session, userData);

            session.CurrentUserType = DetermineUserType(userData.CARTRIDGE, userData.RELATION);

            int endCount = CalculateClearedEndCount(columns, colMap, firstRow);
            session.ClearedEndCount = endCount;
            session.IsOtherCartridgeContentsCleared = (endCount >= 3);

            Debug.Log($"타 콘텐츠 완료 개수: {endCount}개 (Z계열 제외, 3개 이상 완료 판정: {session.IsOtherCartridgeContentsCleared})");
        }
        
        /// <summary> 마음 조각 획득 데이터를 세션에 반영함. </summary>
        private void ApplyPiecesToSession(SessionManager session, Dictionary<string, int> colMap, List<object> row)
        {
            session.PieceA1 = ParseIntSafe(colMap, row, "PIECE_A1");
            session.PieceA2 = ParseIntSafe(colMap, row, "PIECE_A2");
            session.PieceA3 = ParseIntSafe(colMap, row, "PIECE_A3");
            session.PieceB1 = ParseIntSafe(colMap, row, "PIECE_B1");
            session.PieceB2 = ParseIntSafe(colMap, row, "PIECE_B2");
            session.PieceB3 = ParseIntSafe(colMap, row, "PIECE_B3");
            session.PieceC1 = ParseIntSafe(colMap, row, "PIECE_C1");
            session.PieceC2 = ParseIntSafe(colMap, row, "PIECE_C2");
            session.PieceC3 = ParseIntSafe(colMap, row, "PIECE_C3");
            session.PieceD1 = ParseIntSafe(colMap, row, "PIECE_D1");
            session.PieceD2 = ParseIntSafe(colMap, row, "PIECE_D2");
            session.PieceD3 = ParseIntSafe(colMap, row, "PIECE_D3");
        }
        
        /// <summary> 누락 가능성이 있는 텍스트/언어 데이터에 기본값(Fallback)을 적용함. </summary>
        private void ApplyDefaultsToSession(SessionManager session, UserData userData)
        {
            session.CurrentLanguage = !string.IsNullOrWhiteSpace(userData.LANG) ? userData.LANG.Trim() : GetFallback("LANG", "ko");
            session.PlayerAFirstName = !string.IsNullOrWhiteSpace(userData.RESERVATION_FIRST_NAME_LEFT) ? userData.RESERVATION_FIRST_NAME_LEFT.Trim() : GetFallback("RESERVATION_FIRST_NAME_LEFT", "NoNameA");
            session.PlayerBFirstName = !string.IsNullOrWhiteSpace(userData.RESERVATION_FIRST_NAME_RIGHT) ? userData.RESERVATION_FIRST_NAME_RIGHT.Trim() : GetFallback("RESERVATION_FIRST_NAME_RIGHT", "NoNameB");

            session.PlayerAColor = userData.COLOR_LEFT;
            session.PlayerBColor = userData.COLOR_RIGHT; 
        }
        
        private string GetFallback(string fieldName, string fallbackValue)
        {
            Debug.LogWarning($"{fieldName} 누락됨. 기본값 '{fallbackValue}' 적용.");
            return fallbackValue;
        }
        
        /// <summary> 
        /// 런타임 가비지 할당(GC) 없이 카트리지 문자와 관계 번호를 조합하여 UserType 열거형을 안전하게 반환함. 
        /// </summary>
        private UserType DetermineUserType(string cartridge, int relation)
        {
            int cartIndex = GetCartridgeIndex(cartridge);
            
            // 관계 번호 (1~5)를 캐싱 배열 인덱스 (0~4)로 변환
            int relIndex = (relation < 1 || relation > 5) ? 0 : relation - 1;

            return UserTypeCache[cartIndex, relIndex];
        }

        /// <summary>
        /// 문자열에서 첫 유효 알파벳을 검사하여 카트리지 배열 인덱스(0~3)를 반환함.
        /// </summary>
        private int GetCartridgeIndex(string cartridge)
        {
            if (string.IsNullOrEmpty(cartridge)) return 0;

            for (int i = 0; i < cartridge.Length; i++)
            {
                char c = cartridge[i];
                if (char.IsWhiteSpace(c)) continue;

                switch (c)
                {
                    case 'b': case 'B': return 1;
                    case 'c': case 'C': return 2;
                    case 'd': case 'D': return 3;
                    default: return 0; // 'a', 'A' 및 기타 예외 문자는 기본값(0) 처리
                }
            }

            return 0; // 유효 문자가 없을 경우 기본값
        }
        
        /// <summary> 
        /// 타 콘텐츠 클리어 여부(END_ 컬럼 존재 여부)를 검사하여 완료 개수를 반환함.
        /// </summary>
        private int CalculateClearedEndCount(List<string> columns, Dictionary<string, int> colMap, List<object> row)
        {
            int endCount = 0;
            string currentModuleEnd = $"END_{GameConstants.Module.Code.ToUpper()}";

            foreach (string colName in columns)
            {
                if (!colName.StartsWith("END_")) continue;
                if (colName.Equals(currentModuleEnd, StringComparison.OrdinalIgnoreCase) ||
                    colName.StartsWith("END_Z", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string endValue = ParseStringSafe(colMap, row, colName);

                if (!string.IsNullOrWhiteSpace(endValue) && !endValue.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    endCount++;
                }
            }

            return endCount;
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