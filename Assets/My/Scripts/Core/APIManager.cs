using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Cysharp.Text;
using Microsoft.Extensions.Logging;
using ZLogger;
using My.Scripts.Core.Data;
using My.Scripts.Hardware;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using My.Scripts.Global;
using VContainer;
using Wonjeong.Utils;

namespace My.Scripts.Core
{
    public enum ColorData
    {
        NotSet = -1,
        Cyan = 0,
        Pink = 1,
        Orange = 2,
        Green = 3,
        Red = 4,
        Yellow = 5
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

        public int PIECE_A1;
        public int PIECE_A2;
        public int PIECE_A3;
        public int PIECE_B1;
        public int PIECE_B2;
        public int PIECE_B3;
        public int PIECE_C1;
        public int PIECE_C2;
        public int PIECE_C3;
        public int PIECE_D1;
        public int PIECE_D2;
        public int PIECE_D3;
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

        [Header("API Retry Settings")]
        [SerializeField] private int maxRetries;

        [SerializeField] private float retryDelay;

        // --- 의존성 주입 (DI) 변수 ---
        private ILogger<APIManager> _logger;
        private GameManager _gameManager;
        private SessionManager _sessionManager;

        /// <summary>
        /// VContainer를 통한 핵심 의존 모듈 주입 메소드.
        /// </summary>
        [Inject]
        public void Construct(
            ILogger<APIManager> logger,
            GameManager gameManager,
            SessionManager sessionManager)
        {
            _logger = logger;
            _gameManager = gameManager;
            _sessionManager = sessionManager;
        }

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
            if (!_sessionManager)
            {   
                _logger.ZLogError($"sessionManager가 존재하지 않음.");
                return;
            }

            _sessionManager.CurrentUserId = 1;
            _sessionManager.PlayerAFirstName = "fork";
            _sessionManager.PlayerBFirstName = "you";
            _sessionManager.PlayerAColor = ColorData.Green;
            _sessionManager.PlayerBColor = ColorData.Yellow;
            _sessionManager.CurrentLanguage = "ko";
            _sessionManager.CurrentUserType = UserType.A2;
            _sessionManager.BlockCode = "A1,B1,C1,D1";

            _logger.ZLogInformation($"[Debug] 테스트 세션 주입 완료");
        }
#endif

        /// <summary>
        /// API 서버에 유저 데이터를 요청하고 네트워크 실패 시 지정된 횟수만큼 재시도함.
        /// </summary>
        /// <param name="uid">조회할 유저의 고유 식별자</param>
        /// <returns>조회 및 처리 성공 여부</returns>
        public async UniTask<bool> FetchDataAsync(string uid)
        {
            ApiSettings config = EnsureApiConfigLoaded();

            if (config == null || string.IsNullOrEmpty(config.GetUserUrl))
            {
                _logger.ZLogError($"API 설정을 찾을 수 없거나 GetUserUrl이 누락되었습니다.");
                return false;
            }

            string requestUrl = ZString.Format("{0}?uid={1}", config.GetUserUrl, uid);

            return await ExecuteFetchRequestAsync(requestUrl);
        }

        /// <summary>
        /// API 설정(ApiSettings)이 로드되어 있는지 확인하고, 없을 경우 JSON에서 동적으로 로드함.
        /// </summary>
        private ApiSettings EnsureApiConfigLoaded()
        {
            if (_gameManager && _gameManager.ApiConfig != null)
            {
                return _gameManager.ApiConfig;
            }

            string currentLang = _sessionManager ? _sessionManager.CurrentLanguage : "ko";
            string apiPath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.ApiSetting, currentLang);
            ApiSettings config = JsonLoader.Load<ApiSettings>(apiPath);

            if (_gameManager && config != null)
            {
                _gameManager.ApiConfig = config;
            }

