using System;
using System.Text;
using System.Collections.Generic; 
using System.Threading;
using System.Text.RegularExpressions; 
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using My.Scripts.Global;
using Wonjeong.Utils;

namespace My.Scripts.Hardware
{
    [Serializable]
    public class RGBColor
    {
        public int r;
        public int g;
        public int b;
    }

    [Serializable]
    public class HueConfig
    {
        public string bridgeIp;
        public string apiKey;
        
        public int defaultBrightness;
        public int defaultSaturation;
        
        public RGBColor whiteColor;
        public RGBColor color1;
        public RGBColor color2;
        public RGBColor color3;
        public RGBColor color4;
        public RGBColor color5;
    }

    /// <summary>
    /// 로컬 네트워크상의 필립스 휴(Philips Hue) 브릿지와 통신하여 물리 조명의 색상 및 전원을 제어함.
    /// </summary>
    public class HueManager : MonoBehaviour
    {
        public static HueManager Instance;

        public HueConfig Config { get; private set; }

        private List<RGBColor> _shuffledColors;
        private int _colorIndex = 0;

        private CancellationTokenSource _debugCts;

        private List<int> _physicalLightIds = new List<int>();
        private bool _isFetchingLights = false;
        private bool _hasFetchedLights = false;

        /// <summary>
        /// 싱글톤 인스턴스를 초기화하고 씬 전환 시 파괴되지 않도록 설정하며 환경설정 로드를 트리거함.
        /// </summary>
        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadConfig();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Hue 설정 데이터를 JSON에서 로드하고 물리 조명 ID 캐싱 프로세스를 비동기로 시작함.
        /// </summary>
        private void LoadConfig()
        {
            string huePath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.HueConfig);
            Config = JsonLoader.Load<HueConfig>(huePath);
            
            if (Config == null)
            {
                Debug.LogError($"{huePath}.json 파일을 찾을 수 없음. Hue 기능이 비활성화됨.");
            }
            else
            {
                Debug.Log($"휴 설정 로드 완료 (IP: {Config.bridgeIp})");
                EnsureLightIdsFetchedAsync().Forget();
            }
        }

        /// <summary>
        /// 휴 브릿지의 API를 호출하여 현재 네트워크에 연결된 조명 장치들의 고유 식별 번호를 캐싱함.
        /// </summary>
        /// <param name="ct">비동기 작업 취소 토큰</param>
        private async UniTask EnsureLightIdsFetchedAsync(CancellationToken ct = default)
        {
#if UNITY_EDITOR
            if (!_hasFetchedLights)
            {
                _physicalLightIds = new List<int> { 1, 2, 3, 4, 5 };
                _hasFetchedLights = true;
            }
            return;
#endif

            if (_hasFetchedLights) return; 
            
            if (_isFetchingLights) 
            {
                await UniTask.WaitUntil(() => !_isFetchingLights, cancellationToken: ct);
                return;
            }

            _isFetchingLights = true;

            try
            {
                if (Config == null || string.IsNullOrEmpty(Config.bridgeIp) || string.IsNullOrEmpty(Config.apiKey))
                {
                    return;
                }

                // # TODO: 잦은 URL 문자열 할당을 방지하기 위해 BaseURL 변수를 캐싱하여 재사용할 것.
                string url = $"http://{Config.bridgeIp}/api/{Config.apiKey}/lights";
                int maxRetries = 10;
                float retryDelay = 1.0f;

                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    ct.ThrowIfCancellationRequested();

                    using (UnityWebRequest request = UnityWebRequest.Get(url))
                    {
                        request.timeout = 10;
                        
                        try
                        {
                            UnityWebRequestAsyncOperation op = request.SendWebRequest();
                            
                            using (ct.Register(() => { if (!op.isDone) request.Abort(); }))
                            {
                                await op.ToUniTask();
                            }

                            if (request.result == UnityWebRequest.Result.Success)
                            {
                                string json = request.downloadHandler.text;
                                _physicalLightIds.Clear();

                                // # TODO: Regex 객체를 전역 정적 변수로 빼내어 매 호출 시 정규식 컴파일 오버헤드를 줄일 것.
                                MatchCollection matches = Regex.Matches(json, "\"(\\d+)\":\\s*\\{");
                                foreach (Match match in matches)
                                {
                                    if (int.TryParse(match.Groups[1].Value, out int id))
                                    {
                                        if (!_physicalLightIds.Contains(id)) _physicalLightIds.Add(id);
                                    }
                                }
                                
                                _physicalLightIds.Sort(); 
                                Debug.Log($"동적 조명 ID 자동 매핑 완료: {string.Join(", ", _physicalLightIds)}");
                                
                                _hasFetchedLights = true;
                                return; 
                            }

                            if (attempt < maxRetries - 1)
                            {
                                Debug.LogWarning($"조명 목록 조회 실패 ({attempt + 1}/{maxRetries}): {request.error}. {retryDelay}초 후 재시도.");
                                await UniTask.Delay(TimeSpan.FromSeconds(retryDelay), cancellationToken: ct);
                            }
                            else
                            {
                                Debug.LogError($"조명 목록 조회 최종 실패: {request.error}");
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            Debug.Log("조명 목록 조회 취소됨");
                            throw; 
                        }
                        catch (Exception e)
                        {
                            if (attempt < maxRetries - 1)
                            {
                                Debug.LogWarning($"조명 목록 조회 중 예외 ({attempt + 1}/{maxRetries}): {e.Message}. {retryDelay}초 후 재시도.");
                                await UniTask.Delay(TimeSpan.FromSeconds(retryDelay), cancellationToken: ct);
                            }
                            else
                            {
                                Debug.LogError($"조명 목록 조회 중 최종 예외 발생: {e.Message}");
                                throw; 
                            }
                        }
                    }
                }
            }
            finally
            {
                _isFetchingLights = false;
            }
        }
        
