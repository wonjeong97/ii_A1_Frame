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
        public TextSetting descriptionText1; // 설명 텍스트 1 데이터
        public TextSetting descriptionText2; // 설명 텍스트 2 데이터
    }

    /// <summary> 엔딩 2페이지 컨트롤러 (크레딧 및 감사 메시지) </summary>
    public class EndingPage2Controller : GamePage<EndingPage2Data>
    {
        [Header("UI References")]
        [SerializeField] private Text text1; // 설명 텍스트 1
        [SerializeField] private Text text2; // 설명 텍스트 2
        [SerializeField] private CanvasGroup imageCanvasGroup; // 이미지 그룹

        /// <summary> 데이터 설정 (텍스트 적용) </summary>
        protected override void SetupData(EndingPage2Data data)
        {
            if (text1) UIManager.Instance.SetText(text1.gameObject, data.descriptionText1);
            if (text2) UIManager.Instance.SetText(text2.gameObject, data.descriptionText2);
        }

        /// <summary> 페이지 진입 (초기화 및 시퀀스 시작) </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            // 초기화: 투명하게 시작
            SetTextAlpha(text1, 0f);
            SetTextAlpha(text2, 0f);
            if (imageCanvasGroup) imageCanvasGroup.alpha = 0f;

            StartCoroutine(SequenceRoutine());
        }

        /// <summary> 연출 시퀀스 (텍스트1 -> 이미지 -> 텍스트2 -> 완료) </summary>
        private IEnumerator SequenceRoutine()
        {
            // 1. 텍스트 1 페이드 인
            yield return StartCoroutine(FadeText(text1, 0f, 1f, 1.0f));
            
            // 2. 이미지 그룹 페이드 인
            yield return StartCoroutine(FadeCanvasGroup(imageCanvasGroup, 0f, 1f, 1.0f));
            
            // 3. 텍스트 2 페이드 인
            yield return StartCoroutine(FadeText(text2, 0f, 1f, 1.0f));

            // 대기
            yield return new WaitForSeconds(4.0f);
            
            // 4. 완료
            CompleteStep();
        }

        /// <summary> 텍스트 투명도 페이드 코루틴 </summary>
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

        /// <summary> 캔버스 그룹 투명도 페이드 코루틴 </summary>
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
        
        /// <summary> 페이지 전체 투명도 페이드 코루틴 </summary>
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

        /// <summary> 텍스트 알파값 즉시 설정 </summary>
        private void SetTextAlpha(Text t, float a)
        {
            if (t) { Color c = t.color; c.a = a; t.color = c; }
        }
    }
}