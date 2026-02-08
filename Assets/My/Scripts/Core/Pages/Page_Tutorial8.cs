using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Data;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary> 
    /// PlayTutorial 8페이지 컨트롤러 
    /// <para>기능: 인트로(FadeOut) -> 카운트다운(3,2,1) -> 시작 텍스트 자동 재생</para>
    /// </summary>
    public class Page_Tutorial8 : GamePage<TutorialPage8Data>
    {
        [Header("UI References")]
        [SerializeField] private Text descriptionText; // 중앙 텍스트

        private TutorialPage8Data _data;

        protected override void SetupData(TutorialPage8Data data)
        {
            _data = data;
            // 초기 상태: 인트로 텍스트 적용
            if (descriptionText && _data.introText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, _data.introText);
                SetTextAlpha(1f); // 알파값 확실하게 초기화
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            StartCoroutine(SequenceRoutine());
        }

        public override void OnExit()
        {
            StopAllCoroutines();
            base.OnExit();
        }

        private IEnumerator SequenceRoutine()
        {
            // 1. 인트로 텍스트 ("STEP.1...") 보여주기
            if (descriptionText && _data?.introText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, _data.introText);
                SetTextAlpha(1f);
            }
            
            // 2초간 유지
            yield return CoroutineData.GetWaitForSeconds(2.0f); 

            // [추가] 1초간 페이드 아웃 (사라짐)
            yield return StartCoroutine(FadeText(1f, 0f, 1.0f));

            // 2. 카운트다운 (3 -> 2 -> 1)
            string[] counts = { "3", "2", "1" };
            TextSetting countSetting = _data?.countdownText;

            foreach (string count in counts)
            {
                // 카운트다운 시작 전 텍스트 다시 보이게 설정 (알파 1)
                SetTextAlpha(1f);

                if (descriptionText)
                {
                    if (countSetting != null)
                    {
                        // 스타일 적용
                        string originalText = countSetting.text; 
                        countSetting.text = count;
                        UIManager.Instance.SetText(descriptionText.gameObject, countSetting);
                        countSetting.text = originalText; 
                    }
                    else
                    {
                        descriptionText.text = count;
                    }
                }
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            // 3. 시작 텍스트 ("시작!")
            if (descriptionText && _data?.startText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, _data.startText);
                SetTextAlpha(1f); // 스타일 적용 시 알파가 변경될 수 있으므로 확인
            }
            yield return CoroutineData.GetWaitForSeconds(1.0f); 

            // 4. 완료
            CompleteStep();
        }

        /// <summary> 텍스트 알파값 페이드 코루틴 </summary>
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

        /// <summary> 텍스트 투명도 즉시 설정 </summary>
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