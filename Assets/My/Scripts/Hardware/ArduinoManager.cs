using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts.Global;
using R3;
using UnityEngine;
using VContainer;
using ZLogger;

namespace My.Scripts.Hardware
{
    /// <summary>
    /// 복수의 아두이노 장치(Left, Right, Light)와의 시리얼 통신 연결 및 데이터 입출력을 백그라운드에서 관리함.
    /// </summary>
    public class ArduinoManager : MonoBehaviour
    {
        /// <summary>
        /// ReadLine()으로 인한 가비지(GC) 생성을 막기 위한 커스텀 바이트 버퍼.
        /// 바이트 단위로 직접 파싱하여 알려진 하드웨어 명령어 상수로 즉시 반환함.
        /// </summary>
        private class SerialBuffer
        {
            private readonly byte[] _data = new byte[256];
            private int _position;

            public void ReadFromPort(SerialPort port, ConcurrentQueue<(string, bool)> queue, bool isLeft, bool enqueue)
            {
                int bytesToRead = port.BytesToRead;
                if (bytesToRead <= 0) return;

                int readCount = port.Read(_data, _position, Math.Min(bytesToRead, _data.Length - _position));
                _position += readCount;

                while (TryExtractLine(out string line))
                {
                    if (enqueue && !string.IsNullOrEmpty(line))
                    {
                        queue.Enqueue((line, isLeft));
                    }
                }
            }

            private bool TryExtractLine(out string line)
            {
                line = null;
                int newLineIndex = FindNewLineIndex();

                if (newLineIndex == -1)
                {
                    return HandleBufferOverflow();
                }

                int dataLength = (newLineIndex > 0 && _data[newLineIndex - 1] == '\r')
                    ? newLineIndex - 1
                    : newLineIndex;

                line = ConvertBytesToCommand(dataLength);

                ShiftBuffer(newLineIndex + 1);
                return true;
            }

            private int FindNewLineIndex()
            {
                for (int i = 0; i < _position; i++)
                {
                    if (_data[i] == '\n') return i;
                }

                return -1;
            }

            private string ConvertBytesToCommand(int length)
            {
                string command = MatchKnownCommand(_data, length);

                if (string.IsNullOrEmpty(command))
                {
                    command = Encoding.ASCII.GetString(_data, 0, length);
                }

                return command;
            }

            private void ShiftBuffer(int skipCount)
            {
                int remaining = _position - skipCount;

                if (remaining > 0)
                {
                    Array.Copy(_data, skipCount, _data, 0, remaining);
                }

                _position = remaining;
            }

            private bool HandleBufferOverflow()
            {
                if (_position >= _data.Length)
                {
                    _position = 0;
                }

                return false;
            }

            private string MatchKnownCommand(byte[] buffer, int len)
            {
                return len switch
                {
                    3 => MatchThreeCharCommand(buffer),
                    6 => MatchSixCharCommand(buffer),
                    _ => null
                };
            }

            private string MatchThreeCharCommand(byte[] buffer)
            {
                if (buffer[1] != 79 || buffer[2] != 110) return null;

                return buffer[0] switch
                {
                    49 => GameConstants.Hardware.Input1On,
                    50 => GameConstants.Hardware.Input2On,
                    51 => GameConstants.Hardware.Input3On,
                    52 => GameConstants.Hardware.Input4On,
                    53 => GameConstants.Hardware.Input5On,
                    _ => null
                };
            }

            private string MatchSixCharCommand(byte[] buffer)
            {
                bool isShotOn = buffer[0] == 83 && buffer[1] == 104 && buffer[2] == 111 &&
                                buffer[3] == 116 && buffer[4] == 79 && buffer[5] == 110;

                return isShotOn ? GameConstants.Hardware.InputShotOn : null;
            }

            public void Reset()
            {
                _position = 0;
            }
        }

        // --- R3 반응형 이벤트 스트림 변수 개편 ---
        private readonly Subject<(string command, bool isLeft)> _hardwareInputSubject = new Subject<(string, bool)>();
        public Observable<(string command, bool isLeft)> OnHardwareInput => _hardwareInputSubject;

        private SerialPort _leftPort;
        private SerialPort _rightPort;
        private SerialPort _lightPort;

        private readonly SerialBuffer _leftBuffer = new SerialBuffer();
        private readonly SerialBuffer _rightBuffer = new SerialBuffer();
        private readonly SerialBuffer _lightBuffer = new SerialBuffer();

