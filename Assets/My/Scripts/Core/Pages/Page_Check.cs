using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using My.Scripts.Hardware;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary> 플레이어 준비 확인 및 점등 연출 페이지 </summary>
    public class Page_Check : PopupGamePage<CheckPageData>
    {
        [Header("UI References")] 
        [SerializeField] private Text nicknameA; 
        [SerializeField] private Text nicknameB; 
        [SerializeField] private Text waitText;  

        [Header("Check Images")] 
        [SerializeField] private Image imgBackA; 
        [SerializeField] private Image imgLightA; 
        [SerializeField] private Image imgBackB; 
        [SerializeField] private Image imgLightB; 

        private bool isLightOnA; 
        private bool isLightOnB; 
        private bool _completionStarted; 
        private float _enterTime; 

        protected override void SetupData(CheckPageData data)
        {
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);
            if (waitText) UIManager.Instance.SetText(waitText.gameObject, data.waitText);

            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            StopAllCoroutines();
            
            isLightOnA = false;
            isLightOnB = false;
            _completionStarted = false;
            _enterTime = Time.time; 

            ResetIdleState(true);

            SetImgAlpha(imgBackA, 1f);
            SetImgAlpha(imgLightA, 0f);
            if (imgLightA) imgLightA.gameObject.SetActive(false);
            
            SetImgAlpha(imgBackB, 1f);
            SetImgAlpha(imgLightB, 0f);
            if (imgLightB) imgLightB.gameObject.SetActive(false);
            
            if (GameManager.Instance)
            {
                Sprite spriteA = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerAColor);
                if (imgLightA) imgLightA.sprite = spriteA;

                Sprite spriteB = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerBColor);
                if (imgLightB) imgLightB.sprite = spriteB;
            }

            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.OnHardwareInput -= HandleArduinoInput;
                ArduinoManager.Instance.OnHardwareInput += HandleArduinoInput;
            }
        }

        public override void OnExit()
        {
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.OnHardwareInput -= HandleArduinoInput;
            }
            base.OnExit();
        }

        private void OnDestroy()
        {
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.OnHardwareInput -= HandleArduinoInput;
            }
        }

        private void Update()
        {
            if (_completionStarted) return; 

            bool inputDetected = false;
            int selectedValue = 0;
            string side = string.Empty;

            // Left 디버그 (Q W E R T)
            if (Input.GetKeyDown(KeyCode.Q)) { selectedValue = 1; side = "left"; }
            else if (Input.GetKeyDown(KeyCode.W)) { selectedValue = 2; side = "left"; }
            else if (Input.GetKeyDown(KeyCode.E)) { selectedValue = 3; side = "left"; }
            else if (Input.GetKeyDown(KeyCode.R)) { selectedValue = 4; side = "left"; }
            else if (Input.GetKeyDown(KeyCode.T)) { selectedValue = 5; side = "left"; }
            // Right 디버그 (Y U I O P)
            else if (Input.GetKeyDown(KeyCode.Y)) { selectedValue = 1; side = "right"; }
            else if (Input.GetKeyDown(KeyCode.U)) { selectedValue = 2; side = "right"; }
            else if (Input.GetKeyDown(KeyCode.I)) { selectedValue = 3; side = "right"; }
            else if (Input.GetKeyDown(KeyCode.O)) { selectedValue = 4; side = "right"; }
            else if (Input.GetKeyDown(KeyCode.P)) { selectedValue = 5; side = "right"; }

            if (selectedValue != 0)
            {
                inputDetected = true;
                ProcessInput(selectedValue, side);
            }

            if (inputDetected || Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false); 
            }
            else
            {
                UpdateInactivity();
            }
        }

        private void HandleArduinoInput(string input, bool isLeft)
        {
            if (_completionStarted) return;

            int selectedValue = 0;
            string side = isLeft ? "left" : "right";

            if (input == "1On") selectedValue = 1;
            else if (input == "2On") selectedValue = 2;
            else if (input == "3On") selectedValue = 3;
            else if (input == "4On") selectedValue = 4;
            else if (input == "5On") selectedValue = 5;

            if (selectedValue != 0)
            {
                ProcessInput(selectedValue, side);
            }
        }

        private void ProcessInput(int selectedValue, string side)
        {
            bool isPlayerA = (side == "left");

            // 중복 입력(이미 불이 켜진 상태)이 아닐 때만 API 전송 및 LED 제어
            if ((isPlayerA && !isLightOnA) || (!isPlayerA && !isLightOnB))
            {
                // [수정] 자신이 누른 쪽의 아두이노 LED만 개별적으로 끕니다.
                if (ArduinoManager.Instance)
                {
                    if (isPlayerA) ArduinoManager.Instance.SendCommandToLeft("LEDAllOff");
                    else ArduinoManager.Instance.SendCommandToRight("LEDAllOff");
                }

                if (GameManager.Instance && LevelManager.Instance)
                {
                    int qNo = LevelManager.Instance.CurrentQuestionNumber;
                    if (qNo > 0)
                    {
                        GameManager.Instance.SendValueUpdateAPI(qNo, side, selectedValue);
                    }
                }
            }

            ActivatePlayerCheck(isPlayerA);
        }

        public void ActivatePlayerCheck(bool isPlayerA)
        {
            ResetIdleState(false);

            float delay = Mathf.Max(0f, 0.5f - (Time.time - _enterTime));

            if (isPlayerA)
            {
                if (isLightOnA) return;
                isLightOnA = true;
                StartCoroutine(LightOnRoutine(imgBackA, imgLightA, delay));
            }
            else
            {
                if (isLightOnB) return;
                isLightOnB = true;
                StartCoroutine(LightOnRoutine(imgBackB, imgLightB, delay));
            }
        }

        private void CheckCompletion()
        {   
            if (isLightOnA && isLightOnB && !_completionStarted)
            {
                _completionStarted = true;
                StartCoroutine(CompleteRoutine());
            }
        }

        private IEnumerator CompleteRoutine()
        {   
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_22");
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            CompleteStep();
        }

        private IEnumerator LightOnRoutine(Image back, Image light, float delay)
        {
            if (!back || !light) yield break;
            
            if (delay > 0f) yield return CoroutineData.GetWaitForSeconds(delay);

            light.gameObject.SetActive(true);
            Color cl = light.color;
            cl.a = 0f;
            light.color = cl;

            float t = 0f;
            float duration = 1.0f; 

            while (t < duration)
            {
                t += Time.deltaTime;
                float p = t / duration;
                
                cl.a = Mathf.Lerp(0f, 1f, p);
                light.color = cl;
                
                yield return null;
            }

            cl.a = 1f;
            light.color = cl;
            
            CheckCompletion();
        }

        private void SetImgAlpha(Image i, float a)
        {
            if (i)
            {
                Color c = i.color;
                c.a = a;
                i.color = c;
            }
        }
    }
}