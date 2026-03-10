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
    /// <summary> 엔딩 1페이지용 데이터 구조체 </summary>
    [Serializable]
    public class EndingPage1Data
    {
        public TextSetting firstText; // "모든 질문을 찾았습니다."
        public TextSetting secondText; // "STEP.2\n기록된 우리 모습 확인하기."
    }

    /// <summary> 
    /// 엔딩 씬의 진입 안내를 담당하는 컨트롤러입니다.
    /// 두 개의 안내 문구를 순차적인 페이드 효과(Cross-fade)로 연출하여 몰입감을 높입니다.
    /// </summary>
    public class EndingPage1Controller : GamePage<EndingPage1Data>
    {
        [Header("UI References")]
        [SerializeField] private Text descriptionText; 

        private EndingPage1Data _data;

        /// <summary> JSON 설정 로드 및 첫 번째 문구 즉시 활성화 </summary>
        protected override void SetupData(EndingPage1Data data)
        {
            _data = data;
            
            // 진입 시 화면이 비어있지 않도록 첫 문구를 미리 렌더링
            if (descriptionText && _data.firstText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, _data.firstText);
                SetTextAlpha(1f);
            }
        }

        /// <summary> 페이지 활성화 시 연출 시퀀스 가동 </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            StartCoroutine(SequenceRoutine());
        }

        /// <summary> 
        /// 텍스트의 가독성과 연출 흐름을 위해 대기 및 페이드 효과를 순차 제어합니다.
        /// (텍스트1 유지 -> 페이드아웃 -> 내용 교체 -> 페이드인)
        /// </summary>
        private IEnumerator SequenceRoutine()
        {
            // 데이터 무결성 재확인 후 첫 문구 노출
            if (descriptionText && _data?.firstText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, _data.firstText);
                SetTextAlpha(1f);
            }
            
            // 사용자가 첫 문구를 인지할 수 있는 최소 시간 확보
            yield return CoroutineData.GetWaitForSeconds(2.0f); 

            // 자연스러운 내용 교체를 위한 투명도 연출
            yield return StartCoroutine(FadeText(1f, 0f, 1f));

            // 두 번째 단계 안내 내용으로 갱신
            if (descriptionText && _data?.secondText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, _data.secondText);
                SoundManager.Instance?.PlaySFX("공통_13"); // 시각 변화에 따른 효과음 강조
            }

            // 교체된 내용을 부드럽게 노출
            yield return StartCoroutine(FadeText(0f, 1f, 1f));
            
            // 안내 완료 후 다음 페이지로 자동 전환
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            CompleteStep();
        }

        /// <summary> 
        /// 지정된 시간 동안 텍스트 투명도를 선형 보간하여 시각적 전환을 수행합니다. 
        /// </summary>
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

        /// <summary> UI 텍스트 컬러 속성의 알파값을 직접 수정합니다. </summary>
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