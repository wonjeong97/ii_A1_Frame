using System;
using System.Text;
using System.Collections.Generic; 
using System.Threading;
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
            }
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
            string url = $"http://{Config.bridgeIp}/api/{Config.apiKey}/lights/{lightId}/state";
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
            int finalSat = (sat == -1) ? Config.defaultSaturation : sat;
            int finalBri = (bri == -1) ? Config.defaultBrightness : bri;

            string url = $"http://{Config.bridgeIp}/api/{Config.apiKey}/lights/{lightId}/state";
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
                    var op = request.SendWebRequest();
                    
                    // 토큰 취소 요청이 들어오면 즉시 통신을 강제 중단(Abort)합니다.
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
                    // 취소 요청 시 예외로 떨어지므로 로그를 남긴다.
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
    }
}