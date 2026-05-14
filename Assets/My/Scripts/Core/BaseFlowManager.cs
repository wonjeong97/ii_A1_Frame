using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace My.Scripts.Core
{
    /// <summary> 페이지 순차 진행 관리 부모 클래스 </summary>
    public abstract class BaseFlowManager : MonoBehaviour
    {
        [Header("Base Pages")]
        [SerializeField] protected GamePage[] pages; // 진행될 페이지 리스트

        protected int currentPageIndex = -1; // 현재 페이지 인덱스
        protected bool isTransitioning; // 전환 연출 진행 여부
        protected CancellationTokenSource transitionCts; // 비동기 전환 취소 토큰

        protected virtual void Start()
        {
            LoadSettings(); // 1. 데이터 로드 
            if (pages == null || pages.Length == 0)
            {
                Debug.LogWarning("[BaseFlowManager] pages 비어있음");
                return;
            }
            InitializePages(); // 2. 페이지 초기화
            StartFlow(); // 3. 흐름 시작
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

        /// <summary> 데이터 로드 (자식 구현) </summary>
        protected abstract void LoadSettings();

        /// <summary> 모든 페이지 완료 시 호출 (자식 구현) </summary>
        protected abstract void OnAllFinished();

        /// <summary> 페이지 초기화 및 이벤트 연결 </summary>
        protected virtual void InitializePages()
        {
            if (pages == null) return;
            for (int i = 0; i < pages.Length; i++)
            {
                if (!pages[i]) continue;
                
                // 초기 상태: 비활성화 및 투명
                pages[i].gameObject.SetActive(false);
                pages[i].SetAlpha(0f);
                
                // 이벤트 연결: 현재 페이지가 끝나면 -> OnPageComplete 호출
                int currentIndex = i;
                int nextIndex = i + 1;
                
                // 기존 구독 해제 (중복 방지)
                pages[i].onStepComplete = null; 
                pages[i].onStepComplete += (info) => OnPageComplete(currentIndex, nextIndex, info);
            }
        }

        /// <summary> 첫 페이지 진입 </summary>
        protected virtual void StartFlow()
        {
            if (pages != null && pages.Length > 0)
            {
                TransitionToPage(0);
            }
        }

        /// <summary> 페이지 완료 처리 (다음 이동 또는 종료) </summary>
        protected virtual void OnPageComplete(int currentIndex, int nextIndex, int info)
        {
            if (nextIndex < pages.Length)
            {
                TransitionToPage(nextIndex, info);
            }
            else
            {
                OnAllFinished();
            }
        }

        /// <summary> 특정 페이지로 전환 요청 </summary>
        protected virtual void TransitionToPage(int targetIndex, int info = 0)
        {
            if (isTransitioning) return;
            if (pages == null || targetIndex < 0 || targetIndex >= pages.Length)
            {
                Debug.LogWarning($"[BaseFlowManager] 잘못된 인덱스: {targetIndex}");
                return;
            }
            
            CancelTransition();
            transitionCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            
            TransitionAsync(targetIndex, info, transitionCts.Token).Forget();
        }

        /// <summary> 
        /// 페이지 전환 연출 (Fade Out -> Fade In) 
        /// IEnumerator 대신 UniTask를 사용하여 가비지 할당(GC)을 제거함.
        /// </summary>
        protected virtual async UniTaskVoid TransitionAsync(int targetIndex, int info, CancellationToken token)
        {
            isTransitioning = true;
            try
            {
                // 1. 현재 페이지 퇴장 (있다면)
                if (currentPageIndex >= 0 && currentPageIndex < pages.Length)
                {
                    GamePage current = pages[currentPageIndex];
                    if (current)
                    {
                        await FadePageAsync(current, 1f, 0f, 0.5f, token);
                        current.OnExit();
                    }
                }
                
                // 2. 다음 페이지 준비
                currentPageIndex = targetIndex;
                GamePage next = pages[targetIndex];
                if (next)
                {
                    next.OnEnter(); // 활성화 및 초기화
                    
                    // 3. 다음 페이지 등장
                    await FadePageAsync(next, 0f, 1f, 0.5f, token);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                isTransitioning = false;
            }
        }

        /// <summary> 페이지 투명도 조절 </summary>
        protected async UniTask FadePageAsync(GamePage page, float start, float end, float duration, CancellationToken token)
        {
            if (!page) return;
            if (duration <= 0f)
            {
                page.SetAlpha(end);
                return;
            }
            
            float t = 0f;
            page.SetAlpha(start);
            while (t < duration)
            {
                t += Time.deltaTime;
                page.SetAlpha(Mathf.Lerp(start, end, t / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            page.SetAlpha(end);
        }
    }
}