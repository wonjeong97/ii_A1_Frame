using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._18_Ending.Pages
{
    [Serializable]
    public class EndingPage1Data
    {
        public TextSetting firstText; // "모든 질문을 찾았습니다."
        public TextSetting secondText; // "STEP.2\n기록된 우리 모습 확인하기."
    }

    /// <summary> 
    /// 엔딩 씬의 첫 번째 페이지를 담당하는 컨트롤러입니다.
    /// 두 개의 텍스트를 순차적으로 페이드 전환(Cross-fade)하여 보여주는 연출을 수행합니다.
    /// </summary>
    public class EndingPage1Controller : GamePage<EndingPage1Data>
    {
        [Header("UI References")]
        [SerializeField] private Text descriptionText; 

        private EndingPage1Data _data;

        protected override void SetupData(EndingPage1Data data)
        {
            _data = data;
            
            // 페이지 진입 시 첫 번째 텍스트가 바로 보여야 하므로 미리 설정합니다. (깜빡임 방지)
            if (descriptionText && _data.firstText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, _data.firstText);
                SetTextAlpha(1f);
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            StartCoroutine(SequenceRoutine());
        }

        /// <summary>
        /// 텍스트 전환 시퀀스를 제어합니다. (텍스트1 -> 대기 -> 페이드아웃 -> 텍스트2 -> 페이드인 -> 완료)
        /// </summary>
        private IEnumerator SequenceRoutine()
        {
            // 1. 첫 번째 텍스트 표시 확인
            if (descriptionText && _data?.firstText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, _data.firstText);
                SetTextAlpha(1f);
            }
            
            // 사용자가 첫 문구를 읽을 시간을 줍니다.
            yield return CoroutineData.GetWaitForSeconds(2.0f); 

            // 2. 텍스트 교체를 위해 페이드 아웃 (사라짐)
            yield return StartCoroutine(FadeText(1f, 0f, 1.0f));

            // 3. 내용 교체 ("STEP.2...")
            if (descriptionText && _data?.secondText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, _data.secondText);
            }

            // 4. 두 번째 텍스트 페이드 인 (나타남)
            yield return StartCoroutine(FadeText(0f, 1f, 1.0f));
            
            // 5. 충분히 보여준 뒤 다음 단계로 넘어갑니다.
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            CompleteStep();
        }

        /// <summary>
        /// 텍스트의 투명도(Alpha)를 부드럽게 변경합니다.
        /// </summary>
        /// <param name="start">시작 알파값 (0~1)</param>
        /// <param name="end">목표 알파값 (0~1)</param>
        /// <param name="duration">진행 시간(초)</param>
        private IEnumerator FadeText(float start, float end, float duration)
        {
            if (!descriptionText) yield break;
            
            float t = 0f;
            SetTextAlpha(start);
            
            while (t < duration)
            {
                t += Time.deltaTime;
                SetTextAlpha(Mathf.Lerp(start, end, t / duration));
                yield return null;
            }
            SetTextAlpha(end);
        }

        private void SetTextAlpha(float alpha)
        {
            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = alpha;
                descriptionText.color = c;
            }
        }
    }
}