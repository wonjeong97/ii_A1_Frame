using System.Collections;
using UnityEngine;

namespace My.Scripts.Core
{
    /// <summary> 페이지 순차 진행 관리 부모 클래스 </summary>
    public abstract class BaseFlowManager : MonoBehaviour
    {
        [Header("Base Pages")]
        [SerializeField] protected GamePage[] pages; // 진행될 페이지 리스트

        protected int currentPageIndex = -1; // 현재 페이지 인덱스
        protected bool isTransitioning = false; // 전환 연출 진행 여부

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
                if (pages[i] == null) continue;
                
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
            isTransitioning = true;
            StartCoroutine(TransitionRoutine(targetIndex, info));
        }

        /// <summary> 페이지 전환 연출 (Fade Out -> Fade In) </summary>
        protected virtual IEnumerator TransitionRoutine(int targetIndex, int info)
        {
            try
            {
                // 1. 현재 페이지 퇴장 (있다면)
                if (currentPageIndex >= 0 && currentPageIndex < pages.Length)
                {
                    var current = pages[currentPageIndex];
                    if (current != null)
                    {
                        yield return StartCoroutine(FadePage(current, 1f, 0f));
                        current.OnExit();
                    }
                }
                // 2. 다음 페이지 준비
                currentPageIndex = targetIndex;
                var next = pages[targetIndex];
                if (next != null)
                {
                    next.OnEnter(); // 활성화 및 초기화
                    
                    // 3. 다음 페이지 등장
                    yield return StartCoroutine(FadePage(next, 0f, 1f));
                }
            }
            finally
            {
                isTransitioning = false;
            }
        }

        /// <summary> 페이지 투명도 조절 코루틴 </summary>
        protected IEnumerator FadePage(GamePage page, float start, float end, float duration = 0.5f)
        {
            if (!page) yield break;
            if (duration <= 0f)
            {
                page.SetAlpha(end);
                yield break;
            }
            
            float t = 0f;
            page.SetAlpha(start);
            while (t < duration)
            {
                t += Time.deltaTime;
                page.SetAlpha(Mathf.Lerp(start, end, t / duration));
                yield return null;
            }
            page.SetAlpha(end);
        }
    }
}