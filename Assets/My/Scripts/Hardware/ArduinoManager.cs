using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using My.Scripts.Global;
using Wonjeong.Utils;

namespace My.Scripts.Hardware
{
    /// <summary>
    /// PC에 연결된 가용 COM 포트를 스캔하여 좌/우 아두이노 장치를 자동 식별하고, 시리얼 통신(제어 명령 송수신)을 전담하는 싱글톤 매니저입니다.
    /// 메인 스레드 프레임 드랍을 막기 위해 100% 비동기 및 멀티 스레딩 기반으로 구동됩니다.
    /// </summary>
    public class ArduinoManager : MonoBehaviour
    {
        public static ArduinoManager Instance;

        /// <summary> 하드웨어 입력 발생 시 구독 중인 각 페이지(PopupGamePage 등)로 신호를 전달하는 델리게이트 이벤트 </summary>
        public Action<string, bool> OnHardwareInput;

        private SerialPort _leftPort;
        private SerialPort _rightPort;

        // 백그라운드 스레드에서 수신된 데이터를 메인 스레드로 안전하게 넘기기 위한 스레드-세이프 큐
        private ConcurrentQueue<(string input, bool isLeft)> _inputQueue = new ConcurrentQueue<(string input, bool isLeft)>();
        private Thread _readThread;
        private bool _isRunning = false;

        public bool IsLeftConnected => _leftPort != null && _leftPort.IsOpen;
        public bool IsRightConnected => _rightPort != null && _rightPort.IsOpen;

        /// <summary> 씬 전환 시에도 시리얼 통신 연결이 끊어지지 않도록 영속성(DontDestroyOnLoad) 보장 </summary>
        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            _isRunning = true;
            AutoConnectAsync().Forget();
        }

        /// <summary> 
        /// 백그라운드 스레드에서 큐에 쌓아둔 하드웨어 입력 데이터를 메인 스레드 프레임에 맞춰 안전하게 방출합니다.
        /// </summary>
        private void Update()
        {
            // var 금지 규칙에 따라 명시적 튜플 타입 선언
            while (_inputQueue.TryDequeue(out (string input, bool isLeft) result))
            {
                ProcessHardwareInput(result.input, result.isLeft);
            }
        }

        /// <summary> 수신된 문자열 로그를 출력하고, 이벤트를 발생시켜 게임 로직으로 전달합니다. </summary>
        private void ProcessHardwareInput(string input, bool isLeft)
        {
            string sideName = isLeft ? "Left" : "Right";
            Debug.Log($"[{sideName} 아두이노]>> {input}");

            if (OnHardwareInput != null)
            {
                OnHardwareInput.Invoke(input, isLeft);
            }
        }

        /// <summary> 
        /// 전체 COM 포트를 순회하며 임시 개방 후 식별 문자열을 수신 대기합니다.
        /// 포트 개방 시 발생하는 메인 스레드 블로킹을 막기 위해 UniTask로 비동기화되었습니다.
        /// </summary>
        private async UniTaskVoid AutoConnectAsync()
        {
            string[] portNames = SerialPort.GetPortNames();
            Debug.Log($"[ArduinoManager] 발견된 전체 COM 포트 수: {portNames.Length}");

            foreach (string portName in portNames)
            {
                if (IsLeftConnected && IsRightConnected) break;
                
                // 각 포트의 연결 시도 및 대기를 백그라운드 스레드 풀에서 안전하게 순차 실행
                await TryConnectPortAsync(portName);
            }

            if (!IsLeftConnected) Debug.LogWarning("[ArduinoManager] Left 아두이노를 찾지 못했습니다.");
            if (!IsRightConnected) Debug.LogWarning("[ArduinoManager] Right 아두이노를 찾지 못했습니다.");

            // 하나라도 연결 성공 시 백그라운드 수신 스레드 가동
            if (IsLeftConnected || IsRightConnected)
            {
                StartReadingThread();
            }
        }

