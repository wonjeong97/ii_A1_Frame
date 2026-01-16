using System;
using UnityEngine;

namespace My.Scripts._01_Tutorial
{
    // [공통 부모 클래스]
    public abstract class TutorialPageBase : MonoBehaviour
    {
        [Header("Base Components")]
        [SerializeField] protected CanvasGroup canvasGroup;

        // 페이지 완료 이벤트 (int 매개변수: 0=기본, 1=PlayerA, 2=PlayerB 등 트리거 정보 전달용)
        public event Action<int> OnStepComplete;

        // 1. 페이지 진입
        public virtual void OnEnter()
        {
            gameObject.SetActive(true);
            if (canvasGroup) canvasGroup.alpha = 0f; // 초기화
        }

        // 2. 페이지 퇴장
        public virtual void OnExit()
        {
            gameObject.SetActive(false);
        }

        // 3. 데이터 세팅 (자식에서 구현)
        public abstract void SetupData(object data);

        // 4. 투명도 조절 (Manager의 페이드 효과용)
        public void SetAlpha(float alpha)
        {
            if (canvasGroup) canvasGroup.alpha = alpha;
        }

        // 5. 완료 신호 보내기
        protected void CompleteStep(int triggerInfo = 0)
        {
            OnStepComplete?.Invoke(triggerInfo);
        }
    }
}