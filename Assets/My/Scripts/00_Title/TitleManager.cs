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
    /// <summary>
    /// 타이틀 화면 진입 대기 및 씬 전환을 전담하는 매니저입니다.
    /// 서버 방 상태를 주기적으로 확인(Polling)하여 유저 입장이 감지되면 튜토리얼 씬으로 자동 전환합니다.
    /// </summary>
    public class TitleManager : MonoBehaviour
    {
        [Header("Polling Settings")]
        [SerializeField] private float basePollInterval = 1.0f; // 기본 API 폴링 간격
        [SerializeField] private float maxPollInterval = 10.0f; // 통신 실패 시 지수 백오프 최대 한도

        private float _currentPollInterval;
        private bool _isTransitioning; // 중복 씬 로드 방지 가드 플래그

        private Coroutine _soundCoroutine;
        private Coroutine _pollCoroutine;

        /// <summary> 씬 진입 시 잔여 데이터를 청소하고 초기 상태를 구성합니다. </summary>
        private void Start()
        {
            LoadSettings();

            if (_soundCoroutine == null)
            {
                _soundCoroutine = StartCoroutine(StartMainBGM());
            }

            // 타이틀 화면 진입 시 무조건 이전 세션을 비워, 이후 강제 종료 시 잘못된 유저 ID로 API가 전송되는 것을 원천 차단합니다.
            if (SessionManager.Instance)
            {
                SessionManager.Instance.ClearSession();
            }

            // 이전 플레이어의 촬영된 프레임 이미지 및 캐시 메모리 정리
            if (TimeLapseRecorder.Instance)
            {
                Debug.Log("[TitleManager] 소스 이미지 정리");
                TimeLapseRecorder.Instance.ClearRecordingData();
            }

            // 아두이노 하드웨어 리셋 및 LED 초기화 코루틴 가동
            StartCoroutine(TurnOffArduinoLedsRoutine());

            // 휴(Hue) 조명 즉시 소등 가동 (비동기 초기화 대기 포함)
            StartCoroutine(TurnOffHueLightsRoutine());

            _pollCoroutine = StartCoroutine(PollRoomStateRoutine());
        }

        /// <summary> 로컬 JSON에서 환경 설정 데이터를 로드합니다. </summary>
        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);

            if (settings == null)
            {
                Debug.LogWarning("[TitleManager] Settings.json 로드 실패.");
            }
        }

        /// <summary> 휴(Hue) 조명을 비동기로 소등합니다. 매니저 초기화를 기다리기 위해 타임아웃 처리가 포함됩니다. </summary>
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

        /// <summary> 아두이노를 강제 재부팅시킨 후 연결될 때까지 대기하고 모든 LED를 끄는 명령을 전송합니다. </summary>
        private IEnumerator TurnOffArduinoLedsRoutine()
        {
            float timeout = 60.0f;
            float timer = 0f;

            // ArduinoManager가 초기화될 때까지 대기
            while (!ArduinoManager.Instance && timer < timeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (!ArduinoManager.Instance) yield break;

            // 1. 타이틀 진입 시 무조건 아두이노 하드웨어 재부팅(DTR) 지시
            ArduinoManager.Instance.ReconnectAllAsync().Forget();

            // 2. 아두이노가 재부팅되고 정체성을 외쳐서 다시 묶일 때까지 대기 (약 3~5초 소요됨)
            timer = 0f;
            while (!ArduinoManager.Instance.AreBothConnected && timer < timeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            // 타임아웃 발생 시
            if (!ArduinoManager.Instance.AreBothConnected)
            {
                Debug.LogWarning("[TitleManager] 아두이노 재부팅 후 양쪽 연결 대기 시간 초과.");
                yield break;
            }

            // 3. 통신이 안정화될 수 있도록 잠시 대기
            yield return CoroutineData.GetWaitForSeconds(1.5f);

            // 아두이노 상태 동기화를 위해 한 번 더 전송
            bool allOff = ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOff);
            bool shotOff = ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedShotOff);

            if (!allOff || !shotOff)
            {
                Debug.LogWarning("[TitleManager] 아두이노 LED 초기화 명령 전송 실패.");
            }
            Debug.Log("[TitleManager] 아두이노 하드웨어 초기화(리셋) 및 상태 동기화 완료.");
            yield break;
        }

        /// <summary> 
        /// 방 상태를 지속적으로 조회하여 새 유저의 진입 여부를 감지합니다.
        /// </summary>
        private IEnumerator PollRoomStateRoutine()
        {
            _currentPollInterval = basePollInterval;

            while (!_isTransitioning)
            {
                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);

                    continue;
                }

                string requestUrl =
                    $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code={GameConstants.Module.Code.ToLower()}";

                using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
                {
                    webRequest.timeout = 10;

                    yield return webRequest.SendWebRequest();

                    // 네트워크 오류 시 즉각적인 재시도로 인한 과부하를 막기 위해 지수 백오프(Exponential Backoff) 적용
                    if (webRequest.result == UnityWebRequest.Result.ConnectionError ||
                        webRequest.result == UnityWebRequest.Result.ProtocolError)
                    {
                        _currentPollInterval = Mathf.Min(_currentPollInterval * 2f, maxPollInterval);
                        Debug.LogWarning(
                            $"[TitleManager] 상태 체크 통신 실패: {webRequest.error}. 백오프 적용: {_currentPollInterval}초 후 재시도");
                    }
                    else
                    {
                        // 통신 성공 시 폴링 주기를 기본값으로 즉시 복구
                        _currentPollInterval = basePollInterval;

                        string responseText = webRequest.downloadHandler.text;

                        // 서버에서 'USING' 응답 반환 시 유저 입장으로 간주하고 자동 진행
                        if (!string.IsNullOrEmpty(responseText) && responseText.IndexOf(GameConstants.Api.StatusUsing,
                                StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Debug.Log($"[TitleManager] RoomState 'USING' 감지. 튜토리얼로 이동.");
                            GoToTutorial();
                            yield break;
                        }
                    }
                }

                yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);
            }
        }

        /// <summary> 관리자 테스트 및 비상용 수동 전환 단축키 입력 대기 </summary>
        private void Update()
        {
            if (_isTransitioning) return;

            // 엔터 키 입력 시 통신 결과와 무관하게 즉시 튜토리얼로 강제 진입
            if (Input.GetKeyDown(KeyCode.Return))
            {
                GoToTutorial();
            }
        }

        /// <summary> 튜토리얼 씬으로 전환하며 중복 호출을 차단합니다. </summary>
        private void GoToTutorial()
        {
            if (_isTransitioning) return;

            _isTransitioning = true;

            SceneManager.LoadScene(GameConstants.Scene.Tutorial);
        }

        /// <summary> 
        /// 이전 씬(엔딩)의 잔여 사운드와 겹치거나 갑작스러운 소음으로 인한 불쾌감을 막기 위해 일정 시간 대기 후 BGM을 재생합니다. 
        /// </summary>
        private IEnumerator StartMainBGM()
        {
            if (!SoundManager.Instance) yield break;

            SoundManager.Instance.StopBGM();
            yield return CoroutineData.GetWaitForSeconds(1.0f);

            SoundManager.Instance.PlayBGM("MainBGM");
        }

        /// <summary> 오브젝트 파괴 시 백그라운드 코루틴을 정리하여 메모리 누수를 방지합니다. </summary>
        private void OnDestroy()
        {
            StopAllCoroutines();
            _soundCoroutine = null;
            _pollCoroutine = null;
        }
    }
}