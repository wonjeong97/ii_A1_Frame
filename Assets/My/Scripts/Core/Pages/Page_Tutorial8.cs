using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Data;
using My.Scripts.Utils;
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

        /// <summary> JSON 설정에서 인트로, 카운트다운, 시작 텍스트 데이터를 캐싱하고 초기 UI를 구성합니다. </summary>
        protected override void SetupData(TutorialPage8Data data)
        {
            _data = data;
            if (descriptionText && _data.introText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, _data.introText);
                SetTextAlpha(1f);
            }
        }

        /// <summary> 페이지 진입 시 효과음을 재생하고 순차적인 카운트다운 연출을 가동합니다. </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            // 기존 작업 취소 및 새로운 토큰 발행
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            if (SoundManager.Instance)
            {
                SoundManager.Instance.PlaySFX("공통_13");
            }
            
            SequenceAsync(_sequenceCts.Token).Forget();
        }

        /// <summary> 페이지 비활성화 시 진행 중인 비동기 연출을 강제 중단하여 오작동을 방지합니다. </summary>
        public override void OnExit()
        {
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = null;
            
            base.OnExit();
        }

        /// <summary> 
        /// 인트로 텍스트 대기 -> 3, 2, 1 카운트다운 -> 시작 알림 순으로 화면 전환을 동기화합니다. 
        /// </summary>
        private async UniTaskVoid SequenceAsync(CancellationToken token)
        {
            await ShowIntroTextAsync(token);
            await PlayCountdownAsync(token);
            await ShowStartTextAsync(token);

            CompleteStep();
        }
        
        /// <summary>
        /// 유저가 현재 단계를 인지할 수 있도록 인트로 텍스트를 보여준 뒤 페이드아웃 처리함.
        /// </summary>
        private async UniTask ShowIntroTextAsync(CancellationToken token)
        {
            if (descriptionText && _data?.introText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, _data.introText);
                UIFadeUtility.SetAlpha(descriptionText, 1f);
            }
    
            await UniTask.Delay(TimeSpan.FromSeconds(2.0), cancellationToken: token);
            await UIFadeUtility.FadeGraphicAsync(descriptionText, 1f, 0f, 1f, token);
        }

        /// <summary>
        /// 배열된 숫자를 순회하며 카운트다운 효과음과 함께 텍스트를 갱신함.
        /// </summary>
        private async UniTask PlayCountdownAsync(CancellationToken token)
        {
            if (SoundManager.Instance)
            {
                SoundManager.Instance.PlaySFX("공통_10_3초");
            }

            string[] counts = new string[] { "3", "2", "1" };
            TextSetting countSetting = _data?.countdownText;

            foreach (string count in counts)
            {
                UIFadeUtility.SetAlpha(descriptionText, 1f);
                UpdateCountdownText(count, countSetting);
                
                await UniTask.Delay(TimeSpan.FromSeconds(1.0), cancellationToken: token);
            }
        }

        /// <summary>
        /// 카운트다운 텍스트를 안전하게 교체하고 원본 데이터를 복구함.
        /// </summary>
        private void UpdateCountdownText(string count, TextSetting countSetting)
        {
            if (!descriptionText)
            {
                return;
            }

            if (countSetting != null)
            {
                // 원본 템플릿 보존을 위해 임시 할당 후 복구
                string originalText = countSetting.text; 
                countSetting.text = count;
                UIManager.Instance.SetText(descriptionText.gameObject, countSetting);
                countSetting.text = originalText; 
            }
            else
            {
                descriptionText.text = count;
            }
        }

        /// <summary>
        /// 카운트다운 완료 후 최종 시작 텍스트를 노출하고 효과음을 재생함.
        /// </summary>
        private async UniTask ShowStartTextAsync(CancellationToken token)
        {
            if (descriptionText && _data?.startText != null)
            {   
                // 사운드와 시각적 전환의 싱크를 맞추기 위한 미세 지연
                await UniTask.Delay(TimeSpan.FromSeconds(0.3), cancellationToken: token); 
                
                UIManager.Instance.SetText(descriptionText.gameObject, _data.startText);
                UIFadeUtility.SetAlpha(descriptionText, 1f); 
                
                if (SoundManager.Instance)
                {
                    SoundManager.Instance.PlaySFX("공통_14");
                }
            }
            
            await UniTask.Delay(TimeSpan.FromSeconds(1.0), cancellationToken: token); 
        }

        /// <summary> Color 구조체를 우회하여 텍스트의 투명도를 즉시 갱신합니다. </summary>
        private void SetTextAlpha(float alpha)
        {
            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = alpha;
                descriptionText.color = c;
            }
        }
    }
}