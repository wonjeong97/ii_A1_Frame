using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using My.Scripts.Global;
using My.Scripts.Hardware;
using My.Scripts.Timelapse;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._00_Title
{
    public class TitleManager : MonoBehaviour
    {
        [Header("Polling Settings")]
        [SerializeField] private float pollInterval = 3.0f; 

        private bool _isTransitioning; 

        private Coroutine _soundCoroutine;
        private Coroutine _pollCoroutine;

        private void Start()
        {
            LoadSettings();

            if (_soundCoroutine == null)
            {
                _soundCoroutine = StartCoroutine(StartMainBGM());
            }

            if (SessionManager.Instance)
            {
                SessionManager.Instance.ClearSession();
            }

            if (TimeLapseRecorder.Instance)
            {
                Debug.Log("[TitleManager] 소스 이미지 정리");
                TimeLapseRecorder.Instance.ClearRecordingData();
            }

            StartCoroutine(TurnOffArduinoLedsRoutine());
            StartCoroutine(TurnOffHueLightsRoutine());

            _pollCoroutine = StartCoroutine(PollRoomStateRoutine());
        }

        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);

            if (settings == null)
            {
                Debug.LogWarning("[TitleManager] Settings.json 로드 실패.");
            }
        }

        private IEnumerator TurnOffHueLightsRoutine()
        {
            float timeout = 5.0f;
            float timer = 0f;

            while (!HueManager.Instance && timer < timeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (HueManager.Instance)
            {
                HueManager.Instance.SetLightStateAsync(1, false).Forget();
                HueManager.Instance.SetLightStateAsync(2, false).Forget();
                Debug.Log("[TitleManager] 휴(Hue) 조명 소등 완료.");
            }
            else
            {
                Debug.LogWarning("[TitleManager] 휴(Hue) 매니저를 찾지 못해 조명 소등 실패.");
            }
        }

        private IEnumerator TurnOffArduinoLedsRoutine()
        {
            float timeout = 60.0f;
            float timer = 0f;

            while (!ArduinoManager.Instance && timer < timeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (!ArduinoManager.Instance) yield break;

            ArduinoManager.Instance.ReconnectAllAsync().Forget();

            timer = 0f;
            while (!ArduinoManager.Instance.AreAllConnected && timer < timeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (!ArduinoManager.Instance.AreAllConnected)
            {
                Debug.LogWarning("[TitleManager] 아두이노 재부팅 후 전체 연결 대기 시간 초과.");
                yield break;
            }

            yield return CoroutineData.GetWaitForSeconds(1.5f);

            bool allOff = ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOff);
            bool shotOff = ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedShotOff);
            bool lightOff = ArduinoManager.Instance.SendCommandToLight(GameConstants.Hardware.CmdLightOff);

            if (!allOff || !shotOff || !lightOff)
            {
                Debug.LogWarning("[TitleManager] 아두이노 초기화 명령 전송 실패.");
            }
            Debug.Log("[TitleManager] 아두이노 하드웨어 초기화(리셋) 및 상태 동기화 완료.");
            yield break;
        }

        private IEnumerator PollRoomStateRoutine()
        {
// 에디터에서는 서버에 계속 핑을 날려 유저를 가로채지 않습니다.
#if UNITY_EDITOR
            Debug.Log("<color=orange>[TitleManager] 에디터 모드 방지: 실제 전시관 유저 가로채기(Room 폴링)를 차단했습니다. Enter 키를 눌러 수동으로 게임에 진입하세요.</color>");
            yield break;
#endif

            while (!_isTransitioning)
            {
                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    yield return CoroutineData.GetWaitForSeconds(pollInterval);
                    continue;
                }

                string requestUrl =
                    $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code={GameConstants.Module.Code.ToLower()}";

                using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
                {
                    webRequest.timeout = 10;

                    yield return webRequest.SendWebRequest();

                    if (webRequest.result == UnityWebRequest.Result.ConnectionError ||
                        webRequest.result == UnityWebRequest.Result.ProtocolError)
                    {
                        Debug.LogWarning($"[TitleManager] 상태 체크 통신 실패: {webRequest.error}. {pollInterval}초 후 재시도");
                    }
                    else
                    {
                        string responseText = webRequest.downloadHandler.text;

                        if (!string.IsNullOrEmpty(responseText) && responseText.IndexOf(GameConstants.Api.StatusUsing,
                                StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Debug.Log($"[TitleManager] RoomState 'USING' 감지. 튜토리얼로 이동.");
                            GoToTutorial();
                            yield break;
                        }
                    }
                }

                yield return CoroutineData.GetWaitForSeconds(pollInterval);
            }
        }

        private void Update()
        {
            if (_isTransitioning) return;

            // 디버그 용으로 Enter 키를 누르면 바로 튜토리얼로 들어갑니다.
            if (Input.GetKeyDown(KeyCode.Return))
            {
                GoToTutorial();
            }
        }

        private void GoToTutorial()
        {
            if (_isTransitioning) return;

            _isTransitioning = true;

            SceneManager.LoadScene(GameConstants.Scene.Tutorial);
        }

        private IEnumerator StartMainBGM()
        {
            if (!SoundManager.Instance) yield break;

            SoundManager.Instance.StopBGM();
            yield return CoroutineData.GetWaitForSeconds(1.0f);

            SoundManager.Instance.PlayBGM("MainBGM");
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            _soundCoroutine = null;
            _pollCoroutine = null;
        }
    }
}