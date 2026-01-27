using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Data;
using Wonjeong.UI;

namespace My.Scripts.Core.Pages
{
    /// <summary> 플레이어 준비 확인 및 점등 연출 페이지 </summary>
    public class Page_Check : GamePage<CheckPageData>
    {
        [Header("UI References")] 
        [SerializeField] private Text nicknameA; // 플레이어 A 닉네임
        [SerializeField] private Text nicknameB; // 플레이어 B 닉네임

        [Header("Check Images")] 
        [SerializeField] private Image imgBackA; // 플레이어 A 배경 (Off)
        [SerializeField] private Image imgLightA; // 플레이어 A 조명 (On)
        [SerializeField] private Image imgBackB; // 플레이어 B 배경 (Off)
        [SerializeField] private Image imgLightB; // 플레이어 B 조명 (On)

        private bool isLightOnA; // A 점등 여부
        private bool isLightOnB; // B 점등 여부
        private bool _completionStarted; // 완료 시퀀스 시작 여부

        /// <summary> 데이터 설정 (닉네임 적용) </summary>
        protected override void SetupData(CheckPageData data)
        {
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);
        }

        /// <summary> 페이지 진입 (상태 초기화) </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            isLightOnA = false;
            isLightOnB = false;
            _completionStarted = false;

            // 초기 상태: 불 꺼짐(Back 1, Light 0)
            SetImgAlpha(imgBackA, 1f);
            SetImgAlpha(imgLightA, 0f);
            if (imgLightA) imgLightA.gameObject.SetActive(false);
            
            SetImgAlpha(imgBackB, 1f);
            SetImgAlpha(imgLightB, 0f);
            if (imgLightB) imgLightB.gameObject.SetActive(false);
        }
        
        /// <summary> 입력 감지 (숫자키) </summary>
        private void Update()
        {
            if (_completionStarted) return;

            // Player A (1~5) 입력 체크
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha2) || 
                Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Alpha4) || 
                Input.GetKeyDown(KeyCode.Alpha5))
            {
                ActivatePlayerCheck(true);
            }

            // Player B (6~0) 입력 체크
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Alpha7) || 
                Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Alpha9) || 
                Input.GetKeyDown(KeyCode.Alpha0))
            {
                ActivatePlayerCheck(false);
            }
        }

        /// <summary> 플레이어 체크 활성화 (점등 및 완료 확인) </summary>
        public void ActivatePlayerCheck(bool isPlayerA)
        {
            if (isPlayerA)
            {
                if (isLightOnA) return;
                isLightOnA = true;
                StartCoroutine(LightOnRoutine(imgBackA, imgLightA));
            }
            else
            {
                if (isLightOnB) return;
                isLightOnB = true;
                StartCoroutine(LightOnRoutine(imgBackB, imgLightB));
            }

            CheckCompletion();
        }

        /// <summary> 양쪽 완료 확인 </summary>
        private void CheckCompletion()
        {
            if (isLightOnA && isLightOnB && !_completionStarted)
            {
                _completionStarted = true;
                StartCoroutine(CompleteRoutine());
            }
        }

        /// <summary> 대기 후 단계 완료 처리 </summary>
        private IEnumerator CompleteRoutine()
        {
            yield return new WaitForSeconds(1.0f); // 1초 대기 후 완료
            CompleteStep();
        }

        /// <summary> 점등 연출 (Cross Fade) </summary>
        private IEnumerator LightOnRoutine(Image back, Image light)
        {
            if (!back || !light) yield break;
            light.gameObject.SetActive(true);

            float t = 0f, d = 0.3f;
            Color cb = back.color, cl = light.color;

            while (t < d)
            {
                t += Time.deltaTime;
                float p = t / d;
                cb.a = Mathf.Lerp(1f, 0f, p);
                back.color = cb;
                cl.a = Mathf.Lerp(0f, 1f, p);
                light.color = cl;
                yield return null;
            }

            cb.a = 0f;
            back.color = cb;
            cl.a = 1f;
            light.color = cl;
        }

        /// <summary> 이미지 투명도 설정 </summary>
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