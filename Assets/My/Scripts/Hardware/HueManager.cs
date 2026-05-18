using System;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using Cysharp.Text; 
using Microsoft.Extensions.Logging; 
using ZLogger; 
using VContainer; 
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
        public HueConfig Config { get; private set; }

        private List<RGBColor> _shuffledColors;
        private int _colorIndex;

        private CancellationTokenSource _debugCts;

        private List<int> _physicalLightIds = new List<int>();
        private bool _isFetchingLights;
        private bool _hasFetchedLights;

        private readonly static Regex LightIdRegex = new Regex("\"(\\d+)\":\\s*\\{", RegexOptions.Compiled);
        private string _cachedBaseUrl;

        // --- 의존성 주입 (DI) 변수 ---
        private ILogger<HueManager> _logger;
        private SessionManager _sessionManager;
        private ArduinoManager _arduinoManager;

        [Inject]
        public void Construct(
            ILogger<HueManager> logger,
            SessionManager sessionManager,
            ArduinoManager arduinoManager)
        {
            _logger = logger;
            _sessionManager = sessionManager;
            _arduinoManager = arduinoManager;
        }

        private void Awake()
        {
            LoadConfig();
        }

        private void LoadConfig()
        {
            string currentLang = _sessionManager ? _sessionManager.CurrentLanguage : "ko";
            string huePath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.HueConfig, currentLang);
            Config = JsonLoader.Load<HueConfig>(huePath);

            if (Config == null)
            {
                _logger.ZLogError($"{huePath}.json 파일을 찾을 수 없음.");
                return;
            }

            _logger.ZLogInformation($"휴 설정 로드 완료 (IP: {Config.bridgeIp})");
            _cachedBaseUrl = ZString.Format("http://{0}/api/{1}/lights", Config.bridgeIp, Config.apiKey);
            
            EnsureLightIdsFetchedAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTask EnsureLightIdsFetchedAsync(CancellationToken ct = default)
        {
            if (HandleEditorOrCachedState()) return;

            if (_isFetchingLights)
            {
                await UniTask.WaitUntil(() => !_isFetchingLights, cancellationToken: ct);
                return;
            }

            _isFetchingLights = true;
            try
            {
                await ExecuteFetchSequence(ct);
            }
            finally
            {
                _isFetchingLights = false;
            }
        }

        private bool HandleEditorOrCachedState()
        {
#if UNITY_EDITOR
            if (!_hasFetchedLights)
            {
                _physicalLightIds = new List<int> { 1, 2, 3, 4, 5 };
                _hasFetchedLights = true;
            }

            return true;
#endif
            return _hasFetchedLights;
        }

        private async UniTask ExecuteFetchSequence(CancellationToken ct)
        {
            if (Config == null || string.IsNullOrEmpty(_cachedBaseUrl)) return;

            int maxRetries = 10;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                if (await TryFetchAndParseLights(_cachedBaseUrl, ct)) return;

                if (attempt < maxRetries - 1)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: ct);
                }
            }
        }

        private async UniTask<bool> TryFetchAndParseLights(string url, CancellationToken ct)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;

                try
                {
                    await request.SendWebRequest().ToUniTask(cancellationToken: ct);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
                catch (Exception)
                {
                    return false;
                }

                if (request.result != UnityWebRequest.Result.Success) return false;

                ParseLightIds(request.downloadHandler.text);
                if (_physicalLightIds.Count == 0)
                {
                    _logger.ZLogWarning($"[HueManager] 조명 ID를 찾지 못해 다시 시도합니다.");
                    return false;
                }

                _hasFetchedLights = true;
                return true;
            }
        }

        private void ParseLightIds(string json)
        {
            _physicalLightIds.Clear();
            MatchCollection matches = LightIdRegex.Matches(json);

            foreach (Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out int id))
                {
                    if (!_physicalLightIds.Contains(id)) _physicalLightIds.Add(id);
                }
            }

            _physicalLightIds.Sort();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                ApplyDebugState(false, "Off (M)");
                return;
            }

            HandleDebugColorKeys();
        }

        private void HandleDebugColorKeys()
        {
            if (!Input.anyKeyDown) return;

            if (Input.GetKeyDown(KeyCode.Z)) TryApplyDebugColor(Config?.whiteColor, "White (Z)");
            else if (Input.GetKeyDown(KeyCode.X)) TryApplyDebugColor(Config?.color1, "Color 1 (X)");
            else if (Input.GetKeyDown(KeyCode.C)) TryApplyDebugColor(Config?.color2, "Color 2 (C)");
            else if (Input.GetKeyDown(KeyCode.V)) TryApplyDebugColor(Config?.color3, "Color 3 (V)");
            else if (Input.GetKeyDown(KeyCode.B)) TryApplyDebugColor(Config?.color4, "Color 4 (B)");
            else if (Input.GetKeyDown(KeyCode.N)) TryApplyDebugColor(Config?.color5, "Color 5 (N)");
        }

        private void TryApplyDebugColor(RGBColor color, string logName)
        {
            if (color == null) return;

            ApplyDebugColor(color, logName);
        }

        private void ApplyDebugColor(RGBColor color, string logName)
        {
            _debugCts?.Cancel();
            _debugCts?.Dispose();
            _debugCts = new CancellationTokenSource();

            SetLightColorRGBAsync(1, color, -1, 4, _debugCts.Token).Forget();
            SetLightColorRGBAsync(2, color, -1, 4, _debugCts.Token).Forget();
            _logger.ZLogInformation($"디버그 조명 변경: {logName}");
        }

        private void ApplyDebugState(bool isOn, string logName)
        {
            _debugCts?.Cancel();
            _debugCts?.Dispose();
            _debugCts = new CancellationTokenSource();

            SetLightStateAsync(1, isOn, _debugCts.Token).Forget();
            SetLightStateAsync(2, isOn, _debugCts.Token).Forget();
            _logger.ZLogInformation($"디버그 조명 상태 변경: {logName}");
        }

        public void InitRandomColors()
        {
            if (Config == null) return;

            _shuffledColors = new List<RGBColor>
            {
                Config.color1, Config.color2, Config.color3, Config.color4, Config.color5
            };

            for (int i = 0; i < _shuffledColors.Count; i++)
            {
                int rnd = UnityEngine.Random.Range(i, _shuffledColors.Count);
                RGBColor temp = _shuffledColors[i];
                _shuffledColors[i] = _shuffledColors[rnd];
                _shuffledColors[rnd] = temp;
            }

            _colorIndex = 0;
        }

        public RGBColor PopRandomColor()
        {
            if (_shuffledColors == null || _colorIndex >= _shuffledColors.Count) InitRandomColors();
            if (_shuffledColors == null) return null;

            RGBColor selectedColor = _shuffledColors[_colorIndex];
            _colorIndex++;
            return selectedColor;
        }

        public async UniTask SetLightStateAsync(int lightId, bool isOn, CancellationToken ct = default)
        {
            if (Config == null || string.IsNullOrEmpty(_cachedBaseUrl)) return;

            await EnsureLightIdsFetchedAsync(ct);
            int actualId = GetPhysicalLightId(lightId);
            if (actualId == -1) return;
            
            string url = ZString.Format("{0}/{1}/state", _cachedBaseUrl, actualId);
            string jsonBody = ZString.Format("{{\"on\":{0}}}", isOn ? "true" : "false");
            
            if (_arduinoManager)
            {
                string command = isOn ? GameConstants.Hardware.CmdLightOn : GameConstants.Hardware.CmdLightOff;
                _arduinoManager.SendCommandToLight(command);
            }

            await SendPutRequestAsync(url, jsonBody, ct);
        }

        public async UniTask SetLightColorRGBAsync(int lightId, RGBColor rgb, int bri = -1, int transitionTime = 4,
            CancellationToken ct = default)
        {
            if (rgb == null || Config == null) return;

            Color color = new Color(rgb.r / 255f, rgb.g / 255f, rgb.b / 255f);
            Color.RGBToHSV(color, out float h, out float s, out float v);

            int hueValue = Mathf.RoundToInt(h * 65535f);
            int satValue = Mathf.RoundToInt(s * 254f);
            int finalBri = (bri == -1) ? Config.defaultBrightness : bri;

            await SetLightColorAsync(lightId, hueValue, satValue, finalBri, transitionTime, ct);
        }

        public async UniTask SetLightColorAsync(int lightId, int hue, int sat = -1, int bri = -1,
            int transitionTime = 4, CancellationToken ct = default)
        {
            if (Config == null || string.IsNullOrEmpty(_cachedBaseUrl)) return;

            await EnsureLightIdsFetchedAsync(ct);
            int actualId = GetPhysicalLightId(lightId);
            if (actualId == -1) return;

            int finalSat = (sat == -1) ? Config.defaultSaturation : sat;
            int finalBri = (bri == -1) ? Config.defaultBrightness : bri;
            
            string url = ZString.Format("{0}/{1}/state", _cachedBaseUrl, actualId);
            string jsonBody = ZString.Format(
                "{{\"on\":true, \"bri\":{0}, \"hue\":{1}, \"sat\":{2}, \"transitiontime\":{3}}}", 
                finalBri, hue, finalSat, transitionTime);

            if (_arduinoManager)
            {
                _arduinoManager.SendCommandToLight(GameConstants.Hardware.CmdLightOn);
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
                    await request.SendWebRequest().ToUniTask(cancellationToken: ct);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    // [품질 보완] e 자체를 전달하여 내부 가비지 없이 원본 오류 유실 없는 스택 트레이스 디버깅 제공
                    if (!ct.IsCancellationRequested) _logger.ZLogError(e, $"통신 예외: {e.Message}");
                }
            }
        }

        private int GetPhysicalLightId(int logicalId)
        {
            int index = logicalId - 1;
            if (index >= 0 && index < _physicalLightIds.Count) return _physicalLightIds[index];

            return -1;
        }

        private void OnDestroy()
        {
            _debugCts?.Cancel();
            _debugCts?.Dispose();
        }
    }
}