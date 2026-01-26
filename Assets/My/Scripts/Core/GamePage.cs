using System;
using UnityEngine;

namespace My.Scripts.Core
{
    // 매니저가 배열로 관리하기 위한 기본 클래스
    public abstract class GamePage : MonoBehaviour
    {
        public Action<int> onStepComplete;
        protected CanvasGroup canvasGroup;

        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public abstract void SetupData(object data); // 매니저용 (타입 모름)

        public virtual void OnEnter() 
        { 
            gameObject.SetActive(true);
            SetAlpha(1f);
        }

        public virtual void OnExit() 
        { 
            gameObject.SetActive(false); 
        }

        public void SetAlpha(float alpha)
        {
            if (canvasGroup) canvasGroup.alpha = alpha;
        }

        protected void CompleteStep(int triggerInfo = 0)
        {
            onStepComplete?.Invoke(triggerInfo);
        }
    }

    // 제네릭 버전: 각 페이지 컨트롤러는 이것을 상속받음
    public abstract class GamePage<T> : GamePage where T : class
    {
        // 매니저가 호출하는 object 버전을 받아서 -> T 타입으로 안전하게 변환 후 전달
        public sealed override void SetupData(object data)
        {
            if (data is T typedData)
            {
                SetupData(typedData);
            }
            // 데이터가 null이거나 타입이 안 맞으면 무시
        }

        // 자식 클래스가 실제로 구현할 메서드 (T 타입 데이터가 바로 들어옴)
        protected abstract void SetupData(T data);
    }
}