using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using My.Scripts.Global;
using My.Scripts.Hardware;
using My.Scripts.Timelapse;
using UnityEngine;
using UnityEngine.Networking;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._00_Title
{
    /// <summary>
    /// 타이틀 화면의 하드웨어 초기화 및 서버 상태 폴링을 관리함.
    /// </summary>
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

            // 이전 사용자의 데이터가 남아있지 않도록 보장함.
            if (SessionManager.Instance)
            {
                SessionManager.Instance.ClearSession();
            }

            // 저장 공간 확보 및 보안을 위해 로컬 임시 이미지를 제거함.
            if (TimeLapseRecorder.Instance)
            {
                TimeLapseRecorder.Instance.ClearRecordingData();
            }

            StartCoroutine(TurnOffArduinoLedsRoutine());
            StartCoroutine(TurnOffHueLightsRoutine());

            _pollCoroutine = StartCoroutine(PollRoomStateRoutine());
        }

        /// <summary>
        /// 외부 설정 파일을 로드하여 게임 환경을 구성함.
        /// </summary>
        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);

            if (settings == null)
            {
                Debug.LogWarning("환경 설정 파일 누락으로 기본값 사용 가능성 있음.");
            }
        }

        /// <summary>
        /// 전시 시작 전 조명을 초기 상태(Off)로 전환함.
        /// </summary>
        private IEnumerator TurnOffHueLightsRoutine()
        {
            float timeout = 5.0f;
            float timer = 0f;

            // 싱글톤 인스턴스가 생성될 때까지 대기하여 참조 에러를 방지함.
            while (!HueManager.Instance && timer < timeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (HueManager.Instance)
            {
                HueManager.Instance.SetLightStateAsync(1, false).Forget();
                HueManager.Instance.SetLightStateAsync(2, false).Forget();
            }
            else
            {
                Debug.LogWarning("조명 컨트롤러 연결 불가.");
            }
        }

        /// <summary>
        /// 아두이노 하드웨어 재연결 및 LED 상태를 초기화함.
        /// </summary>
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

            // 시리얼 포트 안정성을 위해 기존 연결을 모두 초기화하고 재시작함.
            ArduinoManager.Instance.ReconnectAllAsync().Forget();

            timer = 0f;
            while (!ArduinoManager.Instance.AreAllConnected && timer < timeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (!ArduinoManager.Instance.AreAllConnected)
            {
                Debug.LogWarning("하드웨어 통신 복구 실패.");
                yield break;
            }

            // 명령 간 경합 방지를 위해 물리적인 장치 응답 대기 시간을 가짐.
            yield return CoroutineData.GetWaitForSeconds(1.5f);

            bool allOff = ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOff);
            bool shotOff = ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedShotOff);
            bool lightOff = ArduinoManager.Instance.SendCommandToLight(GameConstants.Hardware.CmdLightOff);

            if (!allOff || !shotOff || !lightOff)
            {
                Debug.LogWarning("일부 하드웨어 명령 전송 누락.");
            }
            yield break;
        }

        /// <summary>
        /// 서버를 지속적으로 확인하여 전시 사용 가능 여부를 판별함.
        /// </summary>
        private IEnumerator PollRoomStateRoutine()
        {
#if UNITY_EDITOR
            // 개발 중 서버 큐를 가로채어 실제 전시가 중단되는 현상을 방지함.
            yield break;
#endif

            while (!_isTransitioning)
            {
                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    yield return CoroutineData.GetWaitForSeconds(pollInterval);
                    continue;
                }

                string requestUrl = $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code={GameConstants.Module.Code.ToLower()}";

                using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
                {
                    webRequest.timeout = 10;
                    yield return webRequest.SendWebRequest();

                    if (webRequest.result == UnityWebRequest.Result.ConnectionError ||
                        webRequest.result == UnityWebRequest.Result.ProtocolError)
                    {
                        Debug.LogWarning("네트워크 불안정으로 상태 체크 실패.");
                    }
                    else
                    {
                        string responseText = webRequest.downloadHandler.text;

                        // 서버로부터 '사용 중' 신호를 받으면 즉시 체험 단계로 진입함.
                        if (!string.IsNullOrEmpty(responseText) && responseText.IndexOf(GameConstants.Api.StatusUsing,
                                StringComparison.OrdinalIgnoreCase) >= 0)
                        {
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

            // 장치 장애 상황 등을 대비한 수동 강제 진입 루트를 제공함.
            if (Input.GetKeyDown(KeyCode.Return))
            {
                GoToTutorial();
            }
        }

        /// <summary>
        /// 씬 전환 시 중복 호출을 방지하며 튜토리얼 씬으로 이동함.
        /// </summary>
        private void GoToTutorial()
        {
            if (_isTransitioning) return;

            _isTransitioning = true;
            SceneLoader.LoadAsync(GameConstants.Scene.Tutorial).Forget();
        }

        /// <summary>
        /// 사운드 매니저 초기화 및 배경음을 재생함.
        /// </summary>
        private IEnumerator StartMainBGM()
        {
            if (!SoundManager.Instance) yield break;

            // 오디오 소스 정리를 위해 이전 음원을 중단하고 재생함.
            SoundManager.Instance.StopBGM();
            yield return CoroutineData.GetWaitForSeconds(1.0f);

            SoundManager.Instance.PlayBGM("MainBGM");
        }

        private void OnDestroy()
        {
            // 메모리 누수 방지 및 씬 전환 후 비정상적인 로직 실행을 차단함.
            StopAllCoroutines();
            _soundCoroutine = null;
            _pollCoroutine = null;
        }
    }
}