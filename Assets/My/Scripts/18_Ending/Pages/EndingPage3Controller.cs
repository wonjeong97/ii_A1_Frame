using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using My.Scripts.Global;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._18_Ending.Pages
{
    [Serializable]
    public class EndingPage3Data
    {
        public TextSetting descriptionText; // 기본 텍스트
        public TextSetting allFinishedText; // 모든 체험 완료 텍스트
    }

    public class EndingPage3Controller : GamePage<EndingPage3Data>
    {
        [Header("UI References")] 
        [SerializeField] private Text descriptionText;

        protected override void SetupData(EndingPage3Data data)
        {
            int randomValue = UnityEngine.Random.Range(0, 2);
            TextSetting textToUse = data.descriptionText;

            if (randomValue == 1 && data.allFinishedText != null)
            {
                textToUse = data.allFinishedText;
                Debug.Log("[EndingPage3] Random(50%): 모든 체험 완료 메시지 선택");
            }
            else
            {
                textToUse = data.descriptionText;
                Debug.Log("[EndingPage3] Random(50%): 현재 체험 완료 메시지 선택");
            }

            if (descriptionText) 
            {
                UIManager.Instance.SetText(descriptionText.gameObject, textToUse);
            }
        }

        public override void OnEnter()
        {
            gameObject.SetActive(true);

            // [초기화] 페이지와 텍스트 모두 투명하게 시작
            SetAlpha(0f);
            SetTextAlpha(descriptionText, 0f);

            StartCoroutine(SequenceRoutine());
        }

        private IEnumerator SequenceRoutine()
        {
            yield return new WaitForSeconds(1.0f);

            // 2. 텍스트 페이드 인 (1초)
            yield return StartCoroutine(FadeText(descriptionText, 0f, 1f, 1.0f));

            // 3. 7초 대기
            yield return new WaitForSeconds(7.0f);

            // 4. 완료 -> 타이틀 전환
            CompleteStep();
        }

        // 페이지 페이드 (CanvasGroup)
        private IEnumerator FadePageAlpha(float start, float end, float duration)
        {
            float t = 0f;
            SetAlpha(start);
            while (t < duration)
            {
                t += Time.deltaTime;
                SetAlpha(Mathf.Lerp(start, end, t / duration));
                yield return null;
            }

            SetAlpha(end);
        }

        // 텍스트 페이드 (Text Color Alpha)
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

        private void SetTextAlpha(Text t, float a)
        {
            if (t)
            {
                Color c = t.color;
                c.a = a;
                t.color = c;
            }
        }
    }
}