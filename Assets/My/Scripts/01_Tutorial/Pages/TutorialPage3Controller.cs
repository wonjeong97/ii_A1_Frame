using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core; 
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage3Data
    {
        public TextSetting nicknamePlayerA;
        public TextSetting nicknamePlayerB;
    }

    /// <summary> 튜토리얼 3페이지 컨트롤러 (플레이어 체크 및 점등 연출) </summary>
    public class TutorialPage3Controller : GamePage<TutorialPage3Data>
    {
        [Header("Page 3 UI")]
        [SerializeField] private Text nicknameA; // 플레이어 A 닉네임
        [SerializeField] private Text nicknameB; // 플레이어 B 닉네임
        [SerializeField] private Image imgBackA; // 플레이어 A 배경 (Off)
        [SerializeField] private Image imgLightA; // 플레이어 A 조명 (On)
        [SerializeField] private Image imgBackB; // 플레이어 B 배경 (Off)
        [SerializeField] private Image imgLightB; // 플레이어 B 조명 (On)

        private bool isLightOnA; // A 점등 여부
        private bool isLightOnB; // B 점등 여부
        private bool _completionStarted; // 완료 시퀀스 시작 여부

        /// <summary> 페이지 진입 (상태 초기화) </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            // 상태 리셋
            isLightOnA = false;
            isLightOnB = false;
            _completionStarted = false;
            
            // 이미지 초기화 (Back 보임, Light 숨김)
            SetImageAlpha(imgBackA, 1f);
            SetImageAlpha(imgLightA, 0f);
            SetImageAlpha(imgBackB, 1f);
            SetImageAlpha(imgLightB, 0f);
        }

        /// <summary> 데이터 설정 (닉네임 적용) </summary>
        protected override void SetupData(TutorialPage3Data data)
        {
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);
        }

        /// <summary> 입력 감지 (테스트용 숫자키) </summary>
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) ActivatePlayerCheck(true);
            if (Input.GetKeyDown(KeyCode.Alpha2)) ActivatePlayerCheck(false);
        }

        /// <summary> 플레이어 체크 활성화 (점등 및 완료 확인) </summary>
        public void ActivatePlayerCheck(bool isPlayerA)
        {
            if (isPlayerA)
            {
                if (isLightOnA) return; // 이미 켜졌으면 무시
                isLightOnA = true;
                StartCoroutine(TransitionCheckImage(imgBackA, imgLightA));
            }
            else
            {
                if (isLightOnB) return;
                isLightOnB = true;
                StartCoroutine(TransitionCheckImage(imgBackB, imgLightB));
            }
            
            // 둘 다 켜졌는지 확인
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
            yield return new WaitForSeconds(1.0f);
            CompleteStep(); 
        }

        /// <summary> 이미지 교차 페이드 연출 (꺼짐 -> 켜짐) </summary>
        private IEnumerator TransitionCheckImage(Image backImage, Image lightImage)
        {
            if (backImage == null || lightImage == null) yield break;

            float timer = 0f;
            float duration = 0.3f;
            
            Color backColor = backImage.color;
            Color lightColor = lightImage.color;
            
            // Light 이미지 준비
            lightColor.a = 0f;
            lightImage.color = lightColor;
            lightImage.gameObject.SetActive(true);

            // Cross Fade
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float progress = timer / duration;
                
                // Back은 서서히 투명, Light는 서서히 불투명
                backColor.a = Mathf.Lerp(1f, 0f, progress);
                backImage.color = backColor;
                lightColor.a = Mathf.Lerp(0f, 1f, progress);
                lightImage.color = lightColor;
                
                yield return null;
            }
            
            // 최종 값 보정
            backColor.a = 0f;
            backImage.color = backColor;
            lightColor.a = 1f;
            lightImage.color = lightColor;
        }

        /// <summary> 이미지 투명도 설정 </summary>
        private void SetImageAlpha(Image img, float alpha)
        {
            if (img == null) return;
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }
}