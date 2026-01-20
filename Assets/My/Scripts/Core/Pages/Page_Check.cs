using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Data;
using Wonjeong.UI;

namespace My.Scripts.Core.Pages
{
    public class Page_Check : GamePage
    {
        [Header("UI References")] [SerializeField]
        private Text nicknameA;

        [SerializeField] private Text nicknameB;

        [Header("Check Images")] [SerializeField]
        private Image imgBackA;

        [SerializeField] private Image imgLightA;
        [SerializeField] private Image imgBackB;
        [SerializeField] private Image imgLightB;

        private bool isLightOnA, isLightOnB;
        private bool _completionStarted;

        public override void SetupData(object data)
        {
            var pageData = data as CheckPageData;
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

            // 초기 상태: 불 꺼짐(Back 1, Light 0)
            SetImgAlpha(imgBackA, 1f);
            SetImgAlpha(imgLightA, 0f);
            if (imgLightA) imgLightA.gameObject.SetActive(false);
            
            SetImgAlpha(imgBackB, 1f);
            SetImgAlpha(imgLightB, 0f);
            if (imgLightB) imgLightB.gameObject.SetActive(false);
        }
        
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

        // 매니저에서 호출 (이전 페이지에서 누가 눌렀는지, 혹은 현재 페이지에서 누가 눌렀는지)
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
            yield return new WaitForSeconds(1.0f); // 1초 대기 후 완료
            CompleteStep();
        }

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