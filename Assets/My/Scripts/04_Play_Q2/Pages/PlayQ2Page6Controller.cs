using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._04_Play_Q2.Pages
{
    [Serializable]
    public class PlayQ2Page6Data
    {
        public TextSetting descriptionText;
    }

    public class PlayQ2Page6Controller : PlayQ2PageBase
    {
        [Header("Page 6 UI")]
        [SerializeField] private Text descriptionText;

        public override void SetupData(object data)
        {
            var pageData = data as PlayQ2Page6Data;
            if (pageData == null) return;

            if (descriptionText) 
            {
                UIManager.Instance.SetText(descriptionText.gameObject, pageData.descriptionText);
                Color c = descriptionText.color; c.a = 1f; descriptionText.color = c;
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            StartCoroutine(EndSequence());
        }

        private IEnumerator EndSequence()
        {
            // PlayQ2Page5에서 페이드아웃 상태로 넘어옴 -> 대기
            yield return new WaitForSeconds(1.0f);
            yield return new WaitForSeconds(2.0f); // 멘트 읽을 시간

            if (descriptionText != null)
            {
                float t = 0f, d = 0.5f;
                while (t < d) { t += Time.deltaTime; Color c = descriptionText.color; c.a = Mathf.Lerp(1f, 0f, t/d); descriptionText.color = c; yield return null; }
                Color f = descriptionText.color; f.a = 0f; descriptionText.color = f;
            }

            CompleteStep();
        }
    }
}