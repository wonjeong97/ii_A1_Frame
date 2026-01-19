using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._03_Play_Q1.Pages
{
    [Serializable]
    public class PlayQ1Page1Data
    {
        public TextSetting descriptionText;
        public TextSetting playerAName;
        public TextSetting playerBName;
    }

    public class PlayQ1Page1Controller : PlayQ1PageBase
    {
        [Header("Page 1 UI")]
        [SerializeField] private Text txtDescription;
        [SerializeField] private Text txtPlayerA;
        [SerializeField] private Text txtPlayerB;

        [Header("Canvas Groups (For Animation)")]
        [SerializeField] private CanvasGroup cvsgDescription;
        [SerializeField] private CanvasGroup cvsgPlayerNames;

        public override void SetupData(object data)
        {
            var pageData = data as PlayQ1Page1Data;
            if (pageData == null) return;

            if (txtDescription) UIManager.Instance.SetText(txtDescription.gameObject, pageData.descriptionText);
            if (txtPlayerA) UIManager.Instance.SetText(txtPlayerA.gameObject, pageData.playerAName);
            if (txtPlayerB) UIManager.Instance.SetText(txtPlayerB.gameObject, pageData.playerBName);
        }

        public override void OnEnter()
        {
            base.OnEnter();

            if (cvsgDescription) cvsgDescription.alpha = 0f;
            if (cvsgPlayerNames) cvsgPlayerNames.alpha = 0f;

            StartCoroutine(IntroSequence());
        }

        private IEnumerator IntroSequence()
        {
            // 1. 설명 텍스트 페이드 인 (1초)
            yield return StartCoroutine(FadeCanvasGroup(cvsgDescription, 0f, 1f, 1.0f));

            // 2. 플레이어 이름 페이드 인 (0.5초)
            yield return StartCoroutine(FadeCanvasGroup(cvsgPlayerNames, 0f, 1f, 0.5f));

            // 3. 4초 대기 (요청 사항)
            yield return new WaitForSeconds(4.0f);

            // 4. 완료 -> 매니저에게 신호 전송 (매니저가 FadeManager 호출)
            CompleteStep();
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end, float duration)
        {
            if (cg == null) yield break;
            float timer = 0f;
            cg.alpha = start;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                cg.alpha = Mathf.Lerp(start, end, timer / duration);
                yield return null;
            }
            cg.alpha = end;
        }
    }
}