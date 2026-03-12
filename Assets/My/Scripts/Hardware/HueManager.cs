using System;
using System.Text;
using System.Collections.Generic; // List 사용을 위해 추가
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

        // --- 랜덤 색상(Q6~Q10) 관리를 위한 변수 ---
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
                Debug.LogWarning("[HueManager] JSON/HueConfig.json 파일을 찾을 수 없어 기본값을 사용합니다.");
                Config = new HueConfig
                {
                    bridgeIp = "192.168.0.127",
                    apiKey = "MGt9ykYRMUMg4Lbffsw-YkqrxIiWcd9H-YN0AtxP",
                    defaultBrightness = 254,
                    defaultSaturation = 254,
                    color1 = new RGBColor { r = 133, g = 189, b = 52 },
                    color2 = new RGBColor { r = 254, g = 143, b = 0 },
                    color3 = new RGBColor { r = 133, g = 67, b = 190 },
                    color4 = new RGBColor { r = 2, g = 144, b = 202 },
                    color5 = new RGBColor { r = 237, g = 83, b = 204 }
                };
            }
            else
            {
                Debug.Log($"[HueManager] 휴 설정 로드 완료 (IP: {Config.bridgeIp})");
            }
        }

        /// <summary> 5가지 색상을 중복 없이 랜덤하게 섞습니다. </summary>
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

            // Fisher-Yates 셔플 알고리즘으로 리스트 섞기
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

        /// <summary> 섞인 색상 리스트에서 하나씩 순차적으로 뽑아옵니다. </summary>
        public RGBColor PopRandomColor()
        {
            // 혹시라도 초기화되지 않았거나 인덱스를 초과했다면 다시 셔플하여 안전성 확보
            if (_shuffledColors == null || _shuffledColors.Count == 0 || _colorIndex >= _shuffledColors.Count)
            {
                InitRandomColors();
            }
            
            RGBColor selectedColor = _shuffledColors[_colorIndex];
            _colorIndex++;
            return selectedColor;
        }

        public async UniTask SetLightStateAsync(int lightId, bool isOn)
        {
            if (Config == null || string.IsNullOrEmpty(Config.bridgeIp)) return;
            string url = $"http://{Config.bridgeIp}/api/{Config.apiKey}/lights/{lightId}/state";
            string jsonBody = "{\"on\":" + (isOn ? "true" : "false") + "}";
            await SendPutRequestAsync(url, jsonBody);
        }

        public async UniTask SetLightColorRGBAsync(int lightId, RGBColor rgb, int bri = -1, int transitionTime = 4)
        {
            if (rgb == null) return;
            Color color = new Color(rgb.r / 255f, rgb.g / 255f, rgb.b / 255f);
            Color.RGBToHSV(color, out float h, out float s, out float v);
            
            int hueValue = Mathf.RoundToInt(h * 65535f);
            int satValue = Mathf.RoundToInt(s * 254f);
            int finalBri = (bri == -1) ? Config.defaultBrightness : bri;

            await SetLightColorAsync(lightId, hueValue, satValue, finalBri, transitionTime);
        }

        public async UniTask SetLightColorAsync(int lightId, int hue, int sat = -1, int bri = -1, int transitionTime = 4)
        {
            if (Config == null || string.IsNullOrEmpty(Config.bridgeIp)) return;
            int finalSat = (sat == -1) ? Config.defaultSaturation : sat;
            int finalBri = (bri == -1) ? Config.defaultBrightness : bri;

            string url = $"http://{Config.bridgeIp}/api/{Config.apiKey}/lights/{lightId}/state";
            string jsonBody = $"{{\"on\":true, \"bri\":{finalBri}, \"hue\":{hue}, \"sat\":{finalSat}, \"transitiontime\":{transitionTime}}}";
            await SendPutRequestAsync(url, jsonBody);
        }

        private async UniTask SendPutRequestAsync(string url, string jsonBody)
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
                    await request.SendWebRequest().ToUniTask();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[HueManager] 통신 실패: {request.error} | URL: {url}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[HueManager] 휴 통신 예외 발생: {e.Message}");
                }
            }
        }
    }
}