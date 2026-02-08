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
    public class EndingPage4Data
    {
        public TextSetting descriptionText;
        public TextSetting allFinishedText;
    }

    /// <summary> 
    /// 엔딩 씬의 마지막 페이지 컨트롤러입니다.
    /// 플레이어에게 최종 메시지를 전달하며, 50% 확률로 '붉은 실(Red Line)' 연출을 포함한 특별 엔딩을 보여줍니다.
    /// </summary>
    public class EndingPage4Controller : GamePage<EndingPage4Data>
    {
        [Header("UI References")] 
        [SerializeField] private Text descriptionText;
        [SerializeField] private Image redLineImage;

        private bool _isAllFinished = false; // 특별 엔딩(붉은 실) 활성화 여부

        protected override void SetupData(EndingPage4Data data)
        {
            // 엔딩의 다양성을 위해 50% 확률로 일반 엔딩과 특별 엔딩(Red Line)을 분기합니다.
            // # TODO: 현재는 완전 랜덤이지만, 추후 플레이어의 선택이나 성취도(QnA 결과 등)에 따라 결정되도록 고도화할 필요가 있음.
            int randomValue = UnityEngine.Random.Range(0, 2);
            TextSetting textToUse = data.descriptionText;

            if (randomValue == 1 && data.allFinishedText != null)
            {
                textToUse = data.allFinishedText;
                _isAllFinished = true;
            }
            else
            {
                textToUse = data.descriptionText;
                _isAllFinished = false;
            }

            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, textToUse);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            Debug.Log($"[EndingPage4Controller] OnEnter: {Time.time}");
            
            // 연출 시작 전 화면을 투명하게 초기화하여 자연스러운 페이드 인을 준비합니다.
            SetAlpha(0f);

            if (redLineImage)
            {
                redLineImage.type = Image.Type.Filled;
                redLineImage.fillAmount = 0f;
            }

            StartCoroutine(SequenceRoutine());
        }

        /// <summary>
        /// 엔딩 시퀀스 루틴입니다. 분기된 엔딩 타입에 따라 다른 연출과 대기 시간을 가집니다.
        /// </summary>
        private IEnumerator SequenceRoutine()
        {
            if (_isAllFinished && redLineImage != null)
            {
                // 특별 엔딩: 텍스트를 먼저 읽을 시간을 주고(1초), 붉은 실이 차오르는 연출(1초)을 보여줍니다.
                // 그 후 충분한 여운(5초)을 남깁니다.
                yield return CoroutineData.GetWaitForSeconds(1.0f);
                yield return StartCoroutine(FillImageRoutine(redLineImage, 0f, 1f, 1.0f));
                yield return CoroutineData.GetWaitForSeconds(5.0f);
            }
            else
            {
                // 일반 엔딩: 텍스트만 보여주며 7초간 대기합니다.
                yield return CoroutineData.GetWaitForSeconds(7.0f);
            }
            
            Debug.Log($"[EndingPage4Controller] End Sequence: {Time.time}");
            CompleteStep();
        }

        /// <summary>
        /// 페이지 전체의 투명도를 조절하는 코루틴입니다.
        /// </summary>
        private IEnumerator FadePageAlpha(float s, float e, float d)
        {
            float t = 0f;
            SetAlpha(s);
            while (t < d)
            {
                t += Time.deltaTime;
                SetAlpha(Mathf.Lerp(s, e, t / d));
                yield return null;
            }

            SetAlpha(e);
        }

        /// <summary>
        /// 이미지의 FillAmount를 조절하여 게이지가 차오르는 듯한 연출을 수행합니다. (붉은 실 연출용)
        /// </summary>
        private IEnumerator FillImageRoutine(Image t, float s, float e, float d)
        {
            if (!t) yield break;
            float time = 0f;
            t.fillAmount = s;
            
            while (time < d)
            {
                time += Time.deltaTime;
                t.fillAmount = Mathf.Lerp(s, e, time / d);
                yield return null;
            }

            t.fillAmount = e;
        }
    }
}