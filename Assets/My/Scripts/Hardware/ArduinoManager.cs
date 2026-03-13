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
        private bool _isRunning = false;

        // 예외 로그 스로틀링용 변수
        private DateTime _leftLastWarnTime = DateTime.MinValue;
        private DateTime _rightLastWarnTime = DateTime.MinValue;
        private readonly TimeSpan WarnThrottle = TimeSpan.FromSeconds(5);

        public bool IsLeftConnected => _leftPort != null && _leftPort.IsOpen;
        public bool IsRightConnected => _rightPort != null && _rightPort.IsOpen;

        // 양쪽 아두이노가 모두 연결되었는지 확인하는 프로퍼티
        public bool AreBothConnected => IsLeftConnected && IsRightConnected;

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

        private async UniTaskVoid AutoConnectAsync()
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

                await UniTask.Delay(TimeSpan.FromSeconds(2.5f));

                string response = string.Empty;
                try
                {
                    if (tempPort.BytesToRead > 0)
                    {
                        response = tempPort.ReadExisting();
                    }
                }
                catch (TimeoutException)
                {
                    Debug.LogWarning($"[ArduinoManager] 응답 타임아웃 ({portName})");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ArduinoManager] 읽기 예외 ({portName}): {e.Message}");
                }

                await UniTask.SwitchToMainThread();

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
                        if (now - _leftLastWarnTime > WarnThrottle)
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
                        if (now - _rightLastWarnTime > WarnThrottle)
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

                Thread.Sleep(50);
            }
        }

        public void SendCommandToRight(string command)
        {
            if (IsRightConnected)
            {
                try
                {
                    _rightPort.WriteLine(command);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ArduinoManager] Right 전송 오류: {e.Message}");
                }
            }
        }

        public void SendCommandToLeft(string command)
        {
            if (IsLeftConnected)
            {
                try
                {
                    _leftPort.WriteLine(command);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ArduinoManager] Left 전송 오류: {e.Message}");
                }
            }
        }

        public void SendCommandToBoth(string command)
        {
            SendCommandToLeft(command);
            SendCommandToRight(command);
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