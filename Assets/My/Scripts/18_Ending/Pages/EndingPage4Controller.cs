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
        
        [Header("Piece Animation")]
        [Tooltip("순차적으로 나타날 5개의 조각 이미지를 할당해 주세요.")]
        [SerializeField] private Image[] pieceImages; 
        
        private const int PagePieceReward = 5; 
        private EndingPage4Data _data; 
        private bool _hasSentPieceUpdate;

        protected override void SetupData(EndingPage4Data data)
        {
            _data = data;
            if (text1 && UIManager.Instance) UIManager.Instance.SetText(text1.gameObject, data.descriptionText1);
            if (text2 && UIManager.Instance) UIManager.Instance.SetText(text2.gameObject, data.descriptionText2);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            if (textCanvasGroup) textCanvasGroup.alpha = 0f;
            
            // 배경 등의 요소를 위해 부모 캔버스 그룹은 켜두고, 내부 조각 이미지들을 투명하게 설정
            if (imageCanvasGroup) imageCanvasGroup.alpha = 1f;
            
            if (pieceImages != null)
            {
                foreach (Image img in pieceImages)
                {
                    SetImageAlpha(img, 0f);
                }
            }
            
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
            
            // 5개의 조각 이미지를 각각 0.8초 동안 순차적으로 나타내며 사운드 재생
            if (pieceImages != null && pieceImages.Length > 0)
            {
                foreach (Image pieceImg in pieceImages)
                {
                    if (pieceImg)
                    {
                        SoundManager.Instance?.PlaySFX("공통_6"); 
                        yield return StartCoroutine(FadeImage(pieceImg, 0f, 1f, 0.8f));
                    }
                }
            }
            else
            {
                // 배열에 이미지가 등록되지 않았을 때를 대비한 안전 장치(기존처럼 사운드 1회 재생)
                SoundManager.Instance?.PlaySFX("공통_6");
            }
            
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            
            // 텍스트 그룹 등장
            yield return StartCoroutine(FadeCanvasGroup(textCanvasGroup, 0f, 1f, 0.5f));
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

        /// <summary> 개별 이미지(Image)의 투명도를 선형 보간하여 시각적 전환 수행 </summary>
        private IEnumerator FadeImage(Image img, float s, float e, float d)
        {
            if (!img) yield break;
            float time = 0f;
            SetImageAlpha(img, s);
            
            while(time < d) 
            { 
                time += Time.deltaTime; 
                SetImageAlpha(img, Mathf.Lerp(s, e, time/d)); 
                yield return null; 
            }
            SetImageAlpha(img, e);
        }

        /// <summary> 이미지의 투명도 직접 갱신 </summary>
        private void SetImageAlpha(Image img, float a)
        {
            if (img)
            {
                Color c = img.color;
                c.a = a;
                img.color = c;
            }
        }
    }
}