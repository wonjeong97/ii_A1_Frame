using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._18_Ending.Pages
{
    [Serializable]
    public class EndingPage2Data
    {
        public TextSetting descriptionText1;
        public TextSetting descriptionText2;
    }

    public class EndingPage2Controller : GamePage
    {
        [Header("UI References")]
        [SerializeField] private Text text1;
        [SerializeField] private Text text2;
        [SerializeField] private CanvasGroup imageCanvasGroup;

        public override void SetupData(object data)
        {
            var pageData = data as EndingPage2Data;
            if (pageData == null) return;

            // JSON 데이터 적용
            if (text1) UIManager.Instance.SetText(text1.gameObject, pageData.descriptionText1);
            if (text2) UIManager.Instance.SetText(text2.gameObject, pageData.descriptionText2);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            // 초기화: 투명하게 시작
            SetTextAlpha(text1, 0f);
            SetTextAlpha(text2, 0f);
            if (imageCanvasGroup) imageCanvasGroup.alpha = 0f;

            StartCoroutine(SequenceRoutine());
        }

        private IEnumerator SequenceRoutine()
        {
            
            // 텍스트 1 페이드 인 (1초)
            yield return StartCoroutine(FadeText(text1, 0f, 1f, 1.0f));
            
            // 이미지 그룹 페이드 인 (1초)
            yield return StartCoroutine(FadeCanvasGroup(imageCanvasGroup, 0f, 1f, 1.0f));
            
            // 텍스트 2 페이드 인 (1초)
            yield return StartCoroutine(FadeText(text2, 0f, 1f, 1.0f));

            yield return new WaitForSeconds(4.0f);
            
            // 4. 완료 -> 타이틀로 이동 (Manager가 처리)
            //CompleteStep();
        }

        // [Helper] 텍스트 페이드
        private IEnumerator FadeText(Text target, float start, float end, float duration)
        {
            if (!target) yield break;
            float t = 0f;
            SetTextAlpha(target, start);
            while (t < duration)
            {
                t += Time.deltaTime;
                SetTextAlpha(target, Mathf.Lerp(start, end, t / duration));
                yield return null;
            }
            SetTextAlpha(target, end);
        }

        // [Helper] 캔버스 그룹 페이드
        private IEnumerator FadeCanvasGroup(CanvasGroup target, float start, float end, float duration)
        {
            if (!target) yield break;
            float t = 0f;
            target.alpha = start;
            while (t < duration)
            {
                t += Time.deltaTime;
                target.alpha = Mathf.Lerp(start, end, t / duration);
                yield return null;
            }
            target.alpha = end;
        }

        private void SetTextAlpha(Text t, float a)
        {
            if (t) { Color c = t.color; c.a = a; t.color = c; }
        }
    }
}