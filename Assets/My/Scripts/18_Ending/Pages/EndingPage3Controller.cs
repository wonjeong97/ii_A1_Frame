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

    /// <summary> 엔딩 3페이지 컨트롤러 (조건부 RedLine 연출) </summary>
    public class EndingPage3Controller : GamePage<EndingPage3Data>
    {
        [Header("UI References")] 
        [SerializeField] private Text descriptionText; // 설명 텍스트
        [SerializeField] private Image redLineImage; // RedLine 이미지

        private bool _isAllFinished = false; // 모든 체험 완료 여부

        /// <summary> 데이터 설정 (텍스트 및 완료 여부 결정) </summary>
        protected override void SetupData(EndingPage3Data data)
        {
            // 50% 확률 로직 (임시)
            int randomValue = UnityEngine.Random.Range(0, 2);
            TextSetting textToUse = data.descriptionText;

            if (randomValue == 1 && data.allFinishedText != null)
            {
                textToUse = data.allFinishedText;
                _isAllFinished = true; // Case A: 모든 체험 완료
                Debug.Log("[EndingPage3] Random(50%): 모든 체험 완료 메시지 선택");
            }
            else
            {
                textToUse = data.descriptionText;
                _isAllFinished = false; // Case B: 단일 체험 완료
                Debug.Log("[EndingPage3] Random(50%): 현재 체험 완료 메시지 선택");
            }

            if (descriptionText)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, textToUse);
            }
        }

        /// <summary> 페이지 진입 (초기화 및 시퀀스 시작) </summary>
        public override void OnEnter()
        {
            gameObject.SetActive(true);

            // 초기화: 투명하게 시작
            SetAlpha(0f);
            SetTextAlpha(descriptionText, 0f);

            // RedLine 초기화
            if (redLineImage)
            {
                redLineImage.type = Image.Type.Filled; // Fill 타입 강제
                redLineImage.fillAmount = 0f; // 0에서 시작
            }

            StartCoroutine(SequenceRoutine());
        }

        /// <summary> 연출 시퀀스 (페이드 -> 텍스트 -> RedLine -> 완료) </summary>
        private IEnumerator SequenceRoutine()
        {
            // 1. 페이지 전체 페이드 인
            yield return StartCoroutine(FadePageAlpha(0f, 1f, 1.0f));

            // 2. 텍스트 페이드 인
            yield return StartCoroutine(FadeText(descriptionText, 0f, 1f, 1.0f));

            // 3. 조건부 연출 분기
            if (_isAllFinished && redLineImage != null)
            {
                // [Case A] RedLine 채우기 (1초)
                yield return StartCoroutine(FillImageRoutine(redLineImage, 0f, 1f, 1.0f));

                // 6초 대기
                yield return new WaitForSeconds(6.0f);
            }
            else
            {
                // [Case B] 일반 대기 (7초)
                yield return new WaitForSeconds(7.0f);
            }

            // 4. 완료
            CompleteStep();
        }

        /// <summary> 이미지 채우기 연출 코루틴 </summary>
        private IEnumerator FillImageRoutine(Image target, float start, float end, float duration)
        {
            if (!target) yield break;

            float t = 0f;
            target.fillAmount = start;
            while (t < duration)
            {
                t += Time.deltaTime;
                target.fillAmount = Mathf.Lerp(start, end, t / duration);
                yield return null;
            }

            target.fillAmount = end;
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

        /// <summary> 텍스트 알파값 즉시 설정 </summary>
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