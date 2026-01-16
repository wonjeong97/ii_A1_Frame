using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    // ---------------------------------------------------------
    // 데이터 클래스
    // ---------------------------------------------------------
    [Serializable]
    public class TutorialPage3Data
    {
        public TextSetting nicknamePlayerA;
        public TextSetting nicknamePlayerB;
    }

    // ---------------------------------------------------------
    // 컨트롤러 클래스
    // ---------------------------------------------------------
    public class TutorialPage3Controller : TutorialPageBase
    {
        [Header("Page 3 UI")]
        [SerializeField] private Text nicknameA;
        [SerializeField] private Text nicknameB;
        [SerializeField] private Image imgBackA;
        [SerializeField] private Image imgLightA;
        [SerializeField] private Image imgBackB;
        [SerializeField] private Image imgLightB;

        // 상태 변수
        private bool isLightOnA;
        private bool isLightOnB;
        private bool _completionStarted;

        // 페이지 진입 시 초기화
        public override void OnEnter()
        {
            base.OnEnter();
            
            // 상태 및 이미지 초기화
            isLightOnA = false;
            isLightOnB = false;
            _completionStarted = false;
            SetImageAlpha(imgBackA, 1f);
            SetImageAlpha(imgLightA, 0f);
            SetImageAlpha(imgBackB, 1f);
            SetImageAlpha(imgLightB, 0f);
        }

        public override void SetupData(object data)
        {
            var pageData = data as TutorialPage3Data;
            if (pageData == null) return;

            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, pageData.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, pageData.nicknamePlayerB);
        }

        private void Update()
        {
            // 각 플레이어 상호작용
            if (Input.GetKeyDown(KeyCode.Alpha1)) ActivatePlayerCheck(true);
            if (Input.GetKeyDown(KeyCode.Alpha2)) ActivatePlayerCheck(false);
        }

        // 외부(매니저)에서 트리거 정보를 받아 강제로 켤 때 사용 (Page 2->3 전환 시)
        public void ActivatePlayerCheck(bool isPlayerA)
        {
            if (isPlayerA)
            {
                if (isLightOnA) return; // 이미 켜짐
                isLightOnA = true;
                StartCoroutine(TransitionCheckImage(imgBackA, imgLightA));
            }
            else
            {
                if (isLightOnB) return; // 이미 켜짐
                isLightOnB = true;
                StartCoroutine(TransitionCheckImage(imgBackB, imgLightB));
            }
            
            // 두 불이 다 켜졌는지 확인
            if (isLightOnA && isLightOnB)
            {
                if (!_completionStarted)
                {
                    _completionStarted = true;
                    StartCoroutine(WaitAndComplete());
                }
            }
        }

        // 1초 대기 후 완료 신호 보냄
        private IEnumerator WaitAndComplete()
        {
            yield return new WaitForSeconds(1.0f);
            CompleteStep(); 
        }

        private IEnumerator TransitionCheckImage(Image backImage, Image lightImage)
        {
            if (backImage == null || lightImage == null) yield break;

            float timer = 0f;
            float duration = 0.3f;
            
            Color backColor = backImage.color;
            Color lightColor = lightImage.color;
            
            lightColor.a = 0f;
            lightImage.color = lightColor;
            lightImage.gameObject.SetActive(true);

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float progress = timer / duration;
                backColor.a = Mathf.Lerp(1f, 0f, progress);
                backImage.color = backColor;
                lightColor.a = Mathf.Lerp(0f, 1f, progress);
                lightImage.color = lightColor;
                yield return null;
            }
            
            // 최종값
            backColor.a = 0f;
            backImage.color = backColor;
            lightColor.a = 1f;
            lightImage.color = lightColor;
        }

        private void SetImageAlpha(Image img, float alpha)
        {
            if (img == null) return;
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }
}