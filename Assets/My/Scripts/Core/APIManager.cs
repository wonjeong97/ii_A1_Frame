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

    public class APIManager : MonoBehaviour
    {
        private string userUid;

        [Header("API Retry Settings")]
        [SerializeField] private int maxRetries = 10;
        [SerializeField] private float retryDelay = 1.0f;

        public void FetchData(string uid)
        {
            FetchDataAsync(uid).Forget();
        }
        
        [ContextMenu("Fetch API Data")]
        public async UniTask<bool> FetchDataAsync(string uid)
        {
            userUid = uid;
            ApiSettings config = GameManager.Instance ? GameManager.Instance.ApiConfig : null;

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

            // 전역 변수 사용
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
                        Debug.LogWarning($"[APIManager] 유저 데이터 조회 실패 ({attempt + 1}/{maxRetries}): {webRequest.error}. {retryDelay}초 후 재시도...");
                        await UniTask.Delay(TimeSpan.FromSeconds(retryDelay));
                    }
                    else
                    {
                        Debug.LogError($"[APIManager] 유저 데이터 조회 최종 실패: {webRequest.error}");
                    }
                }
            }
            return false;
        }

        public async UniTask<bool> ParseAndProcessDataAsync(string jsonString)
        {
            try
            {
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
                        
                        userData.PIECE_A1 = ParseIntSafe(colMap, firstRow, "PIECE_A1");
                        userData.PIECE_A2 = ParseIntSafe(colMap, firstRow, "PIECE_A2");
                        userData.PIECE_A3 = ParseIntSafe(colMap, firstRow, "PIECE_A3");
                        userData.PIECE_B1 = ParseIntSafe(colMap, firstRow, "PIECE_B1");
                        userData.PIECE_B2 = ParseIntSafe(colMap, firstRow, "PIECE_B2");
                        userData.PIECE_B3 = ParseIntSafe(colMap, firstRow, "PIECE_B3");
                        userData.PIECE_C1 = ParseIntSafe(colMap, firstRow, "PIECE_C1");
                        userData.PIECE_C2 = ParseIntSafe(colMap, firstRow, "PIECE_C2");
                        userData.PIECE_C3 = ParseIntSafe(colMap, firstRow, "PIECE_C3");
                        userData.PIECE_D1 = ParseIntSafe(colMap, firstRow, "PIECE_D1");
                        userData.PIECE_D2 = ParseIntSafe(colMap, firstRow, "PIECE_D2");
                        userData.PIECE_D3 = ParseIntSafe(colMap, firstRow, "PIECE_D3");
                        
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

            // 전역 변수 사용
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
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
                        return; // 성공 시 즉시 종료
                    }

                    if (attempt < maxRetries - 1)
                    {
                        Debug.LogWarning($"[APIManager] 카트리지 상태 조회 실패 ({attempt + 1}/{maxRetries}): {req.error}. {retryDelay}초 후 재시도...");
                        await UniTask.Delay(TimeSpan.FromSeconds(retryDelay));
                    }
                    else
                    {
                        Debug.LogError($"[APIManager] 카트리지 상태 조회 최종 실패: {req.error}");
                    }
                }
            }
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