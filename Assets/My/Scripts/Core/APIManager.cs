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

                        // 초기화 시점에 현재 모듈을 제외한 나머지 카트리지 콘텐츠가 완료되었는지 검사
                        if (!string.IsNullOrWhiteSpace(userData.CARTRIDGE))
                        {
                            StartCoroutine(CheckOtherCartridgeContentsRoutine(userData.CARTRIDGE));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[APIManager] 파싱 중 에러 발생: {e.Message}");
            }
        }

        //  카트리지 내용 조회 후 A1 제외 나머지 클리어 여부 확인 코루틴
        private IEnumerator CheckOtherCartridgeContentsRoutine(string cartridgeStr)
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
                    GameManager.Instance.IsOtherCartridgeContentsCleared = ParseOtherCartridgeClearState(req.downloadHandler.text, cartridgeStr);
                }
                else
                {
                    Debug.LogError($"[APIManager] 카트리지 조회 실패: {req.error}");
                }
            }
        }

        // A1(현재 모듈)을 제외하고 카트리지의 다른 내용들이 모두 End 값이 있는지 확인
        private bool ParseOtherCartridgeClearState(string json, string cartridgeStr)
        {
            try
            {
                ApiTableResponse response = JsonConvert.DeserializeObject<ApiTableResponse>(json);
                if (response != null && response.DATA != null && response.DATA.Count > 0)
                {
                    List<object> row = response.DATA[0];
                    string[] codes = cartridgeStr.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string currentModule = GameManager.Instance.CurrentModuleCode.ToUpper();

                    foreach (var code in codes)
                    {
                        string c = code.Trim().ToUpper();
                        
                        // 현재 플레이 중인 모듈(A1)은 검사하지 않고 패스
                        if (c == currentModule) continue; 

                        string val = ParseStringSafe(response, row, $"END_{c}");
                        
                        // 현재 콘텐츠를 제외한 나머지 중 하나라도 완료(END) 안 된 게 있다면 바로 일반 엔딩 처리
                        if (string.IsNullOrWhiteSpace(val) || val.Equals("null", StringComparison.OrdinalIgnoreCase))
                        {
                            return false; 
                        }
                    }
                    return true; // A1을 뺀 나머지 콘텐츠가 모두 완료됨
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[APIManager] 카트리지 상태 파싱 실패: {e.Message}");
            }
            return false;
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