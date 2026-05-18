using UnityEngine;
using VContainer;
using VContainer.Unity;
using My.Scripts._18_Ending;

namespace My.Scripts.App
{
    /// <summary>
    /// Ending 씬 전용 스코프.
    /// EndingManager가 하위 페이지들에 UI/Sound 매니저를 꽂아줄 수 있도록 권한을 넘겨줍니다.
    /// </summary>
    public class EndingLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // 엔딩 매니저 등록
            builder.RegisterComponentInHierarchy<EndingManager>();
        }
    }
}