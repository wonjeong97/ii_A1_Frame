using System;
using UnityEngine;

namespace My.Scripts._02_Play
{

    
    public abstract class PlayPageBase : MonoBehaviour
    {
        public Action<int> OnStepComplete;

        protected CanvasGroup canvasGroup;

        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        public abstract void SetupData(object data);

        public virtual void OnEnter()
        {
            gameObject.SetActive(true);
        }

        public virtual void OnExit()
        {
            gameObject.SetActive(false);
        }

        public void SetAlpha(float alpha)
        {
            if (canvasGroup != null) canvasGroup.alpha = alpha;
        }

        protected void CompleteStep(int triggerInfo = 0)
        {
            OnStepComplete?.Invoke(triggerInfo);
        }
    }
}