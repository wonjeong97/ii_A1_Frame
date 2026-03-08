using System;
using System.Collections;
using System.IO.Ports;
using UnityEngine;

namespace My.Scripts.Hardware
{
    /// <summary>
    /// 장치 관리자에 연결된 COM 포트를 스캔하여 좌/우 아두이노를 식별하고 제어 명령을 송수신하는 매니저 클래스.
    /// </summary>
    public class ArduinoManager : MonoBehaviour
    {
        public static ArduinoManager Instance;

        // 하드웨어 입력 발생 시 다른 스크립트(페이지 등)에 알려주기 위한 이벤트
        public Action<string, bool> OnHardwareInput;

        private SerialPort _leftPort;
        private SerialPort _rightPort;

        public bool IsLeftConnected => _leftPort != null && _leftPort.IsOpen;
        public bool IsRightConnected => _rightPort != null && _rightPort.IsOpen;

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
            StartCoroutine(AutoConnectRoutine());
        }

        private void Update()
        {
            // 1. 왼쪽 아두이노에서 들어온 신호 읽기
            if (IsLeftConnected && _leftPort.BytesToRead > 0)
            {
                try
                {
                    string leftInput = _leftPort.ReadLine().Trim();
                    if (!string.IsNullOrEmpty(leftInput))
                    {
                        ProcessHardwareInput(leftInput, true);
                    }
                }
                catch (TimeoutException) { }
                catch (Exception e) { Debug.LogWarning($"[ArduinoManager] Left 수신 예외: {e.Message}"); }
            }

            // 2. 오른쪽 아두이노에서 들어온 신호 읽기
            if (IsRightConnected && _rightPort.BytesToRead > 0)
            {
                try
                {
                    string rightInput = _rightPort.ReadLine().Trim();
                    if (!string.IsNullOrEmpty(rightInput))
                    {
                        ProcessHardwareInput(rightInput, false);
                    }
                }
                catch (TimeoutException) { }
                catch (Exception e) { Debug.LogWarning($"[ArduinoManager] Right 수신 예외: {e.Message}"); }
            }
        }

        private void ProcessHardwareInput(string input, bool isLeft)
        {
            string sideName = isLeft ? "Left" : "Right";
            Debug.Log($"[{sideName} 아두이노] 버튼 입력 감지됨: {input}");

            // 이벤트를 발생시켜 구독 중인 페이지들에게 입력된 문자열과 방향을 전달
            if (OnHardwareInput != null)
            {
                OnHardwareInput.Invoke(input, isLeft);
            }
        }

        private IEnumerator AutoConnectRoutine()
        {
            string[] portNames = SerialPort.GetPortNames();
            Debug.Log($"[ArduinoManager] 발견된 전체 COM 포트 수: {portNames.Length}");

            foreach (string portName in portNames)
            {
                if (IsLeftConnected && IsRightConnected) break;

                SerialPort tempPort = new SerialPort(portName, 9600);
                tempPort.ReadTimeout = 2000; 
                tempPort.DtrEnable = true; 

                try { tempPort.Open(); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ArduinoManager] 포트 열기 실패 ({portName}): {e.Message}");
                    continue;
                }

                yield return new WaitForSeconds(2.5f);

                string response = string.Empty;
                try
                {
                    // [수정됨] ReadLine() 대신 ReadExisting()을 사용하여 버퍼에 쌓인 모든 로그를 한 번에 긁어옵니다.
                    if (tempPort.BytesToRead > 0) 
                    {
                        response = tempPort.ReadExisting(); 
                    }
                }
                catch (TimeoutException) { Debug.LogWarning($"[ArduinoManager] 응답 타임아웃 ({portName})"); }
                catch (Exception e) { Debug.LogWarning($"[ArduinoManager] 읽기 예외 ({portName}): {e.Message}"); }

                if (response.Contains("Left_Arduino"))
                {
                    tempPort.ReadTimeout = 10; 
                    _leftPort = tempPort;
                    Debug.Log($"[ArduinoManager] Left 아두이노 연결 성공: {portName}");
                }
                else if (response.Contains("Right_Arduino"))
                {
                    tempPort.ReadTimeout = 10;
                    _rightPort = tempPort;
                    Debug.Log($"[ArduinoManager] Right 아두이노 연결 성공: {portName}");
                }
                else
                {
                    tempPort.Close();
                }
            }

            if (!IsLeftConnected) Debug.LogWarning("[ArduinoManager] Left 아두이노를 찾지 못했습니다.");
            if (!IsRightConnected) Debug.LogWarning("[ArduinoManager] Right 아두이노를 찾지 못했습니다.");
        }

        public void SendCommandToRight(string command)
        {
            if (IsRightConnected)
            {
                try { _rightPort.WriteLine(command); }
                catch (Exception e) { Debug.LogError($"[ArduinoManager] Right 전송 오류: {e.Message}"); }
            }
        }

        public void SendCommandToLeft(string command)
        {
            if (IsLeftConnected)
            {
                try { _leftPort.WriteLine(command); }
                catch (Exception e) { Debug.LogError($"[ArduinoManager] Left 전송 오류: {e.Message}"); }
            }
        }

        public void SendCommandToBoth(string command)
        {
            SendCommandToLeft(command);
            SendCommandToRight(command);
        }

        private void OnDestroy()
        {
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