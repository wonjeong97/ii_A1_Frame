using System;
using System.Collections;
using System.IO.Ports;
using UnityEngine;
using My.Scripts.Global;
using Wonjeong.Utils;

namespace My.Scripts.Hardware
{
    /// <summary>
    /// 장치 관리자에 연결된 COM 포트를 스캔하여 좌/우 아두이노를 식별하고 제어 명령을 송수신하는 매니저 클래스.
    /// </summary>
    public class ArduinoManager : MonoBehaviour
    {
        public static ArduinoManager Instance;

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
            if (IsLeftConnected && _leftPort.BytesToRead > 0)
            {
                try
                {
                    string leftInput = _leftPort.ReadLine().Trim();
                    if (!string.IsNullOrEmpty(leftInput)) ProcessHardwareInput(leftInput, true);
                }
                catch (TimeoutException) { }
                catch (Exception e) { Debug.LogWarning($"[ArduinoManager] Left 수신 예외: {e.Message}"); }
            }

            if (IsRightConnected && _rightPort.BytesToRead > 0)
            {
                try
                {
                    string rightInput = _rightPort.ReadLine().Trim();
                    if (!string.IsNullOrEmpty(rightInput)) ProcessHardwareInput(rightInput, false);
                }
                catch (TimeoutException) { }
                catch (Exception e) { Debug.LogWarning($"[ArduinoManager] Right 수신 예외: {e.Message}"); }
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

                yield return CoroutineData.GetWaitForSeconds(2.5f);

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

                // 상수 사용
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