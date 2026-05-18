using UnityEngine;
using VContainer;
using VContainer.Unity;
using My.Scripts._00_Title;

namespace My.Scripts.App
{
    /// <summary>
    /// Title 씬 전용 스코프. 
    /// TitleManager에 전역 매니저(GameManager, SoundManager 등)를 주입합니다.
    /// </summary>
    public class TitleLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // 타이틀 매니저 등록
            builder.RegisterComponentInHierarchy<TitleManager>();
        }
    }
}