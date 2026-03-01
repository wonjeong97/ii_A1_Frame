using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary> 플레이어 준비 확인 및 점등 연출 페이지 </summary>
    public class Page_Check : PopupGamePage<CheckPageData>
    {
        [Header("UI References")] 
        [SerializeField] private Text nicknameA; // 플레이어 A 닉네임
        [SerializeField] private Text nicknameB; // 플레이어 B 닉네임
        [SerializeField] private Text waitText;  // 대기 텍스트

        [Header("Check Images")] 
        [SerializeField] private Image imgBackA; // 플레이어 A 배경
        [SerializeField] private Image imgLightA; // 플레이어 A 조명 (V 표시)
        [SerializeField] private Image imgBackB; // 플레이어 B 배경
        [SerializeField] private Image imgLightB; // 플레이어 B 조명

        private bool isLightOnA; // A 점등 여부
        private bool isLightOnB; // B 점등 여부
        private bool _completionStarted; // 완료 시퀀스 시작 여부
        private float _enterTime; // 페이지 진입 시간 기록용

        /// <summary> 데이터 설정 </summary>
        protected override void SetupData(CheckPageData data)
        {
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);
            if (waitText) UIManager.Instance.SetText(waitText.gameObject, data.waitText);

            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        /// <summary>  페이지 진입 </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            StopAllCoroutines();
            
            isLightOnA = false;
            isLightOnB = false;
            _completionStarted = false;
            _enterTime = Time.time; // 진입 시간 기록

            ResetIdleState(true);

            // 이미지 초기화
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
        }
        
        /// <summary>  매 프레임 업데이트 </summary>
      private void Update()
        {
            if (_completionStarted) return; 

            bool inputDetected = false;
            int selectedValue = 0;
            string side = "";

            // Left (1~5) -> Value (1~5)
            if (Input.GetKeyDown(KeyCode.Alpha1)) { selectedValue = 1; side = "left"; }
            else if (Input.GetKeyDown(KeyCode.Alpha2)) { selectedValue = 2; side = "left"; }
            else if (Input.GetKeyDown(KeyCode.Alpha3)) { selectedValue = 3; side = "left"; }
            else if (Input.GetKeyDown(KeyCode.Alpha4)) { selectedValue = 4; side = "left"; }
            else if (Input.GetKeyDown(KeyCode.Alpha5)) { selectedValue = 5; side = "left"; }
            
            // Right (6~0) -> Value (1~5)
            else if (Input.GetKeyDown(KeyCode.Alpha6)) { selectedValue = 1; side = "right"; }
            else if (Input.GetKeyDown(KeyCode.Alpha7)) { selectedValue = 2; side = "right"; }
            else if (Input.GetKeyDown(KeyCode.Alpha8)) { selectedValue = 3; side = "right"; }
            else if (Input.GetKeyDown(KeyCode.Alpha9)) { selectedValue = 4; side = "right"; }
            else if (Input.GetKeyDown(KeyCode.Alpha0)) { selectedValue = 5; side = "right"; }

            if (selectedValue != 0)
            {
                inputDetected = true;
                bool isPlayerA = (side == "left");

                // 중복 입력(이미 불이 켜진 상태)이 아닐 때만 API 전송
                if ((isPlayerA && !isLightOnA) || (!isPlayerA && !isLightOnB))
                {
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

            if (inputDetected || Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false); 
            }
            else
            {
                UpdateInactivity();
            }
        }

        /// <summary>  플레이어 체크 활성화 (지연 로직 적용) </summary>
        public void ActivatePlayerCheck(bool isPlayerA)
        {
            ResetIdleState(false);

            // 페이지 진입 후 최소 0.5초가 지나야 불이 켜지도록 딜레이 계산
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
            // 양쪽 모두 켜졌고, 완료 시퀀스가 아직 시작되지 않았다면 진행
            if (isLightOnA && isLightOnB && !_completionStarted)
            {
                _completionStarted = true;
                StartCoroutine(CompleteRoutine());
            }
        }

        private IEnumerator CompleteRoutine()
        {   
            SoundManager.Instance?.PlaySFX("공통_22");
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            CompleteStep();
        }

        /// <summary> 
        /// 점등 연출 (Delay + Fade In)
        /// </summary>
        private IEnumerator LightOnRoutine(Image back, Image light, float delay)
        {
            if (!back || !light) yield break;
            
            // 계산된 시간만큼 대기 (페이지 전환 직후 즉시 점등 방지)
            if (delay > 0f) yield return CoroutineData.GetWaitForSeconds(delay);

            light.gameObject.SetActive(true);
            Color cl = light.color;
            cl.a = 0f;
            light.color = cl;

            float t = 0f;
            float duration = 1.0f; // 1초 페이드 인

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
            
            // 페이드 연출이 완전히 끝난 후 완료 조건을 체크
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