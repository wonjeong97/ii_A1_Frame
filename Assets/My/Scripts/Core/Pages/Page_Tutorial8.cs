using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Data;
using My.Scripts.Utils;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;

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
        private CancellationTokenSource _sequenceCts;

        private readonly static string[] CountdownCounts = { "3", "2", "1" };

        // --- 의존성 주입 (DI) 변수 ---
        private SoundManager _soundManager;

        /// <summary> 부모들의 의존성 수령 체인 외에 본 페이지에 필요한 사운드 컨트롤러 주입 </summary>
        [Inject]
        public void ConstructTutorial8(SoundManager soundManager)
        {
            _soundManager = soundManager;
        }

        protected override void SetupData(TutorialPage8Data data)
        {
            _data = data;
            
            if (descriptionText && _data.introText != null && _uiManager != null)
            {
                _uiManager.SetText(descriptionText.gameObject, _data.introText);
                descriptionText.SetAlpha(1f); 
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = new CancellationTokenSource();

            if (_soundManager)
            {
                _soundManager.PlaySFX("공통_13");
            }
            
            SequenceAsync(_sequenceCts.Token).Forget();
        }

        public override void OnExit()
        {
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = null;
            
            base.OnExit();
        }

        private async UniTaskVoid SequenceAsync(CancellationToken token)
        {
            await ShowIntroTextAsync(token);
            await PlayCountdownAsync(token);
            await ShowStartTextAsync(token);

            CompleteStep();
        }
        
        private async UniTask ShowIntroTextAsync(CancellationToken token)
        {
            if (descriptionText && _data?.introText != null && _uiManager)
            {
                _uiManager.SetText(descriptionText.gameObject, _data.introText);
                descriptionText.SetAlpha(1f);
            }
    
            await UniTask.Delay(2000, ignoreTimeScale: true, cancellationToken: token);
            
            if (descriptionText)
            {
                await descriptionText.FadeAsync(1f, 0f, 1f, token);
            }
        }

        private async UniTask PlayCountdownAsync(CancellationToken token)
        {
            if (_soundManager)
            {
                _soundManager.PlaySFX("공통_10_3초");
            }

            TextSetting countSetting = _data?.countdownText;

            foreach (string count in CountdownCounts)
            {
                if (descriptionText) descriptionText.SetAlpha(1f);
                UpdateCountdownText(count, countSetting);
                
                await UniTask.Delay(1000, ignoreTimeScale: true, cancellationToken: token);
            }
        }

        private void UpdateCountdownText(string count, TextSetting countSetting)
        {
            if (!descriptionText) return;

            if (countSetting == null || !_uiManager)
            {
                descriptionText.text = count;
                return;
            }

            string originalText = countSetting.text; 
            countSetting.text = count;
            _uiManager.SetText(descriptionText.gameObject, countSetting);
            countSetting.text = originalText; 
        }

        private async UniTask ShowStartTextAsync(CancellationToken token)
        {
            if (descriptionText && _data?.startText != null)
            {   
                await UniTask.Delay(300, ignoreTimeScale: true, cancellationToken: token); 
                
                if (_uiManager) _uiManager.SetText(descriptionText.gameObject, _data.startText);
                descriptionText.SetAlpha(1f); 
                
                if (_soundManager)
                {
                    _soundManager.PlaySFX("공통_14");
                }
            }
            
            // 정수형 밀리초(1000ms) 변환 최적화 적용
            await UniTask.Delay(1000, ignoreTimeScale: true, cancellationToken: token); 
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