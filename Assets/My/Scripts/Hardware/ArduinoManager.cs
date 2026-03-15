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
    /// </summary>
    public class ArduinoManager : MonoBehaviour
    {
        public static ArduinoManager Instance;

        /// <summary> 하드웨어 입력 발생 시 구독 중인 각 페이지(PopupGamePage 등)로 신호를 전달하는 델리게이트 이벤트 </summary>
        public Action<string, bool> OnHardwareInput;

        private SerialPort _leftPort;
        private SerialPort _rightPort;

        // 백그라운드 스레드에서 수신된 데이터를 메인 스레드로 안전하게 넘기기 위한 스레드-세이프 큐
        private ConcurrentQueue<(string input, bool isLeft)> _inputQueue =
            new ConcurrentQueue<(string input, bool isLeft)>();

        private Thread _readThread;

        // 다중 스레드 동기화 접근을 위한 volatile 키워드 부여
        private volatile bool _isRunning = false;

        // 예외 로그 스로틀링용 변수
        private DateTime _leftLastWarnTime = DateTime.MinValue;
        private DateTime _rightLastWarnTime = DateTime.MinValue;
        private readonly TimeSpan warnThrottle = TimeSpan.FromSeconds(5);

        public bool IsLeftConnected => _leftPort != null && _leftPort.IsOpen;
        public bool IsRightConnected => _rightPort != null && _rightPort.IsOpen;

        // 양쪽 아두이노가 모두 연결되었는지 확인하는 프로퍼티
        public bool AreBothConnected => IsLeftConnected && IsRightConnected;

        private bool _isReconnecting = false;

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

        private void Update()
        {
            while (_inputQueue.TryDequeue(out (string input, bool isLeft) result))
            {
                ProcessHardwareInput(result.input, result.isLeft);
            }
        }

        private void ProcessHardwareInput(string input, bool isLeft)
        {
            string sideName = isLeft ? "Left" : "Right";
            Debug.Log($"[{sideName} 아두이노]>> {input}");

            if (OnHardwareInput != null)
            {
                OnHardwareInput.Invoke(input, isLeft);
            }
        }

        /// <summary> 모든 연결을 끊고 DTR 신호를 다시 발생시켜 아두이노를 하드웨어적으로 강제 재부팅합니다. </summary>
        public async UniTask ReconnectAllAsync()
        {
            if (_isReconnecting) return;

            _isReconnecting = true;
            try
            {
                Debug.Log("<color=blue>[ArduinoManager] 아두이노 하드웨어 강제 재부팅 및 재연결 시작...</color>");
                // 1. 수신 스레드 안전하게 정지
                _isRunning = false;
                if (_readThread != null && _readThread.IsAlive)
                {
                    await UniTask.RunOnThreadPool(() => _readThread.Join(500));
                }

                // 2. 기존 포트 닫기 및 할당 해제
                if (_leftPort != null)
                {
                    try
                    {
                        _leftPort.Close();
                        _leftPort.Dispose();
                    }
                    catch
                    {
                    }
                }

                if (_rightPort != null)
                {
                    try
                    {
                        _rightPort.Close();
                        _rightPort.Dispose();
                    }
                    catch
                    {
                    }
                }

                _leftPort = null;
                _rightPort = null;
                // 3. OS 포트 반환 및 잔여 버퍼 데이터 소멸 대기
                await UniTask.Delay(TimeSpan.FromSeconds(1.0f));
                // 4. 수신 큐 비우기
                while (_inputQueue.TryDequeue(out _))
                {
                }

                // 5. 재연결 시퀀스 가동 (포트가 열리면서 DTR 신호로 아두이노가 재부팅됨)
                _isRunning = true;
                await AutoConnectAsync();

                Debug.Log("<color=blue>[ArduinoManager] 강제 재부팅 및 재연결 완료!</color>");
            }
            finally
            {
                _isReconnecting = false;
            }
        }

        private async UniTask AutoConnectAsync()
        {
            string[] portNames = SerialPort.GetPortNames();
            Debug.Log($"[ArduinoManager] 발견된 전체 COM 포트 수: {portNames.Length}");

            foreach (string portName in portNames)
            {
                if (AreBothConnected) break;

                await TryConnectPortAsync(portName);
            }

            if (!IsLeftConnected) Debug.LogWarning("[ArduinoManager] Left 아두이노를 찾지 못했습니다.");
            if (!IsRightConnected) Debug.LogWarning("[ArduinoManager] Right 아두이노를 찾지 못했습니다.");

            if (IsLeftConnected || IsRightConnected)
            {
                StartReadingThread();
            }
        }

        private async UniTask TryConnectPortAsync(string portName)
        {
            await UniTask.RunOnThreadPool(async () =>
            {
                SerialPort tempPort = new SerialPort(portName, 9600);
                tempPort.ReadTimeout = 2000;

                // 아두이노 강제 리셋을 위한 DTR 활성화
                tempPort.DtrEnable = true;

                try
                {
                    tempPort.Open();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ArduinoManager] 포트 열기 실패 ({portName}): {e.Message}");
                    tempPort.Dispose(); // 누수 방지
                    return;
                }

                // 아두이노가 재부팅되는 최소 시간(1.5초)은 무조건 대기
                await UniTask.Delay(TimeSpan.FromSeconds(1.5f));

                string response = string.Empty;
                float maxWaitTime = 10.0f; // 최대 10초까지 넉넉하게 기다림
                float elapsedTime = 1.5f;

                // 남은 시간 동안 1초 간격으로 확인 (Polling)
                while (elapsedTime < maxWaitTime)
                {
                    try
                    {
                        if (tempPort.BytesToRead > 0)
                        {
                            response += tempPort.ReadExisting();

                            // 만약 버퍼에 "Arduino"라는 단어가 도착했다면, 더 이상 기다릴 필요 없이 즉시 루프 탈출!
                            if (response.Contains("Arduino"))
                            {
                                break;
                            }
                        }
                    }
                    catch (TimeoutException)
                    {
                    }
                    catch (Exception)
                    {
                    }

                    await UniTask.Delay(TimeSpan.FromSeconds(1.0f)); // 1초 대기
                    elapsedTime += 1.0f;
                }

                // 유니티 오브젝트 할당을 위해 메인 스레드로 복귀
                await UniTask.SwitchToMainThread();

                if (response.Contains(GameConstants.Hardware.LeftArduino))
                {
                    tempPort.ReadTimeout = 10;

                    // 기존에 할당된 포트가 있었다면 Handle Leak을 막기 위해 확실하게 해제
                    if (_leftPort != null && _leftPort != tempPort)
                    {
                        try
                        {
                            _leftPort.Close();
                            _leftPort.Dispose();
                        }
                        catch
                        {
                        }
                    }

                    _leftPort = tempPort;
                    Debug.Log($"[ArduinoManager] Left 아두이노 연결 성공: {portName}");
                }
                else if (response.Contains(GameConstants.Hardware.RightArduino))
                {
                    tempPort.ReadTimeout = 10;

                    if (_rightPort != null && _rightPort != tempPort)
                    {
                        try
                        {
                            _rightPort.Close();
                            _rightPort.Dispose();
                        }
                        catch
                        {
                        }
                    }

                    _rightPort = tempPort;
                    Debug.Log($"[ArduinoManager] Right 아두이노 연결 성공: {portName}");
                }
                else
                {
                    // 불필요한 포트 정상 종료 및 폐기
                    tempPort.Close();
                    tempPort.Dispose();
                }
            });
        }

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
                    catch (TimeoutException)
                    {
                    }
                    catch (Exception e)
                    {
                        DateTime now = DateTime.UtcNow;
                        if (now - _leftLastWarnTime > warnThrottle)
                        {
                            _leftLastWarnTime = now;
                            string bytesInfo = "N/A";
                            try
                            {
                                bytesInfo = _leftPort.BytesToRead.ToString();
                            }
                            catch
                            {
                            }

                            Debug.LogWarning(
                                $"[ArduinoManager] Left 아두이노 수신 예외: {e.Message} | BytesToRead: {bytesInfo}");
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
                            {
                                _inputQueue.Enqueue((rightInput, false));
                            }
                        }
                    }
                    catch (TimeoutException)
                    {
                    }
                    catch (Exception e)
                    {
                        DateTime now = DateTime.UtcNow;
                        if (now - _rightLastWarnTime > warnThrottle)
                        {
                            _rightLastWarnTime = now;
                            string bytesInfo = "N/A";
                            try
                            {
                                bytesInfo = _rightPort.BytesToRead.ToString();
                            }
                            catch
                            {
                            }

                            Debug.LogWarning(
                                $"[ArduinoManager] Right 아두이노 수신 예외: {e.Message} | BytesToRead: {bytesInfo}");
                        }
                    }
                }

                Thread.Sleep(10);
            }
        }

        /// <summary> 우측 기기에 제어 명령을 전송하고 성공 여부를 반환합니다. </summary>
        public bool SendCommandToRight(string command)
        {
            if (IsRightConnected)
            {
                try
                {
                    _rightPort.WriteLine(command);
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ArduinoManager] Right 전송 오류: {e.Message}");
                    return false;
                }
            }

            return false;
        }

        /// <summary> 좌측 기기에 제어 명령을 전송하고 성공 여부를 반환합니다. </summary>
        public bool SendCommandToLeft(string command)
        {
            if (IsLeftConnected)
            {
                try
                {
                    _leftPort.WriteLine(command);
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ArduinoManager] Left 전송 오류: {e.Message}");
                    return false;
                }
            }

            return false;
        }

        /// <summary> 양쪽 장치에 동일한 명령을 동시 하달하고 송신 성공 여부를 취합합니다. </summary>
        public bool SendCommandToBoth(string command)
        {
            bool leftResult = SendCommandToLeft(command);
            bool rightResult = SendCommandToRight(command);

            return leftResult && rightResult;
        }

        private void OnDestroy()
        {
            _isRunning = false;

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