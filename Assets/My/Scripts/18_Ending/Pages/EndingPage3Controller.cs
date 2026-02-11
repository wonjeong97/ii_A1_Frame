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

        protected override void SetupData(EndingPage3Data data)
        {
            if (text1) UIManager.Instance.SetText(text1.gameObject, data.descriptionText1);
            if (text2) UIManager.Instance.SetText(text2.gameObject, data.descriptionText2);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            // 연출 시작 전 모든 요소를 숨겨두어 깜빡임 없이 자연스럽게 등장하도록 초기화합니다.
            SetTextAlpha(text1, 0f);
            SetTextAlpha(text2, 0f);
            if (imageCanvasGroup) imageCanvasGroup.alpha = 0f;

            StartCoroutine(SequenceRoutine());
        }

        /// <summary>
        /// 결과 확인 시퀀스를 제어합니다. (텍스트1 -> 이미지 -> 텍스트2 -> 대기 -> 완료)
        /// 순차적인 등장을 통해 사용자의 시선을 유도합니다.
        /// </summary>
        private IEnumerator SequenceRoutine()
        {
            // 1. 첫 번째 안내 문구 등장
            yield return StartCoroutine(FadeText(text1, 0f, 1f, 1f));
            
            // 2. 결과 이미지(마음 조각) 등장
            yield return StartCoroutine(FadeCanvasGroup(imageCanvasGroup, 0f, 1f, 1f));
            
            // 3. 상세 수치 텍스트 등장
            yield return StartCoroutine(FadeText(text2, 0f, 1f, 1f));
            
            // 결과를 충분히 확인할 시간을 제공한 뒤 다음으로 넘어갑니다.
            yield return CoroutineData.GetWaitForSeconds(4.0f);
            CompleteStep();
        }

        /// <summary>
        /// 텍스트의 투명도를 부드럽게 변경합니다.
        /// </summary>
        private IEnumerator FadeText(Text t, float s, float e, float d)
        {
            if (!t) yield break;
            float time = 0f;
            SetTextAlpha(t, s);
            
            while(time < d) 
            { 
                time += Time.deltaTime; 
                SetTextAlpha(t, Mathf.Lerp(s, e, time/d)); 
                yield return null; 
            }
            SetTextAlpha(t, e);
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

        private void SetTextAlpha(Text t, float a) 
        { 
            if(t) 
            { 
                Color c = t.color; 
                c.a = a; 
                t.color = c; 
            } 
        }
    }
}