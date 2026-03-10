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
    /// 튜토리얼의 마지막 진입 단계를 제어하는 페이지 컨트롤러.
    /// 플레이어가 실제 조작을 시작하기 전, 단계별 안내 문구와 카운트다운을 통해 심리적 준비 시간을 제공합니다.
    /// </summary>
    public class Page_Tutorial8 : GamePage<TutorialPage8Data>
    {
        [Header("UI References")]
        [SerializeField] private Text descriptionText; 

        private TutorialPage8Data _data;

        /// <summary> JSON 설정에서 인트로, 카운트다운, 시작 텍스트 데이터를 캐싱하고 초기 UI를 구성합니다. </summary>
        protected override void SetupData(TutorialPage8Data data)
        {
            _data = data;
            if (descriptionText && _data.introText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, _data.introText);
                SetTextAlpha(1f);
            }
        }

        /// <summary> 페이지 진입 시 효과음을 재생하고 순차적인 카운트다운 연출을 가동합니다. </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            SoundManager.Instance?.PlaySFX("공통_13");
            StartCoroutine(SequenceRoutine());
        }

        /// <summary> 예기치 않은 퇴장 시 진행 중인 연출 코루틴을 강제 중단하여 메모리 누수 및 오작동을 방지합니다. </summary>
        public override void OnExit()
        {
            StopAllCoroutines();
            base.OnExit();
        }

        /// <summary> 
        /// 인트로 텍스트 대기 -> 3, 2, 1 카운트다운 -> 시작 알림 순으로 화면 전환을 동기화합니다. 
        /// </summary>
        private IEnumerator SequenceRoutine()
        {
            // 1. 유저가 현재 단계를 인지할 수 있도록 인트로 텍스트 노출
            if (descriptionText && _data?.introText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, _data.introText);
                SetTextAlpha(1f);
            }
            
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            yield return StartCoroutine(FadeText(1f, 0f, 1f));
            
            string[] counts = { "3", "2", "1" };
            TextSetting countSetting = _data?.countdownText;
            
            SoundManager.Instance?.PlaySFX("공통_10_3초");
            foreach (string count in counts)
            {
                SetTextAlpha(1f);

                if (descriptionText)
                {
                    if (countSetting != null)
                    {
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

            // 3. 카운트다운 완료 후 최종 시작 텍스트 노출
            if (descriptionText && _data?.startText != null)
            {   
                // 사운드와 시각적 전환의 싱크를 맞추기 위한 미세 지연
                yield return CoroutineData.GetWaitForSeconds(0.3f); 
                UIManager.Instance.SetText(descriptionText.gameObject, _data.startText);
                SetTextAlpha(1f); 
                SoundManager.Instance?.PlaySFX("공통_14");
            }
            yield return CoroutineData.GetWaitForSeconds(1.0f); 

            CompleteStep();
        }

        /// <summary> 텍스트 컴포넌트의 알파값을 선형 보간하여 시각적 페이드 효과를 생성합니다. </summary>
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

        /// <summary> Color 구조체를 우회하여 텍스트의 투명도를 즉시 갱신합니다. </summary>
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