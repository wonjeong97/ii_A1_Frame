using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks; // 비동기 JSON 파싱을 위해 추가
using My.Scripts.Core.Data;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json; 
using My.Scripts.Global;
using Wonjeong.Utils;

namespace My.Scripts.Core
{
    /// <summary> 서버에서 전달되는 플레이어 고유 색상 코드를 로컬 환경에 맞게 매핑하기 위한 열거형입니다. </summary>
    public enum ColorData
    {   
        NotSet = -1,
        Cyan = 0, Pink = 1, Orange = 2, Green = 3, Red = 4, Yellow = 5
    }
    
    /// <summary> API 응답 데이터 중 세션 관리에 필요한 핵심 유저 정보만 추출하여 보관하는 구조체입니다. </summary>
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

    /// <summary> 서버의 JSON 테이블 구조(COLUMNS/DATA 배열)를 직렬화/역직렬화하기 위한 데이터 컨테이너입니다. </summary>
    public class ApiTableResponse
    {
        public List<string> COLUMNS { get; set; }
        public List<List<object>> DATA { get; set; } 
    }

    /// <summary> 
    /// 서버 API와 통신하여 유저 데이터를 가져오고, 파싱된 결과를 전역 세션(SessionManager)에 동기화하는 통신 매니저.
    /// 외부 카트리지 시스템과의 연동을 통해 묶음 콘텐츠의 클리어 여부도 함께 확인합니다.
    /// </summary>
    public class APIManager : MonoBehaviour
    {
        private string userUid;

        /// <summary> 외부 모듈에서 UID를 전달받아 통신 시퀀스를 개시합니다. </summary>
        public void FetchData(string uid)
        {
            userUid = uid;
            FetchData();
        }
        
        /// <summary> API 설정값을 로드하고 유저 정보 조회 통신을 시작합니다. (컨텍스트 메뉴를 통한 에디터 테스트 지원) </summary>
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

        /// <summary> 메인 스레드 멈춤 없이 백그라운드에서 HTTP GET 요청을 수행합니다. </summary>
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
                    // 비동기 파싱 호출 (Fire and Forget)
                    ParseAndProcessDataAsync(webRequest.downloadHandler.text).Forget();
                }
            }
        }

        /// <summary> 
        /// 수신된 JSON 텍스트를 구조화된 데이터로 변환하고 전역 세션(SessionManager)에 매핑합니다.
        /// 메인 스레드 프레임 드랍을 막기 위해 UniTask를 활용하여 백그라운드 스레드에서 파싱을 수행합니다.
        /// </summary>
        public async UniTaskVoid ParseAndProcessDataAsync(string jsonString)
        {
            try
            {
                // 스레드 풀에서 무거운 JSON 파싱 수행 후 메인 스레드로 자동 복귀
                ApiTableResponse response = await UniTask.RunOnThreadPool(() => JsonConvert.DeserializeObject<ApiTableResponse>(jsonString));

                if (response != null && response.DATA != null && response.DATA.Count > 0)
                {
                    // 응답 데이터의 첫 번째 행(Row) 추출
                    List<object> firstRow = response.DATA[0];
                    UserData userData = new UserData();

                    // 컬럼 인덱스를 안전하게 탐색하여 데이터 매핑 (구조 변경에 대한 방어 로직)
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

                        if (!string.IsNullOrWhiteSpace(userData.LANG)) SessionManager.Instance.CurrentLanguage = userData.LANG.Trim();

                        // 서버의 관계 코드 정수값을 로컬 게임 클라이언트의 UserType으로 매핑
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
                        
                        // 음수 값에 의한 게임 로직 오류를 막기 위해 최솟값(0) 보정
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
                        
                        // 현재 카트리지 그룹 내 다른 게임들의 완료 여부를 2차로 확인
                        if (!string.IsNullOrWhiteSpace(userData.CARTRIDGE))
                        {
                            StartCoroutine(CheckOtherCartridgeContentsRoutine(userData.CARTRIDGE, response, firstRow));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[APIManager] JSON 파싱 중 에러 발생: {e.Message}");
                if (SessionManager.Instance) SessionManager.Instance.IsOtherCartridgeContentsCleared = false;
            }
        }

        /// <summary> 동일 카트리지에 속한 타 콘텐츠 리스트를 조회하는 2차 API 요청입니다. </summary>
        private IEnumerator CheckOtherCartridgeContentsRoutine(string cartridgeStr, ApiTableResponse firstApiResponse, List<object> firstApiRow)
        {
            if (!GameManager.Instance || GameManager.Instance.ApiConfig == null) yield break;

            string url = $"{GameManager.Instance.ApiConfig.GetCartridgeContentUrl}?cartridge={UnityWebRequest.EscapeURL(cartridgeStr)}";
            
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    string targetListStr = req.downloadHandler.text;
                    if (SessionManager.Instance)
                    {
                        SessionManager.Instance.IsOtherCartridgeContentsCleared = ParseOtherCartridgeClearState(targetListStr, firstApiResponse, firstApiRow);
                    }
                }
                else
                {
                    Debug.LogError($"[APIManager] 카트리지 조회 실패: {req.error}");
                }
            }
        }

        /// <summary> 수신된 카트리지 리스트 문자열을 순회하며, 최초 유저 데이터의 'END_모듈명' 컬럼에 값이 채워져 있는지 검사합니다. </summary>
        private bool ParseOtherCartridgeClearState(string targetListStr, ApiTableResponse firstApiResponse, List<object> firstApiRow)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetListStr) || targetListStr.Trim().StartsWith("<")) return false;

                string[] targetCodes = targetListStr.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string currentModule = SessionManager.Instance ? SessionManager.Instance.CurrentModuleCode.ToUpper() : "A1"; 

                foreach (string target in targetCodes)
                {
                    string expectedCode = target.Trim().ToUpper(); 
                    if (expectedCode == currentModule) continue; // 본인 모듈은 검사에서 제외

                    string endColumnName = $"END_{expectedCode}";
                    string endValue = ParseStringSafe(firstApiResponse, firstApiRow, endColumnName);

                    // 하나라도 완료 시간이 비어있으면(null) 카트리지 전체 클리어 실패로 판정
                    if (string.IsNullOrWhiteSpace(endValue) || endValue.Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        return false; 
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[APIManager] 카트리지 감시 목록 파싱 실패: {e.Message}");
                return false;
            }
        }

        /// <summary> 컬럼 인덱스 범위를 초과하거나 형변환 실패 시 예외를 던지지 않고 0을 반환하는 안전한 정수 파서입니다. </summary>
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

        /// <summary> 데이터가 존재하지 않을 경우 null 대신 빈 문자열을 반환하여 이후 로직의 NRE(NullReferenceException)를 방지합니다. </summary>
        private string ParseStringSafe(ApiTableResponse response, List<object> row, string colName)
        {
            int index = response.COLUMNS.IndexOf(colName);
            if (index != -1 && row.Count > index && row[index] != null) return row[index].ToString();
            return string.Empty; 
        }

        /// <summary> 응답받은 정수가 정의된 ColorData 열거형 범위 내에 있는지 검증 후 반환합니다. 범위 초과 시 NotSet 반환. </summary>
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