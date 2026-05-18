using My.Scripts._01_Tutorial;
using My.Scripts.Core;
using My.Scripts.Global;
using VContainer;
using VContainer.Unity;

namespace My.Scripts.App
{
    /// <summary>
    /// 튜토리얼 씬 내부의 컴포넌트들을 전역 컨테이너(Parent)와 연결하고 의존성을 주입하는 씬 전용 스코프.
    /// </summary>
    public class TutorialLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            
            builder.RegisterComponentInHierarchy<TutorialManager>();
            builder.RegisterComponentInHierarchy<APIManager>();
        }
    }
}