        private readonly ConcurrentQueue<(string input, bool isLeft)> _inputQueue =
            new ConcurrentQueue<(string input, bool isLeft)>();

        private Thread _readThread;
        private volatile bool _isRunning;

        private DateTime _leftLastWarnTime;
        private DateTime _rightLastWarnTime;
        private DateTime _lightLastWarnTime;
        private readonly TimeSpan _warnThrottle = TimeSpan.FromSeconds(5);

        public bool IsLeftConnected => _leftPort != null && _leftPort.IsOpen;
        public bool IsRightConnected => _rightPort != null && _rightPort.IsOpen;
        public bool IsLightConnected => _lightPort != null && _lightPort.IsOpen;
        public bool AreAllConnected => IsLeftConnected && IsRightConnected && IsLightConnected;

        private bool _isReconnecting;

        // --- 의존성 주입 (DI) 변수 ---
        private ILogger<ArduinoManager> _logger;

        /// <summary>
        /// VContainer 환경에서 고성능 전역 로그 엔진 인젝션 수령
        /// </summary>
        [Inject]
        public void Construct(ILogger<ArduinoManager> logger)
        {
            _logger = logger;
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
            _logger.ZLogInformation($"[{sideName} 아두이노]>> {input}");

            _hardwareInputSubject.OnNext((input, isLeft));
        }

        public async UniTask ReconnectAllAsync()
        {
            if (_isReconnecting) return;

            _isReconnecting = true;
            try
            {
                _logger.ZLogInformation($"<color=blue>[ArduinoManager] 3개의 아두이노 강제 재부팅 및 재연결 시작...</color>");

                await StopAndCleanupPortsAsync();

                _isRunning = true;
                await AutoConnectAsync();

                _logger.ZLogInformation($"<color=blue>[ArduinoManager] 강제 재부팅 및 재연결 완료!</color>");
            }
            finally
            {
                _isReconnecting = false;
            }
        }

        private async UniTask StopAndCleanupPortsAsync()
        {
            _isRunning = false;

            if (_readThread != null && _readThread.IsAlive)
            {
                await UniTask.RunOnThreadPool(() => _readThread.Join(500));
            }

            CloseAndDisposePort(ref _leftPort);
            CloseAndDisposePort(ref _rightPort);
            CloseAndDisposePort(ref _lightPort);
            _leftBuffer.Reset();
            _rightBuffer.Reset();
            _lightBuffer.Reset();

            await UniTask.Delay(TimeSpan.FromSeconds(1.0f));

            while (_inputQueue.TryDequeue(out _))
            {
            }
        }

        private void CloseAndDisposePort(ref SerialPort port)
        {
            if (port != null)
            {
                try
                {
                    port.Close();
                    port.Dispose();
                }
                catch
                {
                }

                port = null;
            }
        }

        private async UniTask AutoConnectAsync()
        {
            string[] portNames = SerialPort.GetPortNames();
            _logger.ZLogInformation($"[ArduinoManager] 발견된 전체 COM 포트 수: {portNames.Length}");

            foreach (string portName in portNames)
            {
                if (AreAllConnected) break;

                await TryConnectPortAsync(portName);
            }

            if (!IsLeftConnected) _logger.ZLogWarning($"Left 아두이노를 찾지 못함.");
            if (!IsRightConnected) _logger.ZLogWarning($"Right 아두이노를 찾지 못함.");
            if (!IsLightConnected) _logger.ZLogWarning($"Light 아두이노를 찾지 못함.");

            if (IsLeftConnected || IsRightConnected || IsLightConnected)
            {
                StartReadingThread();
            }
        }

        private async UniTask TryConnectPortAsync(string portName)
        {
            // 백그라운드 스레드 풀로 실행 컨텍스트 스위칭
            await UniTask.SwitchToThreadPool();

            SerialPort tempPort = OpenSerialPort(portName);
            if (tempPort == null)
            {
                await UniTask.SwitchToMainThread();
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1.5f));
            string response = await WaitForDeviceResponseAsync(tempPort);

            await UniTask.SwitchToMainThread();
            AssignPortByResponse(response, tempPort, portName);
        }

        private SerialPort OpenSerialPort(string portName)
        {
            SerialPort port = new SerialPort(portName, 9600);
            port.ReadTimeout = 2000;
            port.DtrEnable = true;

            try
            {
                port.Open();
                return port;
            }
            catch (Exception e)
            {
                _logger.ZLogWarning($"포트 열기 실패 ({portName}): {e.Message}");
                port.Dispose();
                return null;
            }
        }

