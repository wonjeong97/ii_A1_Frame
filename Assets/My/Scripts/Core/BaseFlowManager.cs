using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts.Utils;
using UnityEngine;
using VContainer;
using ZLogger;

namespace My.Scripts.Core
{
    /// <summary> 
    /// 페이지 순차 진행 관리 부모 클래스.
    /// IPageFlowListener를 상속받아 가비지 라인을 완전히 제거했습니다.
    /// </summary>
    public abstract class BaseFlowManager : MonoBehaviour, IPageFlowListener
    {
        [Header("Base Pages")]
        [SerializeField] protected GamePage[] pages; 

        protected int currentPageIndex = -1; 
        protected bool isTransitioning; 
        protected CancellationTokenSource transitionCts; 
        protected ILogger<BaseFlowManager> _baseLogger;
        
        [Inject]
        public void ConstructBase(ILogger<BaseFlowManager> baseLogger)
        {
            _baseLogger = baseLogger;
        }

        protected virtual void Start()
        {
            LoadSettings(); 
            if (pages == null || pages.Length == 0)
            {
                Debug.LogWarning("[BaseFlowManager] pages 비어있음");
                return;
            }
            InitializePages(); 
            StartFlow(); 
        }

        protected virtual void OnDestroy()
        {
            CancelTransition();
        }

        protected void CancelTransition()
        {
            if (transitionCts != null)
            {
                transitionCts.Cancel();
                transitionCts.Dispose();
                transitionCts = null;
            }
        }

        protected abstract void LoadSettings();
        protected abstract void OnAllFinished();

        protected virtual void InitializePages()
        {
            if (pages == null) return;
            for (int i = 0; i < pages.Length; i++)
            {
                if (!pages[i]) continue;
                
                pages[i].gameObject.SetActive(false);
                pages[i].SetAlpha(0f);
                
                pages[i].SetFlowListener(this);
            }
        }

        protected virtual void StartFlow()
        {
            if (pages != null && pages.Length > 0)
            {
                _baseLogger?.ZLogInformation($"[{GetType().Name}] 플로우 시작 -> 첫 번째 페이지 진입 (Index: 0)");
                TransitionToPage(0);
            }
        }

        /// <summary>
        /// GamePage 인터페이스로부터 다이렉트로 호출되는 무결점 역추적 콜백부
        /// </summary>
        public void OnPageStepComplete(GamePage page, int triggerInfo)
        {
            // 현재 페이지의 배열 인덱스를 고속 역추적하여 안전하게 다음 단계 연산
            int currentIndex = Array.IndexOf(pages, page);
            if (currentIndex == -1 || currentIndex != currentPageIndex) return;

            _baseLogger?.ZLogInformation($"[{GetType().Name}] Page {currentIndex} 단계 완료 (Trigger Info: {triggerInfo})");

            int nextIndex = currentIndex + 1;
            OnPageComplete(currentIndex, nextIndex, triggerInfo);
        }

        protected virtual void OnPageComplete(int currentIndex, int nextIndex, int info)
        {
            if (nextIndex < pages.Length)
            {
                TransitionToPage(nextIndex, info);
            }
            else
            {
                _baseLogger?.ZLogInformation($"[{GetType().Name}] 모든 페이지 플로우 완료. OnAllFinished() 호출.");
                OnAllFinished();
            }
        }

        protected virtual void TransitionToPage(int targetIndex, int info = 0)
        {
            if (isTransitioning)
            {
                _baseLogger?.ZLogWarning($"[{GetType().Name}] 트랜지션 중단됨: 이미 전환 중 (Target: {targetIndex})");
                return;
            }
            if (pages == null || targetIndex < 0 || targetIndex >= pages.Length)
            {
                _baseLogger?.ZLogError($"[{GetType().Name}] 잘못된 인덱스 오류: {targetIndex}");
                return;
            }
            
            _baseLogger?.ZLogInformation($"[{GetType().Name}] 페이지 전환 트리거: 현재({currentPageIndex}) -> 타겟({targetIndex}) | 데이터: {info}");
            
            CancelTransition();
            transitionCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            
            TransitionAsync(targetIndex, info, transitionCts.Token).Forget();
        }

        protected virtual async UniTaskVoid TransitionAsync(int targetIndex, int info, CancellationToken token)
        {
            isTransitioning = true;
            try
            {
                if (currentPageIndex >= 0 && currentPageIndex < pages.Length)
                {
                    GamePage current = pages[currentPageIndex];
                    if (current)
                    {
                        await FadePageAsync(current, 1f, 0f, 0.5f, token);
                        current.OnExit();
                    }
                }
                
                currentPageIndex = targetIndex;
                GamePage next = pages[targetIndex];
                if (next)
                {
                    next.OnEnter();
                    await FadePageAsync(next, 0f, 1f, 0.5f, token);
                }
            }
            catch (OperationCanceledException)
            {
                _baseLogger?.ZLogWarning($"[{GetType().Name}] 비동기 페이지 전환 취소됨: {targetIndex}");
            }
            catch (Exception e)
            {
                _baseLogger?.ZLogError(e, $"[{GetType().Name}] 전환 중 예외 발생: {e.Message}");
            }
            finally
            {
                isTransitioning = false;
                _baseLogger?.ZLogInformation($"[{GetType().Name}] 전환 종료 (isTransitioning = false)");
            }
        }

        protected async UniTask FadePageAsync(GamePage page, float start, float end, float duration, CancellationToken token)
        {
            if (!page) return;

            if (page.TryGetComponent(out CanvasGroup cg))
            {
                await cg.FadeAsync(start, end, duration, token);
            }
            else
            {
                page.SetAlpha(end);
            }
        }
    }
}