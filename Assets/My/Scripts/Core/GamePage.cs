using System;
using UnityEngine;

namespace My.Scripts.Core
{
    public abstract class GamePage : MonoBehaviour
    {
        public Action<int> onStepComplete;
        protected CanvasGroup canvasGroup;

        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // 제네릭 데이터를 유연하게 받기 위해 object 타입 사용
        public abstract void SetupData(object data);

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
}