        private async UniTask<string> WaitForDeviceResponseAsync(SerialPort port)
        {
            StringBuilder sb = new StringBuilder();
            float maxWaitTime = 10.0f;
            float elapsedTime = 1.5f;

            while (elapsedTime < maxWaitTime)
            {
                if (TryReadDeviceIdentifier(port, sb))
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(1.0f));
                elapsedTime += 1.0f;
            }

            return sb.ToString();
        }

        private bool TryReadDeviceIdentifier(SerialPort port, StringBuilder sb)
        {
            try
            {
                if (port.BytesToRead > 0)
                {
                    sb.Append(port.ReadExisting());
                    string currentResponse = sb.ToString();

                    return currentResponse.Contains("Arduino") ||
                           currentResponse.Contains(GameConstants.Hardware.LightArduino);
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        private void AssignPortByResponse(string response, SerialPort newPort, string portName)
        {
            newPort.ReadTimeout = 10;

            if (response.Contains(GameConstants.Hardware.LeftArduino))
            {
                CloseAndDisposePort(ref _leftPort);
                _leftPort = newPort;
                _logger.ZLogInformation($"Left 아두이노 연결 성공: {portName}");
            }
            else if (response.Contains(GameConstants.Hardware.RightArduino))
            {
                CloseAndDisposePort(ref _rightPort);
                _rightPort = newPort;
                _logger.ZLogInformation($"Right 아두이노 연결 성공: {portName}");
            }
            else if (response.Contains(GameConstants.Hardware.LightArduino))
            {
                CloseAndDisposePort(ref _lightPort);
                _lightPort = newPort;
                _logger.ZLogInformation($"Light 아두이노 연결 성공: {portName}");
            }
            else
            {
                CloseAndDisposePort(ref newPort);
            }
        }

        private void StartReadingThread()
        {
            if (_readThread == null || !_readThread.IsAlive)
            {
                _readThread = new Thread(ReadPortLoop);
                _readThread.IsBackground = true;
                _readThread.Start();
                _logger.ZLogInformation($"백그라운드 시리얼 수신 스레드 가동 시작");
            }
        }

        private void ReadPortLoop()
        {
            while (_isRunning)
            {
                ReadSinglePort(IsLeftConnected, _leftPort, _leftBuffer, true, true, ref _leftLastWarnTime, "Left");
                ReadSinglePort(IsRightConnected, _rightPort, _rightBuffer, false, true, ref _rightLastWarnTime,
                    "Right");
                ReadSinglePort(IsLightConnected, _lightPort, _lightBuffer, false, false, ref _lightLastWarnTime,
                    "Light");

                Thread.Sleep(10);
            }
        }

        private void ReadSinglePort(bool isConnected, SerialPort port, SerialBuffer buffer, bool isLeft, bool enqueue,
            ref DateTime lastWarnTime, string logPrefix)
        {
            if (!isConnected || port == null) return;

            try
            {
                buffer.ReadFromPort(port, _inputQueue, isLeft, enqueue);
            }
            catch (TimeoutException)
            {
            }
            catch (Exception e)
            {
                HandleReadException(e, ref lastWarnTime, logPrefix);
            }
        }

        private void HandleReadException(Exception e, ref DateTime lastWarnTime, string logPrefix)
        {
            DateTime now = DateTime.UtcNow;
            if (now - lastWarnTime > _warnThrottle)
            {
                lastWarnTime = now;
                _logger.ZLogWarning($"{logPrefix} 아두이노 수신 예외: {e.Message}");
            }
        }

        private bool SendCommandInternal(SerialPort port, string command, string portName)
        {
            if (port == null || !port.IsOpen)
            {
                return false;
            }

            try
            {
                port.WriteLine(command);
                return true;
            }
            catch (Exception e)
            {
                _logger.ZLogError($"{portName} 전송 오류: {e.Message}");
                return false;
            }
        }

        public bool SendCommandToRight(string command)
        {
            return SendCommandInternal(_rightPort, command, "Right");
        }

        public bool SendCommandToLeft(string command)
        {
            return SendCommandInternal(_leftPort, command, "Left");
        }

        public bool SendCommandToLight(string command)
        {
            return SendCommandInternal(_lightPort, command, "Light");
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

            CloseAndDisposePort(ref _leftPort);
            CloseAndDisposePort(ref _rightPort);
            CloseAndDisposePort(ref _lightPort);

            _hardwareInputSubject.OnCompleted();
            _hardwareInputSubject.Dispose();
        }
    }
}