        /// <summary>
        /// 앱 내 논리적 조명 순번을 휴 브릿지에 등록된 실제 물리적 장치 ID로 변환함.
        /// </summary>
        /// <param name="logicalId">앱 내부 논리 조명 번호 (1부터 시작)</param>
        private int GetPhysicalLightId(int logicalId)
        {
            if (_physicalLightIds == null || _physicalLightIds.Count == 0)
            {
                Debug.LogWarning($"조명 ID 매핑 실패: 물리 조명 정보가 비어 있음. (요청된 논리 ID: {logicalId})");
                return -1;
            }
            
            int index = logicalId - 1; 
            if (index >= 0 && index < _physicalLightIds.Count) 
            {
                return _physicalLightIds[index];
            }
            
            Debug.LogWarning($"조명 ID 매핑 실패: 논리 ID {logicalId}에 대응하는 물리 조명이 없음.");
            return -1; 
        }

        /// <summary>
        /// 하드웨어 디버그 키 입력을 감지하여 특정 색상 및 전원 테스트를 수행함.
        /// </summary>
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Z))
            {
                if (Config != null && Config.whiteColor != null)
                {
                    ApplyDebugColor(Config.whiteColor, "White (Z)");
                }
                else
                {
                    Debug.LogWarning("Config.whiteColor가 설정되지 않아 디버그 조명을 변경할 수 없음.");
                }
            }
            else if (Input.GetKeyDown(KeyCode.X))
            {
                if (Config != null && Config.color1 != null) ApplyDebugColor(Config.color1, "Color 1 (X)");
                else Debug.LogWarning("Config.color1 누락됨.");
            }
            else if (Input.GetKeyDown(KeyCode.C))
            {
                if (Config != null && Config.color2 != null) ApplyDebugColor(Config.color2, "Color 2 (C)");
                else Debug.LogWarning("Config.color2 누락됨.");
            }
            else if (Input.GetKeyDown(KeyCode.V))
            {
                if (Config != null && Config.color3 != null) ApplyDebugColor(Config.color3, "Color 3 (V)");
                else Debug.LogWarning("Config.color3 누락됨.");
            }
            else if (Input.GetKeyDown(KeyCode.B))
            {
                if (Config != null && Config.color4 != null) ApplyDebugColor(Config.color4, "Color 4 (B)");
                else Debug.LogWarning("Config.color4 누락됨.");
            }
            else if (Input.GetKeyDown(KeyCode.N))
            {
                if (Config != null && Config.color5 != null) ApplyDebugColor(Config.color5, "Color 5 (N)");
                else Debug.LogWarning("Config.color5 누락됨.");
            }
            else if (Input.GetKeyDown(KeyCode.M))
            {
                ApplyDebugState(false, "Off (M)");
            }
        }

        /// <summary>
        /// 이전 디버그 명령을 취소하고 새로운 RGB 색상 명령을 양쪽 조명에 적용함.
        /// </summary>
        private void ApplyDebugColor(RGBColor color, string logName)
        {
            if (color == null) return;
            
            _debugCts?.Cancel();
            _debugCts?.Dispose();
            _debugCts = new CancellationTokenSource();

            SetLightColorRGBAsync(1, color, -1, 4, _debugCts.Token).Forget();
            SetLightColorRGBAsync(2, color, -1, 4, _debugCts.Token).Forget();
            Debug.Log($"디버그 조명 변경: {logName}");
        }

        /// <summary>
        /// 이전 디버그 명령을 취소하고 조명의 전원 상태를 강제로 변경함.
        /// </summary>
        private void ApplyDebugState(bool isOn, string logName)
        {
            _debugCts?.Cancel();
            _debugCts?.Dispose();
            _debugCts = new CancellationTokenSource();

            SetLightStateAsync(1, isOn, _debugCts.Token).Forget();
            SetLightStateAsync(2, isOn, _debugCts.Token).Forget();
            Debug.Log($"디버그 조명 상태 변경: {logName}");
        }

        /// <summary>
        /// 설정 파일의 5가지 기본 색상 배열을 무작위 순서로 섞어 연출용 큐를 준비함.
        /// </summary>
        public void InitRandomColors()
        {
            if (Config == null)
            {
                Debug.LogWarning("Config 누락으로 무작위 색상 초기화 실패.");
                return;
            }
            
            _shuffledColors = new List<RGBColor> 
            { 
                Config.color1, 
                Config.color2, 
                Config.color3, 
                Config.color4, 
                Config.color5 
            };

            for (int i = 0; i < _shuffledColors.Count; i++)
            {
                int rnd = UnityEngine.Random.Range(i, _shuffledColors.Count);
                RGBColor temp = _shuffledColors[i];
                _shuffledColors[i] = _shuffledColors[rnd];
                _shuffledColors[rnd] = temp;
            }
            _colorIndex = 0;
            Debug.Log("Q6~Q10 랜덤 색상 리스트 셔플 완료");
        }

        /// <summary>
        /// 무작위로 섞인 색상 목록에서 다음 순서의 색상 값을 꺼내 반환함.
        /// </summary>
        public RGBColor PopRandomColor()
        {
            if (_shuffledColors == null || _shuffledColors.Count == 0 || _colorIndex >= _shuffledColors.Count)
            {
                InitRandomColors();
            }
            
            if (_shuffledColors == null || _shuffledColors.Count == 0)
            {
                Debug.LogWarning("Config가 없어 랜덤 색상을 반환할 수 없음.");
                return null;
            }
            
            RGBColor selectedColor = _shuffledColors[_colorIndex];
            _colorIndex++;
            return selectedColor;
        }

        /// <summary>
        /// 지정된 대상 조명의 전원 On/Off 상태를 변경하는 API 명령을 브릿지에 송신함.
        /// </summary>
        public async UniTask SetLightStateAsync(int lightId, bool isOn, CancellationToken ct = default)
        {
            if (Config == null || string.IsNullOrEmpty(Config.bridgeIp) || string.IsNullOrEmpty(Config.apiKey)) return;
            
            await EnsureLightIdsFetchedAsync(ct); 
            int actualId = GetPhysicalLightId(lightId); 

            if (actualId == -1)
            {
                Debug.LogWarning($"SetLightState 취소됨: 논리 조명 {lightId}번에 해당하는 물리 조명을 찾을 수 없음.");
                return;
            }

            string url = $"http://{Config.bridgeIp}/api/{Config.apiKey}/lights/{actualId}/state";
            // # TODO: 문자열 할당을 제거하고 JsonSerializer 또는 사전 정의된 Const 바이트 배열을 전송할 것.
            string jsonBody = "{\"on\":" + (isOn ? "true" : "false") + "}";
            
            if (ArduinoManager.Instance) 
            {
                string command = isOn ? GameConstants.Hardware.CmdLightOn : GameConstants.Hardware.CmdLightOff;
                ArduinoManager.Instance.SendCommandToLight(command);
            }

            await SendPutRequestAsync(url, jsonBody, ct);
        }
        
        /// <summary>
        /// RGB 색상 모델을 필립스 휴 통신 규격인 HSV 모델로 변환하여 조명 색상 변경을 요청함.
        /// </summary>
        public async UniTask SetLightColorRGBAsync(int lightId, RGBColor rgb, int bri = -1, int transitionTime = 4, CancellationToken ct = default)
        {
            if (rgb == null || Config == null) return;
            Color color = new Color(rgb.r / 255f, rgb.g / 255f, rgb.b / 255f);
            Color.RGBToHSV(color, out float h, out float s, out float v);
            
            // ex: h = 0.5 (Cyan 계열) -> 0.5 * 65535 = 32768
            int hueValue = Mathf.RoundToInt(h * 65535f);
            
            // ex: s = 0.8 -> 0.8 * 254 = 203
            int satValue = Mathf.RoundToInt(s * 254f);
            
            int finalBri = (bri == -1) ? Config.defaultBrightness : bri;
            await SetLightColorAsync(lightId, hueValue, satValue, finalBri, transitionTime, ct);
        }

        /// <summary>
        /// 휴, 채도, 밝기의 로우 데이터(Raw Data)를 사용하여 브릿지에 조명 변경 명령을 전송함.
        /// </summary>
        public async UniTask SetLightColorAsync(int lightId, int hue, int sat = -1, int bri = -1, int transitionTime = 4, CancellationToken ct = default)
        {
            if (Config == null || string.IsNullOrEmpty(Config.bridgeIp) || string.IsNullOrEmpty(Config.apiKey)) return;
            
            await EnsureLightIdsFetchedAsync(ct); 
            int actualId = GetPhysicalLightId(lightId); 

            if (actualId == -1)
            {
                Debug.LogWarning($"SetLightColor 취소됨: 논리 조명 {lightId}번에 해당하는 물리 조명을 찾을 수 없음.");
                return;
            }

            int finalSat = (sat == -1) ? Config.defaultSaturation : sat;
            int finalBri = (bri == -1) ? Config.defaultBrightness : bri;

            string url = $"http://{Config.bridgeIp}/api/{Config.apiKey}/lights/{actualId}/state";
            string jsonBody = $"{{\"on\":true, \"bri\":{finalBri}, \"hue\":{hue}, \"sat\":{finalSat}, \"transitiontime\":{transitionTime}}}";
            
            if (ArduinoManager.Instance) 
            {
                ArduinoManager.Instance.SendCommandToLight(GameConstants.Hardware.CmdLightOn);
            }

            await SendPutRequestAsync(url, jsonBody, ct);
        }
        
        /// <summary>
        /// 휴 브릿지에 HTTP PUT 메서드로 명령을 전달하고 연결 상태에 따른 예외를 처리함.
        /// </summary>
        private async UniTask SendPutRequestAsync(string url, string jsonBody, CancellationToken ct = default)
        {
#if UNITY_EDITOR
            return;
#endif

            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 5; 

                try
                {
                    UnityWebRequestAsyncOperation op = request.SendWebRequest();
                    
                    using (ct.Register(() => { if (!op.isDone) request.Abort(); }))
                    {
                        await op.ToUniTask();
                    }

                    if (request.result != UnityWebRequest.Result.Success && !ct.IsCancellationRequested)
                    {
                        Debug.LogError($"통신 실패: {request.error} | URL: {url}");
                    }
                }
                catch (OperationCanceledException)
                {
                    Debug.Log("휴 통신 취소됨");
                }
                catch (Exception e)
                {
                    if (!ct.IsCancellationRequested)
                    {
                        Debug.LogError($"휴 통신 예외 발생: {e.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 객체가 파괴될 때 비동기 통신 토큰을 안전하게 해제하여 메모리 누수 및 오동작을 방지함.
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                _debugCts?.Cancel();
                _debugCts?.Dispose();
            }
        }
    }
}