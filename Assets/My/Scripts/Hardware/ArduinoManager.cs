using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using My.Scripts.Global;

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

            /// <summary> 버퍼 데이터에서 한 줄(\n 기준)을 찾아 명령어로 추출함. </summary>
            private bool TryExtractLine(out string line)
            {
                line = null;
                int newLineIndex = FindNewLineIndex();
            
                if (newLineIndex == -1)
                {
                    return HandleBufferOverflow();
                }

                // 개행 문자를 제외한 실제 데이터 길이 계산
                int dataLength = (newLineIndex > 0 && _data[newLineIndex - 1] == '\r') 
                    ? newLineIndex - 1 
                    : newLineIndex;

                line = ConvertBytesToCommand(dataLength);
            
                // 처리한 데이터만큼 버퍼를 앞으로 밀어 다음 데이터 수신 준비
                ShiftBuffer(newLineIndex + 1);
                return true;
            }
            
            /// <summary> 버퍼 내에서 개행 문자(\n)의 인덱스를 탐색함. </summary>
            private int FindNewLineIndex()
            {
                for (int i = 0; i < _position; i++)
                {
                    if (_data[i] == '\n') return i;
                }
                return -1;
            }
            
            /// <summary> 추출된 바이트를 미리 정의된 상수 명령어와 비교하거나 문자열로 변환함. </summary>
            private string ConvertBytesToCommand(int length)
            {
                // 가비지 생성을 막기 위해 하드웨어 상수를 우선 매칭
                string command = MatchKnownCommand(_data, length);
            
                if (string.IsNullOrEmpty(command))
                {
                    command = System.Text.Encoding.ASCII.GetString(_data, 0, length);
                }
            
                return command;
            }
            
            /// <summary> 처리 완료된 바이트를 제거하고 남은 데이터를 버퍼의 시작점으로 이동시킴. </summary>
            private void ShiftBuffer(int skipCount)
            {
                int remaining = _position - skipCount;
            
                if (remaining > 0)
                {
                    Array.Copy(_data, skipCount, _data, 0, remaining);
                }
            
                _position = remaining;
            }
            
            /// <summary> 버퍼가 꽉 찼음에도 개행 문자가 없는 비정상 상황을 처리함. </summary>
            private bool HandleBufferOverflow()
            {
                if (_position >= _data.Length)
                {
                    _position = 0;
                }
                return false;
            }

            /// <summary> 바이트 패턴을 분석하여 캐싱된 명령어 상수를 반환함. </summary>
            private string MatchKnownCommand(byte[] buffer, int len)
            {
                // 명령어 길이에 따라 조기 필터링하여 불필요한 비교 연산 제거
                return len switch
                {
                    3 => MatchThreeCharCommand(buffer),
                    6 => MatchSixCharCommand(buffer),
                    _ => null
                };
            }
            
            /// <summary> 3글자 명령어(예: 1On)를 바이트 단위로 비교하여 상수 반환. </summary>
            private string MatchThreeCharCommand(byte[] buffer)
            {
                // 공통 접미사 'On' 확인 (ASCII: O=79, n=110)
                if (buffer[1] != 79 || buffer[2] != 110) return null;

                return buffer[0] switch
                {
                    49 => GameConstants.Hardware.Input1On, // '1'
                    50 => GameConstants.Hardware.Input2On, // '2'
                    51 => GameConstants.Hardware.Input3On, // '3'
                    52 => GameConstants.Hardware.Input4On, // '4'
                    53 => GameConstants.Hardware.Input5On, // '5'
                    _ => null
                };
            }
            
            /// <summary> 6글자 명령어(ShotOn)를 바이트 단위로 비교하여 상수 반환. </summary>
            private string MatchSixCharCommand(byte[] buffer)
            {
                // "ShotOn" 문자열의 각 바이트를 순차 비교하여 동적 할당 회피
                // 예시: S(83), h(104), o(111), t(116), O(79), n(110)
                bool isShotOn = buffer[0] == 83 && buffer[1] == 104 && buffer[2] == 111 && 
                                buffer[3] == 116 && buffer[4] == 79 && buffer[5] == 110;

                return isShotOn ? GameConstants.Hardware.InputShotOn : null;
            }
                    
            public void Reset()
            {
                _position = 0;
            }
        }

        public static ArduinoManager Instance;

        public Action<string, bool> onHardwareInput;

        private SerialPort _leftPort;
        private SerialPort _rightPort;
        private SerialPort _lightPort;
        
        private readonly SerialBuffer _leftBuffer = new SerialBuffer();
        private readonly SerialBuffer _rightBuffer = new SerialBuffer();
        private readonly SerialBuffer _lightBuffer = new SerialBuffer();

        private ConcurrentQueue<(string input, bool isLeft)> _inputQueue = new ConcurrentQueue<(string input, bool isLeft)>();

        private Thread _readThread;
        private volatile bool _isRunning;

        private DateTime _leftLastWarnTime;
        private DateTime _rightLastWarnTime;
        private DateTime _lightLastWarnTime;
        private readonly TimeSpan warnThrottle = TimeSpan.FromSeconds(5);

        public bool IsLeftConnected => _leftPort != null && _leftPort.IsOpen;
        public bool IsRightConnected => _rightPort != null && _rightPort.IsOpen;
        public bool IsLightConnected => _lightPort != null && _lightPort.IsOpen;
        public bool AreAllConnected => IsLeftConnected && IsRightConnected && IsLightConnected;

        private bool _isReconnecting;

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
            string sideName = isLeft ? "Left" : "Right";
            Debug.Log($"[{sideName} 아두이노]>> {input}");

            if (onHardwareInput != null)
            {
                onHardwareInput.Invoke(input, isLeft);
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
                
                await StopAndCleanupPortsAsync();
                
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
        /// 연결 재시작을 위해 스레드를 종료하고 모든 포트를 닫은 후 큐를 정리함.
        /// </summary>
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

            // 하드웨어 포트 반환 대기 시간 확보
            await UniTask.Delay(TimeSpan.FromSeconds(1.0f));
            
            while (_inputQueue.TryDequeue(out _)) { }
        }
        
        /// <summary>
        /// 시리얼 포트를 안전하게 닫고 리소스를 해제한 뒤 null로 초기화함.
        /// </summary>
        private void CloseAndDisposePort(ref SerialPort port)
        {
            if (port != null)
            {
                try { port.Close(); port.Dispose(); } catch { }
                port = null;
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
                SerialPort tempPort = OpenSerialPort(portName);
                if (tempPort == null) return;

                // 아두이노 리셋 후 부트로더 진입 및 초기화 대기
                await UniTask.Delay(TimeSpan.FromSeconds(1.5f));

                string response = await WaitForDeviceResponseAsync(tempPort);

                await UniTask.SwitchToMainThread();
                AssignPortByResponse(response, tempPort, portName);
            });
        }
        
        /// <summary>
        /// 시리얼 포트 인스턴스를 생성하고 연결을 시도함.
        /// </summary>
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
                Debug.LogWarning($"포트 열기 실패 ({portName}): {e.Message}");
                port.Dispose(); 
                return null;
            }
        }
        
        /// <summary>
        /// 포트 개방 후 아두이노 장치의 고유 식별 문자열이 수신될 때까지 대기함.
        /// </summary>
        private async UniTask<string> WaitForDeviceResponseAsync(SerialPort port)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            float maxWaitTime = 10.0f; 
            float elapsedTime = 1.5f;

            while (elapsedTime < maxWaitTime)
            {
                if (TryReadDeviceIdentifier(port, sb))
                {
                    break;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(1.0f)); 
                elapsedTime += 1.0f;
            }

            return sb.ToString();
        }
        
        /// <summary>
        /// 버퍼의 문자열을 읽고 아두이노 장치 식별자가 포함되어 있는지 확인함.
        /// </summary>
        private bool TryReadDeviceIdentifier(SerialPort port, System.Text.StringBuilder sb)
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
            catch (Exception) { } 

            return false;
        }
        
        /// <summary>
        /// 수신된 응답 문자열을 바탕으로 적절한 장치(Left, Right, Light)에 포트를 할당함.
        /// </summary>
        private void AssignPortByResponse(string response, SerialPort newPort, string portName)
        {
            newPort.ReadTimeout = 10;

            if (response.Contains(GameConstants.Hardware.LeftArduino))
            {
                CloseAndDisposePort(ref _leftPort);
                _leftPort = newPort;
                Debug.Log($"Left 아두이노 연결 성공: {portName}");
            }
            else if (response.Contains(GameConstants.Hardware.RightArduino))
            {
                CloseAndDisposePort(ref _rightPort);
                _rightPort = newPort;
                Debug.Log($"Right 아두이노 연결 성공: {portName}");
            }
            else if (response.Contains(GameConstants.Hardware.LightArduino))
            {
                CloseAndDisposePort(ref _lightPort);
                _lightPort = newPort;
                Debug.Log($"Light 아두이노 연결 성공: {portName}");
            }
            else
            {
                CloseAndDisposePort(ref newPort);
            }
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
            while (_isRunning)
            {
                ReadSinglePort(IsLeftConnected, _leftPort, _leftBuffer, true, true, ref _leftLastWarnTime, "Left");
                ReadSinglePort(IsRightConnected, _rightPort, _rightBuffer, false, true, ref _rightLastWarnTime, "Right");
                ReadSinglePort(IsLightConnected, _lightPort, _lightBuffer, false, false, ref _lightLastWarnTime, "Light"); 

                // CPU 점유율 제어를 위해 짧은 휴지기를 가짐
                Thread.Sleep(10);
            }
        }
        
        /// <summary>
        /// 단일 포트의 데이터를 읽고 조건에 따라 큐에 적재하거나 예외를 로깅함.
        /// 커스텀 버퍼를 활용하여 GC 할당을 방지함.
        /// </summary>
        private void ReadSinglePort(bool isConnected, SerialPort port, SerialBuffer buffer, bool isLeft, bool enqueue, ref DateTime lastWarnTime, string logPrefix)
        {
            if (!isConnected || port == null) return;

            try
            {
                buffer.ReadFromPort(port, _inputQueue, isLeft, enqueue);
            }
            catch (TimeoutException) { }
            catch (Exception e)
            {
                HandleReadException(e, ref lastWarnTime, logPrefix);
            }
        }
        
        /// <summary>
        /// 연속적인 에러 로그 스파이크를 막기 위해 시간 기반으로 스로틀링(Throttling)하여 예외를 로깅함.
        /// </summary>
        private void HandleReadException(Exception e, ref DateTime lastWarnTime, string logPrefix)
        {
            DateTime now = DateTime.UtcNow;
            if (now - lastWarnTime > warnThrottle)
            {
                lastWarnTime = now;
                Debug.LogWarning($"{logPrefix} 아두이노 수신 예외: {e.Message}");
            }
        }
        
        /// <summary>
        /// 포트 개방 유무 확인 및 예외 처리를 통합하여 시리얼 명령어 송신 코드의 중복을 제거함.
        /// </summary>
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
                Debug.LogError($"{portName} 전송 오류: {e.Message}"); 
                return false; 
            }
        }

        /// <summary>
        /// 우측 하드웨어로 제어 명령 문자열을 전송함.
        /// </summary>
        public bool SendCommandToRight(string command)
        {
            return SendCommandInternal(_rightPort, command, "Right");
        }

        /// <summary>
        /// 좌측 하드웨어로 제어 명령 문자열을 전송함.
        /// </summary>
        public bool SendCommandToLeft(string command)
        {
            return SendCommandInternal(_leftPort, command, "Left");
        }

        /// <summary>
        /// 조명 하드웨어로 제어 명령 문자열을 전송함.
        /// </summary>
        public bool SendCommandToLight(string command)
        {
            return SendCommandInternal(_lightPort, command, "Light");
        }

        /// <summary>
        /// 좌/우 하드웨어 모두에 동일한 제어 명령을 전송함.
        /// </summary>
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