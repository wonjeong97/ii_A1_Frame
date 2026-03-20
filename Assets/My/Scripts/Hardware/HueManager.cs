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

        private void LoadConfig()
        {
            Config = JsonLoader.Load<HueConfig>("JSON/HueConfig");
            
            if (Config == null)
            {
                Debug.LogError("[HueManager] JSON/HueConfig.json 파일을 찾을 수 없습니다. Hue 기능이 비활성화됩니다.");
            }
            else
            {
                Debug.Log($"[HueManager] 휴 설정 로드 완료 (IP: {Config.bridgeIp})");
                EnsureLightIdsFetchedAsync().Forget();
            }
        }

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

                                MatchCollection matches = Regex.Matches(json, "\"(\\d+)\":\\s*\\{");
                                foreach (Match match in matches)
                                {
                                    if (int.TryParse(match.Groups[1].Value, out int id))
                                    {
                                        if (!_physicalLightIds.Contains(id)) _physicalLightIds.Add(id);
                                    }
                                }
                                
                                _physicalLightIds.Sort(); 
                                Debug.Log($"<color=cyan>[HueManager] 동적 조명 ID 자동 매핑 완료: {string.Join(", ", _physicalLightIds)}</color>");
                                
                                _hasFetchedLights = true;
                                return; 
                            }

                            if (attempt < maxRetries - 1)
                            {
                                Debug.LogWarning($"[HueManager] 조명 목록 조회 실패 ({attempt + 1}/{maxRetries}): {request.error}. {retryDelay}초 후 재시도...");
                                await UniTask.Delay(TimeSpan.FromSeconds(retryDelay), cancellationToken: ct);
                            }
                            else
                            {
                                Debug.LogError($"[HueManager] 조명 목록 조회 최종 실패: {request.error}");
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            Debug.Log("[HueManager] 조명 목록 조회 취소됨");
                            throw; 
                        }
                        catch (Exception e)
                        {
                            if (attempt < maxRetries - 1)
                            {
                                Debug.LogWarning($"[HueManager] 조명 목록 조회 중 예외 ({attempt + 1}/{maxRetries}): {e.Message}. {retryDelay}초 후 재시도...");
                                await UniTask.Delay(TimeSpan.FromSeconds(retryDelay), cancellationToken: ct);
                            }
                            else
                            {
                                Debug.LogError($"[HueManager] 조명 목록 조회 중 최종 예외 발생: {e.Message}");
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
        
        private int GetPhysicalLightId(int logicalId)
        {
            if (_physicalLightIds == null || _physicalLightIds.Count == 0)
            {
                Debug.LogWarning($"[HueManager] 조명 ID 매핑 실패: 물리 조명 정보가 비어 있습니다. (요청된 논리 ID: {logicalId})");
                return -1;
            }
            
            int index = logicalId - 1; 
            if (index >= 0 && index < _physicalLightIds.Count) 
            {
                return _physicalLightIds[index];
            }
            
            Debug.LogWarning($"[HueManager] 조명 ID 매핑 실패: 논리 ID {logicalId}에 대응하는 물리 조명이 없습니다.");
            return -1; 
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Z))
            {
                RGBColor white = (Config != null && Config.whiteColor != null) 
                    ? Config.whiteColor 
                    : new RGBColor { r = 191, g = 239, b = 251 };
                
                ApplyDebugColor(white, "White (Z)");
            }
            else if (Input.GetKeyDown(KeyCode.X))
            {
                if (Config != null) ApplyDebugColor(Config.color1, "Color 1 (X)");
            }
            else if (Input.GetKeyDown(KeyCode.C))
            {
                if (Config != null) ApplyDebugColor(Config.color2, "Color 2 (C)");
            }
            else if (Input.GetKeyDown(KeyCode.V))
            {
                if (Config != null) ApplyDebugColor(Config.color3, "Color 3 (V)");
            }
            else if (Input.GetKeyDown(KeyCode.B))
            {
                if (Config != null) ApplyDebugColor(Config.color4, "Color 4 (B)");
            }
            else if (Input.GetKeyDown(KeyCode.N))
            {
                if (Config != null) ApplyDebugColor(Config.color5, "Color 5 (N)");
            }
            else if (Input.GetKeyDown(KeyCode.M))
            {
                ApplyDebugState(false, "Off (M)");
            }
        }

        private void ApplyDebugColor(RGBColor color, string logName)
        {
            if (color == null) return;
            
            _debugCts?.Cancel();
            _debugCts?.Dispose();
            _debugCts = new CancellationTokenSource();

            SetLightColorRGBAsync(1, color, -1, 4, _debugCts.Token).Forget();
            SetLightColorRGBAsync(2, color, -1, 4, _debugCts.Token).Forget();
            Debug.Log($"[HueManager] 디버그 조명 변경: {logName}");
        }

        private void ApplyDebugState(bool isOn, string logName)
        {
            _debugCts?.Cancel();
            _debugCts?.Dispose();
            _debugCts = new CancellationTokenSource();

            SetLightStateAsync(1, isOn, _debugCts.Token).Forget();
            SetLightStateAsync(2, isOn, _debugCts.Token).Forget();
            Debug.Log($"[HueManager] 디버그 조명 상태 변경: {logName}");
        }

        public void InitRandomColors()
        {
            if (Config == null) return;
            
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
            Debug.Log("[HueManager] Q6~Q10 랜덤 색상 리스트 셔플 완료");
        }

        public RGBColor PopRandomColor()
        {
            if (_shuffledColors == null || _shuffledColors.Count == 0 || _colorIndex >= _shuffledColors.Count)
            {
                InitRandomColors();
            }
            
            if (_shuffledColors == null || _shuffledColors.Count == 0)
            {
                Debug.LogWarning("[HueManager] Config가 없어 랜덤 색상을 반환할 수 없습니다.");
                return null;
            }
            
            RGBColor selectedColor = _shuffledColors[_colorIndex];
            _colorIndex++;
            return selectedColor;
        }

        public async UniTask SetLightStateAsync(int lightId, bool isOn, CancellationToken ct = default)
        {
            if (Config == null || string.IsNullOrEmpty(Config.bridgeIp) || string.IsNullOrEmpty(Config.apiKey)) return;
            
            await EnsureLightIdsFetchedAsync(ct); 
            int actualId = GetPhysicalLightId(lightId); 

            if (actualId == -1)
            {
                Debug.LogWarning($"[HueManager] SetLightState 취소됨: 논리 조명 {lightId}번에 해당하는 물리 조명을 찾을 수 없습니다.");
                return;
            }

            string url = $"http://{Config.bridgeIp}/api/{Config.apiKey}/lights/{actualId}/state";
            string jsonBody = "{\"on\":" + (isOn ? "true" : "false") + "}";
            
            if (ArduinoManager.Instance) 
            {
                string command = isOn ? GameConstants.Hardware.CmdLightOn : GameConstants.Hardware.CmdLightOff;
                ArduinoManager.Instance.SendCommandToLight(command);
            }

            await SendPutRequestAsync(url, jsonBody, ct);
        }
        
        public async UniTask SetLightColorRGBAsync(int lightId, RGBColor rgb, int bri = -1, int transitionTime = 4, CancellationToken ct = default)
        {
            if (rgb == null || Config == null) return;
            Color color = new Color(rgb.r / 255f, rgb.g / 255f, rgb.b / 255f);
            Color.RGBToHSV(color, out float h, out float s, out float v);
            
            int hueValue = Mathf.RoundToInt(h * 65535f);
            int satValue = Mathf.RoundToInt(s * 254f);
            int finalBri = (bri == -1) ? Config.defaultBrightness : bri;
            await SetLightColorAsync(lightId, hueValue, satValue, finalBri, transitionTime, ct);
        }

        public async UniTask SetLightColorAsync(int lightId, int hue, int sat = -1, int bri = -1, int transitionTime = 4, CancellationToken ct = default)
        {
            if (Config == null || string.IsNullOrEmpty(Config.bridgeIp) || string.IsNullOrEmpty(Config.apiKey)) return;
            
            await EnsureLightIdsFetchedAsync(ct); 
            int actualId = GetPhysicalLightId(lightId); 

            if (actualId == -1)
            {
                Debug.LogWarning($"[HueManager] SetLightColor 취소됨: 논리 조명 {lightId}번에 해당하는 물리 조명을 찾을 수 없습니다.");
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
                        Debug.LogError($"[HueManager] 통신 실패: {request.error} | URL: {url}");
                    }
                }
                catch (OperationCanceledException)
                {
                    Debug.Log($"[HueManager] 휴 통신 취소됨");
                }
                catch (Exception e)
                {
                    if (!ct.IsCancellationRequested)
                    {
                        Debug.LogError($"[HueManager] 휴 통신 예외 발생: {e.Message}");
                    }
                }
            }
        }

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