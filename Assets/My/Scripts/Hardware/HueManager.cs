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
        private int _colorIndex;

        private CancellationTokenSource _debugCts;

        private List<int> _physicalLightIds = new List<int>();
        private bool _isFetchingLights;
        private bool _hasFetchedLights;

        // 정규식 컴파일 오버헤드 방지를 위한 정적 캐싱
        private static readonly Regex _lightIdRegex = new Regex("\"(\\d+)\":\\s*\\{", RegexOptions.Compiled);
        private string _cachedBaseUrl;

        /// <summary>
        /// 싱글톤 인스턴스를 초기화하고 환경설정 로드를 트리거함.
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
        /// Hue 설정 데이터를 JSON에서 로드하고 조명 ID 캐싱을 시작함.
        /// </summary>
        private void LoadConfig()
        {
            string huePath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.HueConfig);
            Config = JsonLoader.Load<HueConfig>(huePath);

            if (Config == null)
            {
                Debug.LogError($"{huePath}.json 파일을 찾을 수 없음.");
                return;
            }

            Debug.Log($"휴 설정 로드 완료 (IP: {Config.bridgeIp})");
            _cachedBaseUrl = $"http://{Config.bridgeIp}/api/{Config.apiKey}/lights";
            EnsureLightIdsFetchedAsync().Forget();
        }

        /// <summary>
        /// 휴 브릿지의 API를 호출하여 조명 장치 ID를 비동기로 캐싱함.
        /// </summary>
        /// <param name="ct">비동기 작업 취소 토큰</param>
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

        /// <summary>
        /// 에디터 환경이거나 이미 데이터를 가져온 경우의 예외 처리를 수행함.
        /// </summary>
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

        /// <summary>
        /// 실제 조명 목록을 요청하고 재시도 로직을 관리함.
        /// </summary>
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

        /// <summary>
        /// 웹 요청을 통해 수신된 JSON 데이터에서 ID를 추출함.
        /// </summary>
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
                _hasFetchedLights = true;
                return true;
            }
        }

        /// <summary>
        /// 정규식을 사용하여 JSON 텍스트에서 숫자 ID를 파싱함.
        /// </summary>
        private void ParseLightIds(string json)
        {
            _physicalLightIds.Clear();
            MatchCollection matches = _lightIdRegex.Matches(json);

            foreach (Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out int id))
                {
                    if (!_physicalLightIds.Contains(id)) _physicalLightIds.Add(id);
                }
            }

            _physicalLightIds.Sort();
        }

        /// <summary>
        /// 디버그용 단축키 입력을 처리함.
        /// </summary>
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                ApplyDebugState(false, "Off (M)");
                return;
            }

            HandleDebugColorKeys();
        }

        /// <summary>
        /// 지정된 키에 따라 조명 색상을 즉시 변경함.
        /// </summary>
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

        /// <summary>
        /// 데이터 유효성 검사 후 디버그 색상을 적용함.
        /// </summary>
        private void TryApplyDebugColor(RGBColor color, string logName)
        {
            if (color == null) return;

            ApplyDebugColor(color, logName);
        }

        /// <summary>
        /// 이전 작업을 취소하고 조명 색상 변경 태스크를 가동함.
        /// </summary>
        private void ApplyDebugColor(RGBColor color, string logName)
        {
            _debugCts?.Cancel();
            _debugCts?.Dispose();
            _debugCts = new CancellationTokenSource();

            SetLightColorRGBAsync(1, color, -1, 4, _debugCts.Token).Forget();
            SetLightColorRGBAsync(2, color, -1, 4, _debugCts.Token).Forget();
            Debug.Log($"디버그 조명 변경: {logName}");
        }

        /// <summary>
        /// 조명의 전원 상태를 디버그 명령으로 변경함.
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
        /// 조명 연출용 셔플 리스트를 초기화함.
        /// </summary>
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

        /// <summary>
        /// 셔플된 목록에서 다음 색상을 반환함.
        /// </summary>
        public RGBColor PopRandomColor()
        {
            if (_shuffledColors == null || _colorIndex >= _shuffledColors.Count) InitRandomColors();
            if (_shuffledColors == null) return null;

            RGBColor selectedColor = _shuffledColors[_colorIndex];
            _colorIndex++;
            return selectedColor;
        }

        /// <summary>
        /// 조명의 전원 상태를 변경하는 API 명령을 전송함.
        /// </summary>
        public async UniTask SetLightStateAsync(int lightId, bool isOn, CancellationToken ct = default)
        {
            if (Config == null || string.IsNullOrEmpty(_cachedBaseUrl)) return;

            await EnsureLightIdsFetchedAsync(ct);
            int actualId = GetPhysicalLightId(lightId);
            if (actualId == -1) return;

            string url = $"{_cachedBaseUrl}/{actualId}/state";
            string jsonBody = "{\"on\":" + (isOn ? "true" : "false") + "}";

            if (ArduinoManager.Instance)
            {
                string command = isOn ? GameConstants.Hardware.CmdLightOn : GameConstants.Hardware.CmdLightOff;
                ArduinoManager.Instance.SendCommandToLight(command);
            }

            await SendPutRequestAsync(url, jsonBody, ct);
        }

        /// <summary>
        /// RGB 색상을 HSV 모델로 변환하여 조명 명령을 전송함.
        /// </summary>
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

        /// <summary>
        /// 원시 색상 데이터를 사용하여 브릿지에 조명 변경을 요청함.
        /// </summary>
        public async UniTask SetLightColorAsync(int lightId, int hue, int sat = -1, int bri = -1,
            int transitionTime = 4, CancellationToken ct = default)
        {
            if (Config == null || string.IsNullOrEmpty(_cachedBaseUrl)) return;

            await EnsureLightIdsFetchedAsync(ct);
            int actualId = GetPhysicalLightId(lightId);
            if (actualId == -1) return;

            int finalSat = (sat == -1) ? Config.defaultSaturation : sat;
            int finalBri = (bri == -1) ? Config.defaultBrightness : bri;

            string url = $"{_cachedBaseUrl}/{actualId}/state";
            string jsonBody =
                $"{{\"on\":true, \"bri\":{finalBri}, \"hue\":{hue}, \"sat\":{finalSat}, \"transitiontime\":{transitionTime}}}";

            if (ArduinoManager.Instance) ArduinoManager.Instance.SendCommandToLight(GameConstants.Hardware.CmdLightOn);

            await SendPutRequestAsync(url, jsonBody, ct);
        }

        /// <summary>
        /// HTTP PUT 메서드로 명령을 송신함.
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
                    await request.SendWebRequest().ToUniTask(cancellationToken: ct);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    if (!ct.IsCancellationRequested) Debug.LogError($"통신 예외: {e.Message}");
                }
            }
        }

        /// <summary>
        /// 논리 ID를 실제 물리 ID로 변환함.
        /// </summary>
        private int GetPhysicalLightId(int logicalId)
        {
            int index = logicalId - 1;
            if (index >= 0 && index < _physicalLightIds.Count) return _physicalLightIds[index];

            return -1;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            _debugCts?.Cancel();
            _debugCts?.Dispose();
        }
    }
}