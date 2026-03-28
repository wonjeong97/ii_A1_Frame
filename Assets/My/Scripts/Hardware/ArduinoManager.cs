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
    /// 복수의 아두이노 장치(Left, Right, Light)와의 시리얼 통신 연결 및 데이터 입출력을 백그라운드에서 관리함.
    /// </summary>
    public class ArduinoManager : MonoBehaviour
    {
        public static ArduinoManager Instance;

        public Action<string, bool> OnHardwareInput;

        private SerialPort _leftPort;
        private SerialPort _rightPort;
        private SerialPort _lightPort;

        private ConcurrentQueue<(string input, bool isLeft)> _inputQueue = new ConcurrentQueue<(string input, bool isLeft)>();

        private Thread _readThread;
        private volatile bool _isRunning = false;

        private DateTime _leftLastWarnTime = DateTime.MinValue;
        private DateTime _rightLastWarnTime = DateTime.MinValue;
        private DateTime _lightLastWarnTime = DateTime.MinValue;
        private readonly TimeSpan warnThrottle = TimeSpan.FromSeconds(5);

        public bool IsLeftConnected => _leftPort != null && _leftPort.IsOpen;
        public bool IsRightConnected => _rightPort != null && _rightPort.IsOpen;
        public bool IsLightConnected => _lightPort != null && _lightPort.IsOpen;
        public bool AreAllConnected => IsLeftConnected && IsRightConnected && IsLightConnected;

        private bool _isReconnecting = false;

        /// <summary>
        /// 싱글톤 인스턴스를 초기화하고 씬 전환 시 파괴되지 않도록 설정함.
        /// </summary>
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

        /// <summary>
        /// 객체 생성 시 백그라운드 포트 스캔 및 자동 연결 프로세스를 시작함.
        /// </summary>
        private void Start()
        {
            _isRunning = true;
            AutoConnectAsync().Forget();
        }

        /// <summary>
        /// 백그라운드 스레드에서 수신된 하드웨어 입력 큐를 메인 스레드에서 소진함.
        /// </summary>
        private void Update()
        {
            // # TODO: 매 프레임 TryDequeue 호출 및 구조체 할당 비용이 발생하므로 프레임당 처리 개수 제한 고려
            while (_inputQueue.TryDequeue(out (string input, bool isLeft) result))
            {
                ProcessHardwareInput(result.input, result.isLeft);
            }
        }

        /// <summary>
        /// 메인 스레드로 전달된 입력 문자열을 파싱하고 이벤트를 발생시킴.
        /// </summary>
        /// <param name="input">수신된 문자열 데이터</param>
        /// <param name="isLeft">좌측 장치 여부</param>
        private void ProcessHardwareInput(string input, bool isLeft)
        {
            // ex: isLeft=true -> sideName="Left"
            string sideName = isLeft ? "Left" : "Right";
            Debug.Log($"[{sideName} 아두이노]>> {input}");

            if (OnHardwareInput != null)
            {
                OnHardwareInput.Invoke(input, isLeft);
            }
        }

        /// <summary>
        /// 통신 장애 복구를 위해 기존 포트를 모두 닫고 전체 연결 시퀀스를 재시작함.
        /// </summary>
        public async UniTask ReconnectAllAsync()
        {
            if (_isReconnecting) return;

            _isReconnecting = true;
            try
            {
                Debug.Log("<color=blue>[ArduinoManager] 3개의 아두이노 강제 재부팅 및 재연결 시작...</color>");
                _isRunning = false;
                
                if (_readThread != null && _readThread.IsAlive)
                {
                    await UniTask.RunOnThreadPool(() => _readThread.Join(500));
                }

                if (_leftPort != null) { try { _leftPort.Close(); _leftPort.Dispose(); } catch { } }
                if (_rightPort != null) { try { _rightPort.Close(); _rightPort.Dispose(); } catch { } }
                if (_lightPort != null) { try { _lightPort.Close(); _lightPort.Dispose(); } catch { } } 

                _leftPort = null;
                _rightPort = null;
                _lightPort = null;

                // 하드웨어 포트 반환 대기 시간 확보
                await UniTask.Delay(TimeSpan.FromSeconds(1.0f));
                
                while (_inputQueue.TryDequeue(out _)) { }

                _isRunning = true;
                await AutoConnectAsync();

                Debug.Log("<color=blue>[ArduinoManager] 강제 재부팅 및 재연결 완료!</color>");
            }
            finally
            {
                _isReconnecting = false;
            }
        }

        /// <summary>
        /// 가용한 모든 COM 포트를 스캔하여 3종의 아두이노 장치 연결을 시도함.
        /// </summary>
        private async UniTask AutoConnectAsync()
        {
            string[] portNames = SerialPort.GetPortNames();
            Debug.Log($"[ArduinoManager] 발견된 전체 COM 포트 수: {portNames.Length}");

            foreach (string portName in portNames)
            {
                if (AreAllConnected) break;

                await TryConnectPortAsync(portName);
            }

            if (!IsLeftConnected) Debug.LogWarning("Left 아두이노를 찾지 못함.");
            if (!IsRightConnected) Debug.LogWarning("Right 아두이노를 찾지 못함.");
            if (!IsLightConnected) Debug.LogWarning("Light 아두이노를 찾지 못함.");

            if (IsLeftConnected || IsRightConnected || IsLightConnected)
            {
                StartReadingThread();
            }
        }

        /// <summary>
        /// 단일 포트를 개방하고 초기 응답 문자열을 분석하여 장치 종류를 식별함.
        /// </summary>
        /// <param name="portName">테스트할 COM 포트 이름</param>
        private async UniTask TryConnectPortAsync(string portName)
        {
            await UniTask.RunOnThreadPool(async () =>
            {
                SerialPort tempPort = new SerialPort(portName, 9600);
                tempPort.ReadTimeout = 2000;
                tempPort.DtrEnable = true;

                try
                {
                    tempPort.Open();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"포트 열기 실패 ({portName}): {e.Message}");
                    tempPort.Dispose(); 
                    return;
                }

                // 아두이노 리셋 후 부트로더 진입 및 초기화 대기
                await UniTask.Delay(TimeSpan.FromSeconds(1.5f));

                string response = string.Empty;
                float maxWaitTime = 10.0f; 
                float elapsedTime = 1.5f;

                // 식별 문자열 수신 시까지 반복 대기
                while (elapsedTime < maxWaitTime)
                {
                    try
                    {
                        if (tempPort.BytesToRead > 0)
                        {
                            response += tempPort.ReadExisting();
                            if (response.Contains("Arduino") || response.Contains(GameConstants.Hardware.LightArduino))
                            {
                                break;
                            }
                        }
                    }
                    catch (TimeoutException) { }
                    catch (Exception) { }

                    await UniTask.Delay(TimeSpan.FromSeconds(1.0f)); 
                    elapsedTime += 1.0f;
                }

                await UniTask.SwitchToMainThread();

                if (response.Contains(GameConstants.Hardware.LeftArduino))
                {
                    tempPort.ReadTimeout = 10;
                    if (_leftPort != null && _leftPort != tempPort)
                    {
                        try { _leftPort.Close(); _leftPort.Dispose(); } catch { }
                    }
                    _leftPort = tempPort;
                    Debug.Log($"Left 아두이노 연결 성공: {portName}");
                }
                else if (response.Contains(GameConstants.Hardware.RightArduino))
                {
                    tempPort.ReadTimeout = 10;
                    if (_rightPort != null && _rightPort != tempPort)
                    {
                        try { _rightPort.Close(); _rightPort.Dispose(); } catch { }
                    }
                    _rightPort = tempPort;
                    Debug.Log($"Right 아두이노 연결 성공: {portName}");
                }
                else if (response.Contains(GameConstants.Hardware.LightArduino))
                {
                    tempPort.ReadTimeout = 10;
                    if (_lightPort != null && _lightPort != tempPort)
                    {
                        try { _lightPort.Close(); _lightPort.Dispose(); } catch { }
                    }
                    _lightPort = tempPort;
                    Debug.Log($"Light 아두이노 연결 성공: {portName}");
                }
                else
                {
                    tempPort.Close();
                    tempPort.Dispose();
                }
            });
        }

        /// <summary>
        /// 메인 스레드 프리징 방지를 위해 시리얼 포트 읽기 전용 백그라운드 스레드를 구동함.
        /// </summary>
        private void StartReadingThread()
        {
            if (_readThread == null || !_readThread.IsAlive)
            {
                _readThread = new Thread(ReadPortLoop);
                _readThread.IsBackground = true;
                _readThread.Start();
                Debug.Log("백그라운드 시리얼 수신 스레드 가동 시작");
            }
        }

        /// <summary>
        /// 무한 루프를 돌며 각 포트 버퍼에 데이터가 있을 경우 큐에 적재함.
        /// </summary>
        private void ReadPortLoop()
        {
            // # TODO: ReadLine() 호출은 매번 새로운 문자열을 동적 할당하여 GC 스파이크를 유발함. 
            // 바이트 버퍼 기반의 풀링(Pooling) 방식으로 구조 변경을 고려할 것.
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
                                _inputQueue.Enqueue((leftInput, true));
                        }
                    }
                    catch (TimeoutException) { }
                    catch (Exception e)
                    {
                        DateTime now = DateTime.UtcNow;
                        if (now - _leftLastWarnTime > warnThrottle)
                        {
                            _leftLastWarnTime = now;
                            Debug.LogWarning($"Left 아두이노 수신 예외: {e.Message}");
                        }
                    }
                }

                if (IsRightConnected)
                {
                    try
                    {
                        if (_rightPort.BytesToRead > 0)
                        {
                            string rightInput = _rightPort.ReadLine().Trim();
                            if (!string.IsNullOrEmpty(rightInput))
                                _inputQueue.Enqueue((rightInput, false));
                        }
                    }
                    catch (TimeoutException) { }
                    catch (Exception e)
                    {
                        DateTime now = DateTime.UtcNow;
                        if (now - _rightLastWarnTime > warnThrottle)
                        {
                            _rightLastWarnTime = now;
                            Debug.LogWarning($"Right 아두이노 수신 예외: {e.Message}");
                        }
                    }
                }
                
                if (IsLightConnected)
                {
                    try
                    {
                        if (_lightPort.BytesToRead > 0)
                        {
                            string lightInput = _lightPort.ReadLine().Trim();
                        }
                    }
                    catch (TimeoutException) { }
                    catch (Exception e)
                    {
                        DateTime now = DateTime.UtcNow;
                        if (now - _lightLastWarnTime > warnThrottle)
                        {
                            _lightLastWarnTime = now;
                            Debug.LogWarning($"Light 아두이노 수신 예외: {e.Message}");
                        }
                    }
                }

                // CPU 점유율 제어를 위해 짧은 휴지기를 가짐
                Thread.Sleep(10);
            }
        }

        /// <summary>
        /// 우측 하드웨어로 제어 명령 문자열을 전송함.
        /// </summary>
        /// <param name="command">전송할 명령어</param>
        public bool SendCommandToRight(string command)
        {
            if (IsRightConnected)
            {
                try { _rightPort.WriteLine(command); return true; }
                catch (Exception e) { Debug.LogError($"Right 전송 오류: {e.Message}"); return false; }
            }
            return false;
        }

        /// <summary>
        /// 좌측 하드웨어로 제어 명령 문자열을 전송함.
        /// </summary>
        /// <param name="command">전송할 명령어</param>
        public bool SendCommandToLeft(string command)
        {
            if (IsLeftConnected)
            {
                try { _leftPort.WriteLine(command); return true; }
                catch (Exception e) { Debug.LogError($"Left 전송 오류: {e.Message}"); return false; }
            }
            return false;
        }

        /// <summary>
        /// 조명 하드웨어로 제어 명령 문자열을 전송함.
        /// </summary>
        /// <param name="command">전송할 명령어</param>
        public bool SendCommandToLight(string command)
        {
            if (IsLightConnected)
            {
                try { _lightPort.WriteLine(command); return true; }
                catch (Exception e) { Debug.LogError($"Light 전송 오류: {e.Message}"); return false; }
            }
            return false;
        }

        /// <summary>
        /// 좌/우 하드웨어 모두에 동일한 제어 명령을 전송함.
        /// </summary>
        /// <param name="command">전송할 명령어</param>
        public bool SendCommandToBoth(string command)
        {
            bool leftResult = SendCommandToLeft(command);
            bool rightResult = SendCommandToRight(command);
            return leftResult && rightResult;
        }

        /// <summary>
        /// 매니저 파괴 시 백그라운드 스레드를 안전하게 종료하고 열린 포트를 닫음.
        /// </summary>
        private void OnDestroy()
        {
            _isRunning = false;

            if (_readThread != null && _readThread.IsAlive)
            {
                _readThread.Join(500);
            }

            if (IsLeftConnected) { _leftPort.Close(); _leftPort.Dispose(); }
            if (IsRightConnected) { _rightPort.Close(); _rightPort.Dispose(); }
            if (IsLightConnected) { _lightPort.Close(); _lightPort.Dispose(); }
        }
    }
}