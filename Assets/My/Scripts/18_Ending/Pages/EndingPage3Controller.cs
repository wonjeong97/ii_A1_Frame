using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._18_Ending.Pages
{
    [Serializable]
    public class EndingPage3Data
    {
        public TextSetting descriptionText1; // "'우리'는 마음조각 5개를 받았습니다."
        public TextSetting descriptionText2; // "'우리'의 마음 조각 0/20"
    }

    /// <summary> 
    /// 엔딩 3페이지 컨트롤러입니다.
    /// 플레이어들이 모은 '마음 조각'의 결과를 확인하는 페이지로, 텍스트와 이미지를 순차적으로 보여주는 연출을 담당합니다.
    /// </summary>
    public class EndingPage3Controller : GamePage<EndingPage3Data>
    {
        [Header("UI References")]
        [SerializeField] private Text text1; 
        [SerializeField] private Text text2; 
        [SerializeField] private CanvasGroup imageCanvasGroup;
        [SerializeField] private CanvasGroup textCanvasGroup;

        protected override void SetupData(EndingPage3Data data)
        {
            if (text1) UIManager.Instance.SetText(text1.gameObject, data.descriptionText1);
            if (text2) UIManager.Instance.SetText(text2.gameObject, data.descriptionText2);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            if (textCanvasGroup) textCanvasGroup.alpha = 0;
            if (imageCanvasGroup) imageCanvasGroup.alpha = 0f;
            
            StartCoroutine(SequenceRoutine());
        }

        /// <summary>
        /// 마음 조각 이미지 페이드인(1초) >> 2초 대기 >> 텍스트 페이드인(1초) >> 3초 대기 후 완료
        /// </summary>
        private IEnumerator SequenceRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(0.5f); // 페이지 로드 대기
            SoundManager.Instance?.PlaySFX("공통_6");
            yield return StartCoroutine(FadeCanvasGroup(imageCanvasGroup, 0f, 1f, 1.0f));
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            yield return StartCoroutine(FadeCanvasGroup(textCanvasGroup, 0f, 1f, 1.0f));
            yield return CoroutineData.GetWaitForSeconds(3.0f);
            
            CompleteStep();
        }

        /// <summary>
        /// 캔버스 그룹의 투명도를 부드럽게 변경합니다. (이미지 등 그룹 단위 제어용)
        /// </summary>
        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float s, float e, float d)
        {
            if (!cg) yield break;
            float time = 0f;
            cg.alpha = s;
            
            while(time < d) 
            { 
                time += Time.deltaTime; 
                cg.alpha = Mathf.Lerp(s, e, time/d); 
                yield return null; 
            }
            cg.alpha = e;
        }
    }
}