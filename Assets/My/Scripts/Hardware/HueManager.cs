using System;
using System.Text;
using System.Collections.Generic; 
using System.Threading;
using System.Text.RegularExpressions; // 정규식(Regex) 처리를 위해 추가
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
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

        // ▼▼▼ 동적 ID 매핑용 변수 추가 ▼▼▼
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
                // 설정 로드 후 즉시 비동기로 연결된 조명 번호들을 수집합니다.
                EnsureLightIdsFetchedAsync().Forget();
            }
        }

        // ▼▼▼ 휴 브릿지에 연결된 실제 조명 ID를 동적으로 수집하는 함수 ▼▼▼
        private async UniTask EnsureLightIdsFetchedAsync()
        {
            if (_hasFetchedLights) return; // 이미 수집했다면 패스
            
            // 다른 곳에서 이미 수집 중이라면 끝날 때까지 대기
            if (_isFetchingLights) 
            {
                await UniTask.WaitUntil(() => !_isFetchingLights);
                return;
            }

            _isFetchingLights = true;

            if (Config == null || string.IsNullOrEmpty(Config.bridgeIp) || string.IsNullOrEmpty(Config.apiKey))
            {
                _isFetchingLights = false;
                return;
            }

            string url = $"http://{Config.bridgeIp}/api/{Config.apiKey}/lights";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 5;
                try
                {
                    await request.SendWebRequest().ToUniTask();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string json = request.downloadHandler.text;
                        _physicalLightIds.Clear();

                        // 정규식을 이용해 JSON에서 "3": { ... }, "4": { ... } 형태의 최상위 숫자 ID를 추출합니다.
                        MatchCollection matches = Regex.Matches(json, "\"(\\d+)\":\\s*\\{");
                        foreach (Match match in matches)
                        {
                            if (int.TryParse(match.Groups[1].Value, out int id))
                            {
                                if (!_physicalLightIds.Contains(id)) _physicalLightIds.Add(id);
                            }
                        }
                        
                        _physicalLightIds.Sort(); // 낮은 번호부터 정렬 (예: 3, 4)
                        Debug.Log($"<color=cyan>[HueManager] 동적 조명 ID 자동 매핑 완료: {string.Join(", ", _physicalLightIds)}</color>");
                        _hasFetchedLights = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[HueManager] 조명 목록 조회 실패: {request.error}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[HueManager] 조명 목록 조회 중 예외: {e.Message}");
                }
            }

            _isFetchingLights = false;
        }
        
        private int GetPhysicalLightId(int logicalId)
        {
            // 아직 연결된 조명이 확인되지 않았다면 기존 번호 그대로 반환 (안전장치)
            if (_physicalLightIds == null || _physicalLightIds.Count == 0) return logicalId; 
            
            // logicalId 1번은 리스트의 [0]번째, 2번은 [1]번째를 가져옴
            int index = logicalId - 1; 
            if (index >= 0 && index < _physicalLightIds.Count) 
            {
                return _physicalLightIds[index];
            }
            
            return logicalId; // 범위를 초과하면 그대로 반환
        }

        private void Update()
        {
            // Z키: White
            if (Input.GetKeyDown(KeyCode.Z))
            {
                RGBColor white = (Config != null && Config.whiteColor != null) 
                    ? Config.whiteColor 
                    : new RGBColor { r = 191, g = 239, b = 251 };
                
                ApplyDebugColor(white, "White (Z)");
            }
            // X키: Color 1
            else if (Input.GetKeyDown(KeyCode.X))
            {
                if (Config != null) ApplyDebugColor(Config.color1, "Color 1 (X)");
            }
            // C키: Color 2
            else if (Input.GetKeyDown(KeyCode.C))
            {
                if (Config != null) ApplyDebugColor(Config.color2, "Color 2 (C)");
            }
            // V키: Color 3
            else if (Input.GetKeyDown(KeyCode.V))
            {
                if (Config != null) ApplyDebugColor(Config.color3, "Color 3 (V)");
            }
            // B키: Color 4
            else if (Input.GetKeyDown(KeyCode.B))
            {
                if (Config != null) ApplyDebugColor(Config.color4, "Color 4 (B)");
            }
            // N키: Color 5
            else if (Input.GetKeyDown(KeyCode.N))
            {
                if (Config != null) ApplyDebugColor(Config.color5, "Color 5 (N)");
            }
            // M키: 조명 Off
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
            
            await EnsureLightIdsFetchedAsync(); // 물리 번호 확인 보장
            int actualId = GetPhysicalLightId(lightId); // 1 -> 3 등으로 변환

            string url = $"http://{Config.bridgeIp}/api/{Config.apiKey}/lights/{actualId}/state";
            string jsonBody = "{\"on\":" + (isOn ? "true" : "false") + "}";
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
            
            await EnsureLightIdsFetchedAsync(); // 물리 번호 확인 보장
            int actualId = GetPhysicalLightId(lightId); // 1 -> 3 등으로 변환

            int finalSat = (sat == -1) ? Config.defaultSaturation : sat;
            int finalBri = (bri == -1) ? Config.defaultBrightness : bri;

            string url = $"http://{Config.bridgeIp}/api/{Config.apiKey}/lights/{actualId}/state";
            string jsonBody = $"{{\"on\":true, \"bri\":{finalBri}, \"hue\":{hue}, \"sat\":{finalSat}, \"transitiontime\":{transitionTime}}}";
            await SendPutRequestAsync(url, jsonBody, ct);
        }
        
        private async UniTask SendPutRequestAsync(string url, string jsonBody, CancellationToken ct = default)
        {
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