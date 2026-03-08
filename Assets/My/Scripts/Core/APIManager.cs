using System;
using System.Collections;
using System.Collections.Generic;
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

        public void FetchData(string uid)
        {
            userUid = uid;
            FetchData();
        }
        
        [ContextMenu("Fetch API Data")]
        public void FetchData()
        {
            ApiSettings config = null;
            if (GameManager.Instance) config = GameManager.Instance.ApiConfig;
            if (config == null) config = JsonLoader.Load<ApiSettings>(GameConstants.Path.ApiSetting);

            if (config == null)
            {
                Debug.LogError("[APIManager] API 설정을 찾을 수 없습니다.");
                return;
            }

            string requestUrl = $"{config.GetUserUrl}?uid={userUid}";
            StartCoroutine(GetApiDataRoutine(requestUrl));
        }

        private IEnumerator GetApiDataRoutine(string url)
        {
            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                webRequest.timeout = 10; 
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                    webRequest.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"[APIManager] 통신 실패: {webRequest.error}");
                }
                else
                {
                    ParseAndProcessData(webRequest.downloadHandler.text);
                }
            }
        }

        public void ParseAndProcessData(string jsonString)
        {
            try
            {
                ApiTableResponse response = JsonConvert.DeserializeObject<ApiTableResponse>(jsonString);

                if (response != null && response.DATA != null && response.DATA.Count > 0)
                {
                    List<object> firstRow = response.DATA[0];
                    UserData userData = new UserData();

                    userData.IDX_USER = ParseIntSafe(response, firstRow, "IDX_USER");
                    userData.CARTRIDGE = ParseStringSafe(response, firstRow, "CARTRIDGE"); 
                    
                    userData.UID_LEFT = ParseStringSafe(response, firstRow, "UID_LEFT");
                    userData.UID_RIGHT = ParseStringSafe(response, firstRow, "UID_RIGHT");
                    userData.LANG = ParseStringSafe(response, firstRow, "LANG");
                    userData.RELATION = ParseIntSafe(response, firstRow, "RELATION");

                    userData.RESERVATION_LAST_NAME_LEFT = ParseStringSafe(response, firstRow, "RESERVATION_LAST_NAME_LEFT");
                    userData.RESERVATION_LAST_NAME_RIGHT = ParseStringSafe(response, firstRow, "RESERVATION_LAST_NAME_RIGHT");
                    
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

                    if (GameManager.Instance)
                    {   
                        GameManager.Instance.CurrentUserId = userData.IDX_USER;
                        GameManager.Instance.Cartridge = userData.CARTRIDGE; 
                        
                        GameManager.Instance.PlayerAUid = userData.UID_LEFT;
                        GameManager.Instance.PlayerBUid = userData.UID_RIGHT;

                        if (!string.IsNullOrWhiteSpace(userData.LANG)) GameManager.Instance.CurrentLanguage = userData.LANG.Trim();

                        switch (userData.RELATION)
                        {
                            case 1: GameManager.Instance.currentUserType = UserType.A; break;
                            case 2: GameManager.Instance.currentUserType = UserType.B; break;
                            case 3: GameManager.Instance.currentUserType = UserType.C; break;
                            case 4: GameManager.Instance.currentUserType = UserType.D; break;
                            case 5: GameManager.Instance.currentUserType = UserType.E; break;
                            case 6: GameManager.Instance.currentUserType = UserType.F; break;
                            default: GameManager.Instance.currentUserType = UserType.A; break;
                        }

                        if (!string.IsNullOrEmpty(userData.RESERVATION_LAST_NAME_LEFT))
                            GameManager.Instance.PlayerALastName = userData.RESERVATION_LAST_NAME_LEFT;
                        if (!string.IsNullOrEmpty(userData.RESERVATION_LAST_NAME_RIGHT))
                            GameManager.Instance.PlayerBLastName = userData.RESERVATION_LAST_NAME_RIGHT;
                        
                        GameManager.Instance.PlayerAColor = userData.COLOR_LEFT;
                        GameManager.Instance.PlayerBColor = userData.COLOR_RIGHT;
                        
                        GameManager.Instance.PieceA1 = Mathf.Max(0, userData.PIECE_A1);
                        GameManager.Instance.PieceA2 = Mathf.Max(0, userData.PIECE_A2);
                        GameManager.Instance.PieceA3 = Mathf.Max(0, userData.PIECE_A3);
                        GameManager.Instance.PieceB1 = Mathf.Max(0, userData.PIECE_B1);
                        GameManager.Instance.PieceB2 = Mathf.Max(0, userData.PIECE_B2);
                        GameManager.Instance.PieceB3 = Mathf.Max(0, userData.PIECE_B3);
                        GameManager.Instance.PieceC1 = Mathf.Max(0, userData.PIECE_C1);
                        GameManager.Instance.PieceC2 = Mathf.Max(0, userData.PIECE_C2);
                        GameManager.Instance.PieceC3 = Mathf.Max(0, userData.PIECE_C3);
                        GameManager.Instance.PieceD1 = Mathf.Max(0, userData.PIECE_D1);
                        GameManager.Instance.PieceD2 = Mathf.Max(0, userData.PIECE_D2);
                        GameManager.Instance.PieceD3 = Mathf.Max(0, userData.PIECE_D3);

                        GameManager.Instance.IsOtherCartridgeContentsCleared = false;
                        if (!string.IsNullOrWhiteSpace(userData.CARTRIDGE))
                        {
                            // 첫 번째 API에서 파싱해둔 response 전체 원본(END 값들)을 코루틴으로 함께 넘깁니다.
                            StartCoroutine(CheckOtherCartridgeContentsRoutine(userData.CARTRIDGE, response, firstRow));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[APIManager] 유저 데이터 JSON 파싱 중 에러 발생: {e.Message}");
                if (GameManager.Instance) GameManager.Instance.IsOtherCartridgeContentsCleared = false;
            }
        }

        // 두 번째 API를 호출하여 "감시해야 할 타겟 목록" 문자열을 받아옵니다.
        private IEnumerator CheckOtherCartridgeContentsRoutine(string cartridgeStr, ApiTableResponse firstApiResponse, List<object> firstApiRow)
        {
            ApiSettings config = GameManager.Instance.ApiConfig;
            if (config == null) yield break;

            string url = $"{config.GetCartridgeContentUrl}?cartridge={UnityWebRequest.EscapeURL(cartridgeStr)}";
            
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    // 두 번째 API의 응답(예: "A1,B1,C1")
                    string targetListStr = req.downloadHandler.text;

                    // 감시할 목록(targetListStr)과 END 값들이 들어있는 첫 번째 API 원본(firstApiResponse)을 엮어서 비교합니다.
                    GameManager.Instance.IsOtherCartridgeContentsCleared = ParseOtherCartridgeClearState(targetListStr, firstApiResponse, firstApiRow);
                }
                else
                {
                    Debug.LogError($"[APIManager] 카트리지 조회 실패: {req.error}");
                }
            }
        }

        /// <summary>
        /// 두 번째 API에서 받은 목록("A1, B1...")을 순회하며, 
        /// 첫 번째 API의 원본 JSON 데이터에서 해당 모듈들의 END 값이 존재하는지 검사합니다.
        /// </summary>
        private bool ParseOtherCartridgeClearState(string targetListStr, ApiTableResponse firstApiResponse, List<object> firstApiRow)
        {
            try
            {
                Debug.Log($"[APIManager] 두 번째 API(감시 목록) 응답 데이터: {targetListStr}");

                // 비어있거나 HTML 에러가 왔다면 감시 불가 처리
                if (string.IsNullOrWhiteSpace(targetListStr) || targetListStr.Trim().StartsWith("<")) return false;

                // 1. 감시해야 할 타겟 목록을 분리합니다. (예: "A1", "B1", "C1")
                string[] targetCodes = targetListStr.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
                string currentModule = "A1"; 
                if (GameManager.Instance && !string.IsNullOrEmpty(GameManager.Instance.CurrentModuleCode))
                {
                    currentModule = GameManager.Instance.CurrentModuleCode.ToUpper();
                }

                // 2. 타겟 목록을 하나씩 돕니다.
                foreach (string target in targetCodes)
                {
                    string expectedCode = target.Trim().ToUpper(); 
                    
                    // 현재 플레이 중인 모듈(예: A1)은 감시 대상에서 제외
                    if (expectedCode == currentModule) continue;

                    // 3. 첫 번째 API 데이터(firstApiResponse)에서 "END_B1", "END_C1" 등의 값을 콕 집어서 뽑아옵니다.
                    string endColumnName = $"END_{expectedCode}";
                    string endValue = ParseStringSafe(firstApiResponse, firstApiRow, endColumnName);

                    // 4. END 값이 비어있거나 "null" 텍스트라면 아직 클리어하지 않은 것!
                    if (string.IsNullOrWhiteSpace(endValue) || endValue.Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log($"[APIManager] 특별 엔딩 불가: 감시 대상 모듈 {expectedCode}이(가) 아직 클리어되지 않았습니다. (END 값이 없음)");
                        return false; 
                    }
                }

                // 모든 타겟 모듈의 END 값이 채워져 있음
                Debug.Log("[APIManager] 감시 대상 카트리지 목록의 모든 콘텐츠 클리어 확인됨! (특별 엔딩 조건 충족)");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[APIManager] 카트리지 감시 목록 파싱 실패: {e.Message}");
                return false;
            }
        }

        private int ParseIntSafe(ApiTableResponse response, List<object> row, string colName)
        {
            int index = response.COLUMNS.IndexOf(colName);
            if (index != -1 && row.Count > index && row[index] != null)
            {
                string valStr = row[index].ToString().Trim();
                if (string.IsNullOrEmpty(valStr)) return 0;
                if (int.TryParse(valStr, out int val)) return val;
                if (float.TryParse(valStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float fVal)) return (int)fVal;
            }
            return 0; 
        }

        private string ParseStringSafe(ApiTableResponse response, List<object> row, string colName)
        {
            int index = response.COLUMNS.IndexOf(colName);
            if (index != -1 && row.Count > index && row[index] != null) return row[index].ToString();
            return string.Empty; 
        }

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
    }
}