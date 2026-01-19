using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._02_Play_Tutorial.Pages
{
    [Serializable]
    public class PlayTutorialPage3Data
    {
        public TextSetting nicknamePlayerA;
        public TextSetting nicknamePlayerB;
    }

    public class PlayTutorialPage3Controller : PlayTutorialPageBase
    {
        [Header("Page 3 UI")]
        [SerializeField] private Text nicknameA;
        [SerializeField] private Text nicknameB;
        
        [Header("Check Images")]
        [SerializeField] private Image imgBackA;   
        [SerializeField] private Image imgLightA;  
        [SerializeField] private Image imgBackB;   
        [SerializeField] private Image imgLightB;  

        private bool isLightOnA;
        private bool isLightOnB;
        private bool _completionStarted;

        public override void SetupData(object data)
        {
            var pageData = data as PlayTutorialPage3Data;
            if (pageData == null) return;

            if (nicknameA) UIManager.Instance.SetText(nicknameA.gameObject, pageData.nicknamePlayerA);
            if (nicknameB) UIManager.Instance.SetText(nicknameB.gameObject, pageData.nicknamePlayerB);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            isLightOnA = false;
            isLightOnB = false;
            _completionStarted = false;

            SetImageAlpha(imgBackA, 1f);
            SetImageAlpha(imgLightA, 0f);
            if (imgLightA) imgLightA.gameObject.SetActive(false);

            SetImageAlpha(imgBackB, 1f);
            SetImageAlpha(imgLightB, 0f);
            if (imgLightB) imgLightB.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_completionStarted) return;

            // [임시] A(1~5), B(6~0) 입력 체크
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha2) || 
                Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Alpha4) || 
                Input.GetKeyDown(KeyCode.Alpha5))
            {
                ActivatePlayerCheck(true);
            }

            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Alpha7) || 
                Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Alpha9) || 
                Input.GetKeyDown(KeyCode.Alpha0))
            {
                ActivatePlayerCheck(false);
            }
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

            lightImage.gameObject.SetActive(true);

            float timer = 0f;
            float duration = 0.3f;
            
            Color backColor = backImage.color;
            Color lightColor = lightImage.color;
            
            backColor.a = 1f;
            lightColor.a = 0f;
            backImage.color = backColor;
            lightImage.color = lightColor;

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