using System;
using UnityEngine;

namespace My.Scripts.Core
{
    /// <summary> 모든 페이지 컨트롤러의 최상위 부모 </summary>
    public abstract class GamePage : MonoBehaviour
    {
        public Action<int> onStepComplete; // 단계 완료 이벤트 (int: 트리거 정보)
        protected CanvasGroup canvasGroup; // 투명도 조절 컴포넌트

        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary> 데이터 설정 (타입 미정) </summary>
        public abstract void SetupData(object data);

        /// <summary> 진입 (활성화) </summary>
        public virtual void OnEnter() 
        { 
            gameObject.SetActive(true);
            SetAlpha(1f);
        }

        /// <summary> 퇴장 (비활성화) </summary>
        public virtual void OnExit() 
        { 
            gameObject.SetActive(false); 
        }

        /// <summary> 투명도 설정 </summary>
        public void SetAlpha(float alpha)
        {
            if (canvasGroup) canvasGroup.alpha = alpha;
        }

        /// <summary> 완료 신호 전송 </summary>
        protected void CompleteStep(int triggerInfo = 0)
        {
            onStepComplete?.Invoke(triggerInfo);
        }
    }

    /// <summary> 제네릭 데이터 페이지 부모 (타입 안전) </summary>
    public abstract class GamePage<T> : GamePage where T : class
    {
        /// <summary> 타입 안전 데이터 주입 (매니저 호출용) </summary>
        public sealed override void SetupData(object data)
        {
            if (data is T typedData)
            {
                SetupData(typedData);
            }
        }

        /// <summary> 실제 데이터 설정 구현 (자식용) </summary>
        protected abstract void SetupData(T data);
    }
}