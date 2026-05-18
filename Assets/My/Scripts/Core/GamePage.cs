using UnityEngine;
using VContainer;      
using Wonjeong.UI;  

namespace My.Scripts.Core
{
    public interface IPageFlowListener
    {
        void OnPageStepComplete(GamePage page, int triggerInfo);
    }

    public abstract class GamePage : MonoBehaviour
    {
        private IPageFlowListener _flowListener; 
        protected CanvasGroup canvasGroup; 
        protected UIManager _uiManager; 

        // [최적화 완료] VContainer가 상속 구조를 따라 최상위 부모인 이 메서드를 최우선 자동 실행합니다.
        [Inject]
        public void InjectUIManager(UIManager uiManager)
        {
            _uiManager = uiManager;
        }

        protected virtual void Awake()
        {
            if (!TryGetComponent(out canvasGroup))
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

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

        public void SetFlowListener(IPageFlowListener listener)
        {
            _flowListener = listener;
        }

        protected void CompleteStep(int triggerInfo = 0)
        {
            if (_flowListener != null)
            {
                _flowListener.OnPageStepComplete(this, triggerInfo);
            }
        }

        public virtual object ExtractCurrentData() => null;
        
        protected virtual void OnDestroy()
        {
        }
    }

    public abstract class GamePage<T> : GamePage where T : class
    {
        public sealed override void SetupData(object data)
        {
            if (data is T typedData)
            {
                SetupData(typedData);
            }
        }

        protected abstract void SetupData(T data);
    }
}