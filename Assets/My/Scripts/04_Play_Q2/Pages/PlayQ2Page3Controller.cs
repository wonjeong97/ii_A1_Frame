using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._04_Play_Q2.Pages
{
    [Serializable]
    public class PlayQ2Page3Data
    {
        public TextSetting nicknamePlayerA;
        public TextSetting nicknamePlayerB;
    }

    public class PlayQ2Page3Controller : PlayQ2PageBase
    {
        [Header("Page 3 UI")] [SerializeField] private Text nicknameA;
        [SerializeField] private Text nicknameB;
        [SerializeField] private Image imgBackA, imgLightA, imgBackB, imgLightB;

        private bool isLightOnA, isLightOnB, _completionStarted;

        public override void SetupData(object data)
        {
            var pageData = data as PlayQ2Page3Data;
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

            if (isLightOnA && isLightOnB && !_completionStarted)
            {
                _completionStarted = true;
                StartCoroutine(WaitAndComplete());
            }
        }

        private IEnumerator WaitAndComplete()
        {
            yield return new WaitForSeconds(1.0f);
            CompleteStep();
        }

        private IEnumerator TransitionCheckImage(Image backImage, Image lightImage)
        {
            if (!backImage || !lightImage) yield break;
            lightImage.gameObject.SetActive(true);
            float timer = 0f, duration = 0.3f;
            Color bc = backImage.color, lc = lightImage.color;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float p = timer / duration;
                bc.a = Mathf.Lerp(1f, 0f, p);
                backImage.color = bc;
                lc.a = Mathf.Lerp(0f, 1f, p);
                lightImage.color = lc;
                yield return null;
            }

            bc.a = 0f;
            backImage.color = bc;
            lc.a = 1f;
            lightImage.color = lc;
        }

        private void SetImageAlpha(Image img, float a)
        {
            if (img)
            {
                Color c = img.color;
                c.a = a;
                img.color = c;
            }
        }
    }
}