            return config;
        }

        /// <summary>
        /// 실제 HTTP GET 요청을 수행하고 결과를 파싱함.
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
                        _logger.ZLogWarning(
                            $"유저 데이터 조회 실패 ({attempt + 1}/{maxRetries}): {webRequest.error}. {retryDelay}초 후 재시도.");
                        await UniTask.Delay(TimeSpan.FromSeconds(retryDelay));
                    }
                    else
                    {
                        _logger.ZLogError($"유저 데이터 조회 최종 실패: {webRequest.error}");
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 응답받은 JSON 문자열을 역직렬화하고 세션 매니저 객체에 값을 매핑함.
        /// </summary>
        public async UniTask<bool> ParseAndProcessDataAsync(string jsonString)
        {
            try
            {
                ApiTableResponse response =
                    await UniTask.RunOnThreadPool(() => JsonConvert.DeserializeObject<ApiTableResponse>(jsonString));

                if (!IsValidResponse(response))
                {
                    return false;
                }

                Dictionary<string, int> colMap = BuildColumnMap(response.COLUMNS);
                List<object> firstRow = response.DATA[0];

                UserData userData = ExtractUserData(colMap, firstRow);
                LogExtractedData(userData);

                if (_sessionManager)
                {
                    ApplyToSession(userData, response.COLUMNS, colMap, firstRow);
                }

                return true;
            }
            catch (Exception e)
            {
                _logger.ZLogError($"JSON 파싱 중 에러 발생: {e.Message}");
                return false;
            }
        }

        private bool IsValidResponse(ApiTableResponse response)
        {
            return response != null && response.DATA != null && response.DATA.Count > 0 && response.COLUMNS != null;
        }

        private Dictionary<string, int> BuildColumnMap(List<string> columns)
        {
            Dictionary<string, int> colMap = new Dictionary<string, int>();
            for (int i = 0; i < columns.Count; i++)
            {
                colMap[columns[i]] = i;
            }

            return colMap;
        }

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

        private void LogExtractedData(UserData data)
        {
            _logger.ZLogInformation($@"유저 데이터 로드 완료
- 유저 인덱스(IDX_USER): {data.IDX_USER}
- 이름 (L/R): {data.RESERVATION_FIRST_NAME_LEFT} / {data.RESERVATION_FIRST_NAME_RIGHT}
- UID (L/R): {data.UID_LEFT} / {data.UID_RIGHT}
- 컬러 (L/R): {data.COLOR_LEFT} / {data.COLOR_RIGHT}
- 언어/관계: {data.LANG} / {data.RELATION}
- 카트리지: {data.CARTRIDGE}
- 블록 코드: {data.BLOCK_CODE}");
        }

        private void ApplyToSession(UserData userData, List<string> columns, Dictionary<string, int> colMap,
            List<object> firstRow)
        {
            _sessionManager.CurrentUserId = userData.IDX_USER;
            _sessionManager.BlockCode = userData.BLOCK_CODE;
            _sessionManager.Cartridge = userData.CARTRIDGE;
            _sessionManager.PlayerAUid = userData.UID_LEFT;
            _sessionManager.PlayerBUid = userData.UID_RIGHT;

            ApplyPiecesToSession(_sessionManager, colMap, firstRow);
            ApplyDefaultsToSession(_sessionManager, userData);

            _sessionManager.CurrentUserType = DetermineUserType(userData.CARTRIDGE, userData.RELATION);

            int endCount = CalculateClearedEndCount(columns, colMap, firstRow);
            _sessionManager.ClearedEndCount = endCount;
            _sessionManager.IsOtherCartridgeContentsCleared = (endCount >= 3);

            _logger.ZLogInformation(
                $"타 콘텐츠 완료 개수: {endCount}개 (3개 이상 완료 판정: {_sessionManager.IsOtherCartridgeContentsCleared})");
        }

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

        private void ApplyDefaultsToSession(SessionManager session, UserData userData)
        {
            session.CurrentLanguage = !string.IsNullOrWhiteSpace(userData.LANG)
                ? userData.LANG.Trim()
                : GetFallback("LANG", "ko");
            session.PlayerAFirstName = !string.IsNullOrWhiteSpace(userData.RESERVATION_FIRST_NAME_LEFT)
                ? userData.RESERVATION_FIRST_NAME_LEFT.Trim()
                : GetFallback("RESERVATION_FIRST_NAME_LEFT", "NoNameA");
            session.PlayerBFirstName = !string.IsNullOrWhiteSpace(userData.RESERVATION_FIRST_NAME_RIGHT)
                ? userData.RESERVATION_FIRST_NAME_RIGHT.Trim()
                : GetFallback("RESERVATION_FIRST_NAME_RIGHT", "NoNameB");

            session.PlayerAColor = userData.COLOR_LEFT;
            session.PlayerBColor = userData.COLOR_RIGHT;
        }

        private string GetFallback(string fieldName, string fallbackValue)
        {
            _logger.ZLogWarning($"{fieldName} 누락됨. 기본값 '{fallbackValue}' 적용.");
            return fallbackValue;
        }

        private UserType DetermineUserType(string cartridge, int relation)
        {
            int cartIndex = GetCartridgeIndex(cartridge);
            int relIndex = (relation < 1 || relation > 5) ? 0 : relation - 1;

            return UserTypeCache[cartIndex, relIndex];
        }

        private int GetCartridgeIndex(string cartridge)
        {
            if (string.IsNullOrEmpty(cartridge)) return 0;

            for (int i = 0; i < cartridge.Length; i++)
            {
                char c = cartridge[i];
                if (char.IsWhiteSpace(c)) continue;

                switch (c)
                {
                    case 'b':
                    case 'B': return 1;
                    case 'c':
                    case 'C': return 2;
                    case 'd':
                    case 'D': return 3;
                    default: return 0;
                }
            }

            return 0;
        }

        private int CalculateClearedEndCount(List<string> columns, Dictionary<string, int> colMap, List<object> row)
        {
            int endCount = 0;
            string currentModuleEnd = ZString.Concat("END_", GameConstants.Module.Code.ToUpper());

            foreach (string colName in columns)
            {
                if (!colName.StartsWith("END_")) continue;

                if (colName.Equals(currentModuleEnd, StringComparison.OrdinalIgnoreCase) ||
                    colName.StartsWith("END_Z", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string endValue = ParseStringSafe(colMap, row, colName);

                if (!string.IsNullOrWhiteSpace(endValue) &&
                    !endValue.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    endCount++;
                }
            }

            return endCount;
        }

        private int ParseIntSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null)
            {
                string valStr = row[idx].ToString().Trim();
                if (int.TryParse(valStr, out int val)) return val;
            }

            return 0;
        }

        private string ParseStringSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null)
                return row[idx].ToString();

            return string.Empty;
        }

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