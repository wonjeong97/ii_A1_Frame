using System;
using System.Collections;
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
    public class EndingPage4Data
    {
        public TextSetting descriptionText1; 
        public TextSetting descriptionText2; 
    }

    public class EndingPage4Controller : GamePage<EndingPage4Data>
    {
        [Header("UI References")]
        [SerializeField] private Text text1; 
        [SerializeField] private Text text2; 
        [SerializeField] private CanvasGroup imageCanvasGroup;
        [SerializeField] private CanvasGroup textCanvasGroup;
        
        private const int PagePieceReward = 5;
        private EndingPage4Data _data; 
        private bool _hasSentPieceUpdate;

        protected override void SetupData(EndingPage4Data data)
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
            
            if (GameManager.Instance && SessionManager.Instance && SessionManager.Instance.CurrentUserId != 0 && !_hasSentPieceUpdate)
            {
                GameManager.Instance.SendPieceUpdateAPI(PagePieceReward);
                _hasSentPieceUpdate = true; 
            }

            UpdateTotalPiecesText();
            
            StartCoroutine(SequenceRoutine());
        }

        private void UpdateTotalPiecesText()
        {
            if (text2 && _data?.descriptionText2 != null)
            {
                if (!GameManager.Instance || !SessionManager.Instance) return;
                
                int existingPieces = SessionManager.Instance.TotalPieces;
                int pendingReward = _hasSentPieceUpdate ? PagePieceReward : 0;
                int totalPieces = existingPieces + pendingReward;

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