        /// <summary> 단일 포트에 대한 실질적인 개방, 딜레이 대기, 식별 문자열 검사 로직을 스레드 풀에서 수행합니다. </summary>
        private async UniTask TryConnectPortAsync(string portName)
        {
            await UniTask.RunOnThreadPool(async () =>
            {
                SerialPort tempPort = new SerialPort(portName, 9600);
                tempPort.ReadTimeout = 2000; 
                
                // 아두이노 보드의 자동 리셋 기능을 활용하여 연결 즉시 초기화 신호(식별 문자열)를 유도하기 위해 DTR 신호 활성화
                tempPort.DtrEnable = true; 

                try { tempPort.Open(); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ArduinoManager] 포트 열기 실패 ({portName}): {e.Message}");
                    return;
                }

                // 보드가 리셋되고 식별 문자열을 전송할 때까지 충분한 시간 비동기 대기
                await UniTask.Delay(TimeSpan.FromSeconds(2.5f));

                string response = string.Empty;
                try
                {
                    if (tempPort.BytesToRead > 0) 
                    {
                        response = tempPort.ReadExisting(); 
                    }
                }
                catch (TimeoutException) { Debug.LogWarning($"[ArduinoManager] 응답 타임아웃 ({portName})"); }
                catch (Exception e) { Debug.LogWarning($"[ArduinoManager] 읽기 예외 ({portName}): {e.Message}"); }

                // 유니티 오브젝트 필드 할당의 안전성을 보장하기 위해 메인 스레드로 강제 복귀
                await UniTask.SwitchToMainThread();

                // 식별 완료된 포트는 타임아웃을 짧게 줄여 실시간 게임 조작 시 딜레이를 최소화함
                if (response.Contains(GameConstants.Hardware.LeftArduino))
                {
                    tempPort.ReadTimeout = 10; 
                    _leftPort = tempPort;
                    Debug.Log($"[ArduinoManager] Left 아두이노 연결 성공: {portName}");
                }
                else if (response.Contains(GameConstants.Hardware.RightArduino))
                {
                    tempPort.ReadTimeout = 10;
                    _rightPort = tempPort;
                    Debug.Log($"[ArduinoManager] Right 아두이노 연결 성공: {portName}");
                }
                else
                {
                    tempPort.Close();
                }
            });
        }

        /// <summary> 메인 스레드와 독립적으로 SerialPort의 버퍼를 쉴 새 없이 모니터링할 전용 백그라운드 스레드를 생성합니다. </summary>
        private void StartReadingThread()
        {
            if (_readThread == null || !_readThread.IsAlive)
            {
                _readThread = new Thread(ReadPortLoop);
                _readThread.IsBackground = true;
                _readThread.Start();
                Debug.Log("[ArduinoManager] 백그라운드 시리얼 수신 스레드 가동 시작");
            }
        }

        /// <summary> 백그라운드 스레드 내부에서 무한 루프를 돌며 유효한 시리얼 문자열을 ConcurrentQueue에 적재합니다. </summary>
        private void ReadPortLoop()
        {
            while (_isRunning)
            {
                if (IsLeftConnected)
                {
                    try
                    {
                        if (_leftPort.BytesToRead > 0)
                        {
                            string leftInput = _leftPort.ReadLine().Trim();
                            if (!string.IsNullOrEmpty(leftInput))
                            {
                                _inputQueue.Enqueue((leftInput, true));
                            }
                        }
                    }
                    catch (TimeoutException) { }
                    catch (Exception) { }
                }

                if (IsRightConnected)
                {
                    try
                    {
                        if (_rightPort.BytesToRead > 0)
                        {
                            string rightInput = _rightPort.ReadLine().Trim();
                            if (!string.IsNullOrEmpty(rightInput))
                            {
                                _inputQueue.Enqueue((rightInput, false));
                            }
                        }
                    }
                    catch (TimeoutException) { }
                    catch (Exception) { }
                }

                // CPU 코어 점유율 폭주(100%) 방지를 위한 미세한 휴식
                Thread.Sleep(5); 
            }
        }

        /// <summary> 우측 플레이어 기기(LED, 사운드 등)에 제어 명령을 전송합니다. </summary>
        public void SendCommandToRight(string command)
        {
            if (IsRightConnected)
            {
                try { _rightPort.WriteLine(command); }
                catch (Exception e) { Debug.LogError($"[ArduinoManager] Right 전송 오류: {e.Message}"); }
            }
        }

        /// <summary> 좌측 플레이어 기기(LED, 사운드 등)에 제어 명령을 전송합니다. </summary>
        public void SendCommandToLeft(string command)
        {
            if (IsLeftConnected)
            {
                try { _leftPort.WriteLine(command); }
                catch (Exception e) { Debug.LogError($"[ArduinoManager] Left 전송 오류: {e.Message}"); }
            }
        }

        /// <summary> 양쪽 장치에 동일한 명령을 동시 하달합니다. (예: 정답 공개 시 동시 사운드 출력) </summary>
        public void SendCommandToBoth(string command)
        {
            SendCommandToLeft(command);
            SendCommandToRight(command);
        }

        /// <summary> 애플리케이션 종료 또는 오브젝트 파괴 시 백그라운드 스레드를 안전하게 종료하고 포트 자원을 반환합니다. </summary>
        private void OnDestroy()
        {
            _isRunning = false; // 루프 탈출 신호 전송

            // 강제 중단 대신 스레드가 자연스럽게 종료될 때까지 최대 500ms 대기 (Graceful Shutdown)
            if (_readThread != null && _readThread.IsAlive)
            {
                _readThread.Join(500);
            }

            if (IsLeftConnected)
            {
                _leftPort.Close();
                _leftPort.Dispose();
            }

            if (IsRightConnected)
            {
                _rightPort.Close();
                _rightPort.Dispose();
            }
        }
    }
}