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
    /// 정적 배열 캐싱 및 밀리초 엔진 동기화 처리가 완료된 무결점 최종본입니다.
    /// </summary>
    public class Page_Tutorial8 : GamePage<TutorialPage8Data>
    {
        [Header("UI References")]
        [SerializeField] private Text descriptionText; 

        private TutorialPage8Data _data;
        private CancellationTokenSource _sequenceCts;

        // [최적화 핵심] 매번 힙 할당을 유발하던 카운트다운 배열을 정적 메모리 영역에 단 1회만 캐싱 (런타임 가비지 0B)
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

            if (_soundManager != null)
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
            if (descriptionText && _data?.introText != null && _uiManager != null)
            {
                _uiManager.SetText(descriptionText.gameObject, _data.introText);
                descriptionText.SetAlpha(1f);
            }
    
            // [최적화 핵심] 구조체 변환 비용이 없는 정수형 밀리초(2000ms) 다이렉트 타이머 연동
            await UniTask.Delay(2000, ignoreTimeScale: true, cancellationToken: token);
            
            if (descriptionText)
            {
                await descriptionText.FadeAsync(1f, 0f, 1f, token);
            }
        }

        private async UniTask PlayCountdownAsync(CancellationToken token)
        {
            if (_soundManager != null)
            {
                _soundManager.PlaySFX("공통_10_3초");
            }

            TextSetting countSetting = _data?.countdownText;

            // 정적 static 배열 순회 방식으로 변경 (가비지 발생 차단)
            foreach (string count in CountdownCounts)
            {
                if (descriptionText) descriptionText.SetAlpha(1f);
                UpdateCountdownText(count, countSetting);
                
                // 정수형 밀리초(1000ms) 대기 파이프라인 연결
                await UniTask.Delay(1000, ignoreTimeScale: true, cancellationToken: token);
            }
        }

        private void UpdateCountdownText(string count, TextSetting countSetting)
        {
            if (!descriptionText) return;

            if (countSetting == null || _uiManager == null)
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
                // 정수형 밀리초(300ms) 변환 최적화 적용
                await UniTask.Delay(300, ignoreTimeScale: true, cancellationToken: token); 
                
                if (_uiManager != null) _uiManager.SetText(descriptionText.gameObject, _data.startText);
                descriptionText.SetAlpha(1f); 
                
                if (_soundManager != null)
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