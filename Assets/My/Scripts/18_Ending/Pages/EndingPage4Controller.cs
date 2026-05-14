using System;
using System.Collections;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Utils;
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

    /// <summary>
    /// 엔딩 단계에서 획득한 마음 조각 애니메이션과 최종 개수를 표시하는 컨트롤러.
    /// </summary>
    public class EndingPage4Controller : GamePage<EndingPage4Data>
    {
        [Header("UI References")]
        [SerializeField] private Text text1; 
        [SerializeField] private Text text2; 
        [SerializeField] private CanvasGroup imageCanvasGroup;
        [SerializeField] private CanvasGroup textCanvasGroup;
        
        [Header("Piece Animation")]
        [Tooltip("순차적으로 나타날 5개의 조각 이미지를 할당")]
        [SerializeField] private Image[] pieceImages; 
        
        private const int PagePieceReward = 5; 
        private EndingPage4Data _data; 
        private bool _hasSentPieceUpdate;

        private readonly static StringBuilder StringBuilder = new StringBuilder(128);

        protected override void SetupData(EndingPage4Data data)
        {
            _data = data;
            if (text1 && UIManager.Instance)
            {
                UIManager.Instance.SetText(text1.gameObject, data.descriptionText1);
            }
            if (text2 && UIManager.Instance)
            {
                UIManager.Instance.SetText(text2.gameObject, data.descriptionText2);
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            ResetUIStates();
            
            if (CanSendPieceUpdate())
            {
                GameManager.Instance.SendPieceUpdateAPI(PagePieceReward);
                _hasSentPieceUpdate = true; 
            }

            UpdateTotalPiecesText();
            SequenceAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// 진입 시 UI 요소들의 투명도 및 활성 상태를 초기화함.
        /// </summary>
        private void ResetUIStates()
        {
            if (textCanvasGroup) textCanvasGroup.alpha = 0f;
            if (imageCanvasGroup) imageCanvasGroup.alpha = 1f;
            
            if (pieceImages != null)
            {
                foreach (Image img in pieceImages)
                {
                    SetImageAlpha(img, 0f);
                }
            }
        }

        /// <summary>
        /// API 전송이 가능한 상태인지 확인함.
        /// </summary>
        private bool CanSendPieceUpdate()
        {
            return GameManager.Instance && SessionManager.Instance && 
                   SessionManager.Instance.CurrentUserId != 0 && !_hasSentPieceUpdate;
        }

        /// <summary>
        /// StringBuilder를 사용하여 가비지 생성 없이 최종 조각 개수를 UI에 반영함.
        /// </summary>
        private void UpdateTotalPiecesText()
        {
            if (!text2 || _data?.descriptionText2 == null) return;
            if (!GameManager.Instance || !SessionManager.Instance) return;
            
            int existingPieces = SessionManager.Instance.TotalPieces;
            int pendingReward = _hasSentPieceUpdate ? PagePieceReward : 0;
            int totalPieces = existingPieces + pendingReward;

            string template = _data.descriptionText2.text;
            if (string.IsNullOrEmpty(template)) return;

            StringBuilder.Clear();
            StringBuilder.Append(template);
            StringBuilder.Replace("{0}", totalPieces.ToString());

            text2.text = StringBuilder.ToString();
        }

        private async UniTaskVoid SequenceAsync(CancellationToken token)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(0.5), cancellationToken: token);
            await PlayPieceAnimationsAsync(token);
            
            await UniTask.Delay(TimeSpan.FromSeconds(2.0), cancellationToken: token);
            await ShowResultTextAsync(token);

            await UniTask.Delay(TimeSpan.FromSeconds(3.0), cancellationToken: token);
            CompleteStep();
        }

        /// <summary>
        /// 할당된 조각 이미지들을 순차적으로 페이드인함.
        /// </summary>
        private async UniTask PlayPieceAnimationsAsync(CancellationToken token)
        {
            if (pieceImages == null || pieceImages.Length == 0)
            {
                PlayRewardSFX();
                return;
            }

            foreach (Image pieceImg in pieceImages)
            {
                if (!pieceImg)
                {
                    continue;
                }

                PlayRewardSFX();
                await UIFadeUtility.FadeGraphicAsync(pieceImg, 0f, 1f, 0.8f, token);
            }
        }

        /// <summary>
        /// 보상 획득 시 사용되는 공통 효과음을 재생함.
        /// </summary>
        private void PlayRewardSFX()
        {
            if (SoundManager.Instance)
            {
                SoundManager.Instance.PlaySFX("공통_6");
            }
        }

        /// <summary>
        /// 결과 텍스트 그룹을 화면에 노출함.
        /// </summary>
        private async UniTask ShowResultTextAsync(CancellationToken token)
        {
            if (!textCanvasGroup)
            {
                return;
            }

            await UIFadeUtility.FadeCanvasGroupAsync(textCanvasGroup, 0f, 1f, 0.5f, token);
        }

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