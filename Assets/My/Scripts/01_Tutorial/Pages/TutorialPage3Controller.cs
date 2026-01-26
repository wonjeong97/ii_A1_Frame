using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core; // [필수] GamePage 사용을 위해 추가
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    // 데이터 클래스는 그대로 유지
    [Serializable]
    public class TutorialPage3Data
    {
        public TextSetting nicknamePlayerA;
        public TextSetting nicknamePlayerB;
    }

    // TutorialPageBase -> GamePage<TutorialPage3Data> 상속 변경
    public class TutorialPage3Controller : GamePage<TutorialPage3Data>
    {
        [Header("Page 3 UI")]
        [SerializeField] private Text nicknameA;
        [SerializeField] private Text nicknameB;
        [SerializeField] private Image imgBackA;
        [SerializeField] private Image imgLightA;
        [SerializeField] private Image imgBackB;
        [SerializeField] private Image imgLightB;

        private bool isLightOnA;
        private bool isLightOnB;
        private bool _completionStarted;

        public override void OnEnter()
        {
            base.OnEnter();
            
            isLightOnA = false;
            isLightOnB = false;
            _completionStarted = false;
            SetImageAlpha(imgBackA, 1f);
            SetImageAlpha(imgLightA, 0f);
            SetImageAlpha(imgBackB, 1f);
            SetImageAlpha(imgLightB, 0f);
        }

        // 제네릭 SetupData 구현
        protected override void SetupData(TutorialPage3Data data)
        {
            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, data.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, data.nicknamePlayerB);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) ActivatePlayerCheck(true);
            if (Input.GetKeyDown(KeyCode.Alpha2)) ActivatePlayerCheck(false);
        }

        public void ActivatePlayerCheck(bool isPlayerA)
        {
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
            
            if (isLightOnA && isLightOnB)
            {
                if (!_completionStarted)
                {
                    _completionStarted = true;
                    StartCoroutine(WaitAndComplete());
                }
            }
        }

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