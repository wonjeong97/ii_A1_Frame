using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Pages;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary> 튜토리얼 4페이지 데이터 클래스 </summary>
    [Serializable]
    public class TutorialPage4Data
    {
        public TextSetting nicknamePlayerA; // 플레이어 A 닉네임 데이터
        public TextSetting nicknamePlayerB; // 플레이어 B 닉네임 데이터
        
        public string warningMessage; // 1차 경고 메시지
        public string resetMessage;   // 2차 초기화 메시지
    }

    /// <summary> 튜토리얼 4페이지 컨트롤러 </summary>
    public class TutorialPage4Controller : PopupGamePage<TutorialPage4Data>
    {
        [Header("Page 4 UI")]
        [SerializeField] private Text nicknameA; // 플레이어 A 닉네임 UI
        [SerializeField] private Text nicknameB; // 플레이어 B 닉네임 UI
        [SerializeField] private Image imgBackA; // 플레이어 A 배경 이미지 (Off)
        [SerializeField] private Image imgLightA; // 플레이어 A 조명 이미지 (On)
        [SerializeField] private Image imgBackB; // 플레이어 B 배경 이미지 (Off)
        [SerializeField] private Image imgLightB; // 플레이어 B 조명 이미지 (On)

        private bool isLightOnA; // 플레이어 A 점등 여부
        private bool isLightOnB; // 플레이어 B 점등 여부
        private bool _completionStarted; // 완료 시퀀스 진행 여부

        /// <summary>  데이터 설정: 닉네임 적용 및 팝업 메시지 설정 </summary>
        protected override void SetupData(TutorialPage4Data data)
        {
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);

            // 팝업 메시지 설정
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        /// <summary> 페이지 진입: 상태 초기화 </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            // 로직 상태 리셋
            isLightOnA = false;
            isLightOnB = false;
            _completionStarted = false;
            
            // 팝업 즉시 끄기 및 타이머 초기화
            ResetIdleState(true); 
            
            // 이미지 초기화 (Back 보임, Light 숨김)
            SetImageAlpha(imgBackA, 1f);
            SetImageAlpha(imgLightA, 0f);
            SetImageAlpha(imgBackB, 1f);
            SetImageAlpha(imgLightB, 0f);
        }

        /// <summary> 매 프레임 업데이트: 입력 감지 및 비활성 체크 </summary>
        private void Update()
        {
            if (_completionStarted) return; // 완료 시퀀스 중이면 로직 중단

            // 1. 입력 감지
            if (Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false); // 부드럽게 리셋 취소

                // 테스트용 키 입력 (1, 2번 키)
                if (Input.GetKeyDown(KeyCode.Alpha1)) ActivatePlayerCheck(true);
                if (Input.GetKeyDown(KeyCode.Alpha2)) ActivatePlayerCheck(false);
            }
            else
            {
                // 2. 비활성 시간 누적
                UpdateInactivity();
            }
        }

        /// <summary> 플레이어 체크 활성화 (점등 연출). </summary>
        public void ActivatePlayerCheck(bool isPlayerA)
        {
            // 외부 호출 시에도 리셋 로직 취소 (활동 감지)
            ResetIdleState(false);

            if (isPlayerA)
            {
                if (isLightOnA) return; 
                isLightOnA = true;
                StartCoroutine(TransitionCheckImage(imgBackA, imgLightA));
            }
            else
            {
                if (isLightOnB) return;
                isLightOnB = true;
                StartCoroutine(TransitionCheckImage(imgBackB, imgLightB));
            }
            
            // 양쪽 모두 켜졌는지 확인
            if (isLightOnA && isLightOnB)
            {
                if (!_completionStarted)
                {
                    _completionStarted = true;
                    StartCoroutine(WaitAndComplete());
                }
            }
        }

        /// <summary> 대기 후 단계 완료 처리 </summary>
        private IEnumerator WaitAndComplete()
        {
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            CompleteStep(); 
        }

        /// <summary> 이미지 교차 페이드(Cross Fade) 연출 </summary>
        private IEnumerator TransitionCheckImage(Image backImage, Image lightImage)
        {
            if (backImage == null || lightImage == null) yield break;

            float timer = 0f;
            float duration = 0.1f;
            
            Color backColor = backImage.color;
            Color lightColor = lightImage.color;
            
            lightColor.a = 0f;
            lightImage.color = lightColor;
            lightImage.gameObject.SetActive(true);

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float progress = timer / duration;
                
                // Back은 투명해지고, Light는 불투명해짐
                backColor.a = Mathf.Lerp(1f, 0f, progress);
                backImage.color = backColor;
                lightColor.a = Mathf.Lerp(0f, 1f, progress);
                lightImage.color = lightColor;
                
                yield return null;
            }
            
            // 최종값 보정
            backColor.a = 0f;
            backImage.color = backColor;
            lightColor.a = 1f;
            lightImage.color = lightColor;
        }

        /// <summary>  이미지 투명도 즉시 설정 </summary>
        private void SetImageAlpha(Image img, float alpha)
        {
            if (img == null) return;
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }
}