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
    public class ArduinoManager : MonoBehaviour
    {
        public static ArduinoManager Instance;

        public Action<string, bool> OnHardwareInput;

        private SerialPort _leftPort;
        private SerialPort _rightPort;
        private SerialPort _lightPort;

        private ConcurrentQueue<(string input, bool isLeft)> _inputQueue =
            new ConcurrentQueue<(string input, bool isLeft)>();

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

        private async UniTask AutoConnectAsync()
        {
            string[] portNames = SerialPort.GetPortNames();
            Debug.Log($"[ArduinoManager] 발견된 전체 COM 포트 수: {portNames.Length}");

            foreach (string portName in portNames)
            {
                // 3개가 모두 연결되면 포트 스캔 중지
                if (AreAllConnected) break;

                await TryConnectPortAsync(portName);
            }

            if (!IsLeftConnected) Debug.LogWarning("[ArduinoManager] Left 아두이노를 찾지 못했습니다.");
            if (!IsRightConnected) Debug.LogWarning("[ArduinoManager] Right 아두이노를 찾지 못했습니다.");
            if (!IsLightConnected) Debug.LogWarning("[ArduinoManager] Light 아두이노를 찾지 못했습니다.");

            if (IsLeftConnected || IsRightConnected || IsLightConnected)
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
                    tempPort.Dispose(); 
                    return;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(1.5f));

                string response = string.Empty;
                float maxWaitTime = 10.0f; 
                float elapsedTime = 1.5f;

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
                    Debug.Log($"[ArduinoManager] Left 아두이노 연결 성공: {portName}");
                }
                else if (response.Contains(GameConstants.Hardware.RightArduino))
                {
                    tempPort.ReadTimeout = 10;
                    if (_rightPort != null && _rightPort != tempPort)
                    {
                        try { _rightPort.Close(); _rightPort.Dispose(); } catch { }
                    }
                    _rightPort = tempPort;
                    Debug.Log($"[ArduinoManager] Right 아두이노 연결 성공: {portName}");
                }
                else if (response.Contains(GameConstants.Hardware.LightArduino))
                {
                    tempPort.ReadTimeout = 10;
                    if (_lightPort != null && _lightPort != tempPort)
                    {
                        try { _lightPort.Close(); _lightPort.Dispose(); } catch { }
                    }
                    _lightPort = tempPort;
                    Debug.Log($"[ArduinoManager] Light 아두이노 연결 성공: {portName}");
                }
                else
                {
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
                            Debug.LogWarning($"[ArduinoManager] Left 아두이노 수신 예외: {e.Message}");
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
                            Debug.LogWarning($"[ArduinoManager] Right 아두이노 수신 예외: {e.Message}");
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
                            Debug.LogWarning($"[ArduinoManager] Light 아두이노 수신 예외: {e.Message}");
                        }
                    }
                }

                Thread.Sleep(10);
            }
        }

        public bool SendCommandToRight(string command)
        {
            if (IsRightConnected)
            {
                try { _rightPort.WriteLine(command); return true; }
                catch (Exception e) { Debug.LogError($"[ArduinoManager] Right 전송 오류: {e.Message}"); return false; }
            }
            return false;
        }

        public bool SendCommandToLeft(string command)
        {
            if (IsLeftConnected)
            {
                try { _leftPort.WriteLine(command); return true; }
                catch (Exception e) { Debug.LogError($"[ArduinoManager] Left 전송 오류: {e.Message}"); return false; }
            }
            return false;
        }

        public bool SendCommandToLight(string command)
        {
            if (IsLightConnected)
            {
                try { _lightPort.WriteLine(command); return true; }
                catch (Exception e) { Debug.LogError($"[ArduinoManager] Light 전송 오류: {e.Message}"); return false; }
            }
            return false;
        }

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

            if (IsLeftConnected) { _leftPort.Close(); _leftPort.Dispose(); }
            if (IsRightConnected) { _rightPort.Close(); _rightPort.Dispose(); }
            if (IsLightConnected) { _lightPort.Close(); _lightPort.Dispose(); } // [추가됨]
        }
    }
}