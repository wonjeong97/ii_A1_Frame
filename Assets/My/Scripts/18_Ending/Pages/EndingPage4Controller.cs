using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Text;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Utils;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;
using ZLogger;

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
        private CancellationTokenSource _sequenceCts;

        // --- 의존성 주입 (DI) 변수 ---
        private GameManager _gameManager;
        private SessionManager _sessionManager;
        private SoundManager _soundManager;
        private ILogger<EndingPage4Controller> _logger;

        [Inject]
        public void Construct(
            GameManager gameManager, 
            SessionManager sessionManager, 
            SoundManager soundManager, 
            ILogger<EndingPage4Controller> logger)
        {
            _gameManager = gameManager;
            _sessionManager = sessionManager;
            _soundManager = soundManager;
            _logger = logger;
        }

        protected override void SetupData(EndingPage4Data data)
        {
            _data = data;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            ResetUIStates();
            
            if (CanSendPieceUpdate())
            {
                _gameManager.SendPieceUpdateAPI(PagePieceReward);
                _hasSentPieceUpdate = true; 
            }

            UpdateTotalPiecesText();
            
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = new CancellationTokenSource();

            SequenceAsync(_sequenceCts.Token).Forget();
        }

        public override void OnExit()
        {
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = null;

            base.OnExit();
        }

        private void ResetUIStates()
        {
            if (text1 && _data?.descriptionText1 != null && _uiManager)
            {
                _uiManager.SetText(text1.gameObject, _data.descriptionText1);
            }

            if (textCanvasGroup) textCanvasGroup.alpha = 0f;
            if (imageCanvasGroup) imageCanvasGroup.alpha = 1f;
            
            if (pieceImages != null)
            {
                for (int i = 0; i < pieceImages.Length; i++)
                {
                    if (pieceImages[i]) pieceImages[i].SetAlpha(0f);
                }
            }
        }

        private bool CanSendPieceUpdate()
        {
            return _gameManager && _sessionManager && 
                   _sessionManager.CurrentUserId != 0 && !_hasSentPieceUpdate;
        }

        private void UpdateTotalPiecesText()
        {
            if (!text2 || _data?.descriptionText2 == null) return;
            if (!_gameManager || !_sessionManager) return;
            if (_uiManager) _uiManager.SetText(text2.gameObject, _data.descriptionText2);
            
            int existingPieces = _sessionManager.TotalPieces;
            int pendingReward = _hasSentPieceUpdate ? PagePieceReward : 0;
            int totalPieces = existingPieces + pendingReward;

            string template = _data.descriptionText2.text;
            if (string.IsNullOrEmpty(template)) return;

            text2.text = ZString.Format(template, totalPieces);
        }

        private async UniTaskVoid SequenceAsync(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(500, ignoreTimeScale: true, cancellationToken: token);
                await PlayPieceAnimationsAsync(token);
                
                await UniTask.Delay(2000, ignoreTimeScale: true, cancellationToken: token);
                await ShowResultTextAsync(token);

                await UniTask.Delay(3000, ignoreTimeScale: true, cancellationToken: token);
                CompleteStep();
            }
            catch (OperationCanceledException)
            {
                // 취소 예외 무음 억제
            }
        }

        private async UniTask PlayPieceAnimationsAsync(CancellationToken token)
        {
            if (pieceImages == null || pieceImages.Length == 0)
            {
                PlayRewardSFX();
                return;
            }

            for (int i = 0; i < pieceImages.Length; i++)
            {
                if (!pieceImages[i]) continue;

                PlayRewardSFX();
                await pieceImages[i].FadeAsync(0f, 1f, 0.8f, token);
            }
        }

        private void PlayRewardSFX()
        {
            if (_soundManager)
            {
                _soundManager.PlaySFX("공통_6");
            }
        }

        private async UniTask ShowResultTextAsync(CancellationToken token)
        {
            if (!textCanvasGroup) return;

            await textCanvasGroup.FadeAsync(0f, 1f, 0.5f, token);
        }

        protected override void OnDestroy()
        {
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = null;

            base.OnDestroy();
        }
    }
}