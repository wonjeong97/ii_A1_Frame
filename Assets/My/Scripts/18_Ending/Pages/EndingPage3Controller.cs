using System;
using System.Collections;
using System.Text.RegularExpressions;
using My.Scripts.Core;
using My.Scripts.Global; 
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
        public TextSetting descriptionText1; 
        public TextSetting descriptionText2; 
    }

    public class EndingPage3Controller : GamePage<EndingPage3Data>
    {
        [Header("UI References")]
        [SerializeField] private Text text1; 
        [SerializeField] private Text text2; 
        [SerializeField] private CanvasGroup imageCanvasGroup;
        [SerializeField] private CanvasGroup textCanvasGroup;

        // 나중에 OnEnter에서 텍스트를 교체하기 위해 데이터를 들고 있습니다.
        private EndingPage3Data _data; 

        protected override void SetupData(EndingPage3Data data)
        {
            _data = data;
            
            if (text1) UIManager.Instance.SetText(text1.gameObject, data.descriptionText1);
            if (text2) UIManager.Instance.SetText(text2.gameObject, data.descriptionText2);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            if (textCanvasGroup) textCanvasGroup.alpha = 0;
            if (imageCanvasGroup) imageCanvasGroup.alpha = 0f;
            
            UpdateTotalPiecesText();

            if (GameManager.Instance)
            {
                Debug.Log("[EndingPage3] 진입: 마음 조각 업데이트 호출 (고정값: 5)");
                GameManager.Instance.SendPieceUpdateAPI(5);
            }
            
            StartCoroutine(SequenceRoutine());
        }

        /// <summary>
        /// API 통신이 끝난 시점에서 최종 마음 조각 개수를 계산하여 화면 텍스트에 적용합니다.
        /// </summary>
        private void UpdateTotalPiecesText()
        {
            if (text2 && _data?.descriptionText2 != null)
            {
                int totalPieces = 0;
                int existingPieces = 0;
                
                if (GameManager.Instance)
                {
                    existingPieces = GameManager.Instance.TotalPieces;
                    totalPieces = existingPieces + 5; // 기존 조각 + 이번 판 5개
                }

                string originalText = _data.descriptionText2.text;
                if (!string.IsNullOrEmpty(originalText))
                {
                    text2.text = originalText.Replace("{0}", totalPieces.ToString());
                }
            }
        }

        private IEnumerator SequenceRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(0.5f);
            SoundManager.Instance?.PlaySFX("공통_6");
            yield return StartCoroutine(FadeCanvasGroup(imageCanvasGroup, 0f, 1f, 1.0f));
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            yield return StartCoroutine(FadeCanvasGroup(textCanvasGroup, 0f, 1f, 1.0f));
            yield return CoroutineData.GetWaitForSeconds(3.0f);
            
            CompleteStep();
        }

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