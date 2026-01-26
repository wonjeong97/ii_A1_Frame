using System.Collections;
using UnityEngine;

namespace My.Scripts.Core
{
    public abstract class BaseFlowManager : MonoBehaviour
    {
        [Header("Base Pages")]
        [SerializeField] protected GamePage[] pages;

        protected int currentPageIndex = -1;
        protected bool isTransitioning = false;

        protected virtual void Start()
        {
            LoadSettings(); // 1. 데이터 로드 
            InitializePages(); // 2. 페이지 초기화
            StartFlow(); // 3. 흐름 시작
        }

        protected abstract void LoadSettings();

        protected abstract void OnAllFinished();

        // 페이지 초기화 및 이벤트 연결
        protected virtual void InitializePages()
        {
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

        protected virtual void StartFlow()
        {
            if (pages.Length > 0)
            {
                TransitionToPage(0);
            }
        }

        // 페이지 완료 시 호출되는 로직
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

        // 페이지 전환 로직 (기본 페이드)
        protected virtual void TransitionToPage(int targetIndex, int info = 0)
        {
            if (isTransitioning) return;
            StartCoroutine(TransitionRoutine(targetIndex, info));
        }

        protected virtual IEnumerator TransitionRoutine(int targetIndex, int info)
        {
            isTransitioning = true;

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

            isTransitioning = false;
        }

        // 공용 페이드 유틸리티
        protected IEnumerator FadePage(GamePage page, float start, float end, float duration = 0.5f)
        {
            if (!page) yield break;
            
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