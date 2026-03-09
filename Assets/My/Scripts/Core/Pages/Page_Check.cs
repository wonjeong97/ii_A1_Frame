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
        [SerializeField] private CanvasGroup cgLightA; 
        [SerializeField] private Image imgLightA; 
        [SerializeField] private CanvasGroup cgLightB; 
        [SerializeField] private Image imgLightB; 

        private bool isLightOnA; 
        private bool isLightOnB; 
        private bool _completionStarted; 
        private float _enterTime; 

        protected override void SetupData(CheckPageData data)
        {
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            else Debug.LogWarning("[Page_Check] nicknameA가 할당되지 않았습니다.");

            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);
            else Debug.LogWarning("[Page_Check] nicknameB가 할당되지 않았습니다.");

            if (waitText) UIManager.Instance.SetText(waitText.gameObject, data.waitText);
            else Debug.LogWarning("[Page_Check] waitText가 할당되지 않았습니다.");

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

            if (cgLightA) 
            {
                cgLightA.alpha = 0f;
                cgLightA.gameObject.SetActive(false);
            }
            
            if (cgLightB) 
            {
                cgLightB.alpha = 0f;
                cgLightB.gameObject.SetActive(false);
            }
            
            if (SessionManager.Instance && GameManager.Instance)
            {
                Sprite spriteA = GameManager.Instance.GetColorSprite(SessionManager.Instance.PlayerAColor);
                if (imgLightA) imgLightA.sprite = spriteA;

                Sprite spriteB = GameManager.Instance.GetColorSprite(SessionManager.Instance.PlayerBColor);
                if (imgLightB) imgLightB.sprite = spriteB;
            }
        }

        private void Update()
        {
            if (_completionStarted) return; 

            bool inputDetected = ProcessCommonKeyboardInput();

            if (inputDetected || Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false); 
            }
            else
            {
                UpdateInactivity();
            }
        }

        protected override void OnHardwareInput(string input, bool isLeft)
        {
            if (_completionStarted) return;

            int selectedValue = 0;
            string side = isLeft ? "left" : "right";

            // 상수 사용
            if (input.Equals(GameConstants.Hardware.Input1On)) selectedValue = 1;
            else if (input.Equals(GameConstants.Hardware.Input2On)) selectedValue = 2;
            else if (input.Equals(GameConstants.Hardware.Input3On)) selectedValue = 3;
            else if (input.Equals(GameConstants.Hardware.Input4On)) selectedValue = 4;
            else if (input.Equals(GameConstants.Hardware.Input5On)) selectedValue = 5;

            if (selectedValue != 0)
            {
                ProcessInput(selectedValue, side);
            }
        }

        private void ProcessInput(int selectedValue, string side)
        {
            bool isPlayerA = side.Equals("left");

            if ((isPlayerA && !isLightOnA) || (!isPlayerA && !isLightOnB))
            {
                if (ArduinoManager.Instance)
                {
                    // 상수 사용
                    if (isPlayerA) ArduinoManager.Instance.SendCommandToLeft(GameConstants.Hardware.CmdLedAllOff);
                    else ArduinoManager.Instance.SendCommandToRight(GameConstants.Hardware.CmdLedAllOff);
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
                StartCoroutine(LightOnRoutine(cgLightA, delay));
            }
            else
            {
                if (isLightOnB) return;
                isLightOnB = true;
                StartCoroutine(LightOnRoutine(cgLightB, delay));
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

        private IEnumerator LightOnRoutine(CanvasGroup cg, float delay)
        {
            if (!cg) yield break;
            
            if (delay > 0f) yield return CoroutineData.GetWaitForSeconds(delay);

            cg.gameObject.SetActive(true);
            cg.alpha = 0f;

            float t = 0f;
            float duration = 1.0f; 

            while (t < duration)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(0f, 1f, t / duration);
                yield return null;
            }

            cg.alpha = 1f;
            
            CheckCompletion();
        }
    }
}