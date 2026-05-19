using UnityEngine;
using VContainer;
using VContainer.Unity;
using My.Scripts.Core;
using My.Scripts.Core.Data;

namespace My.Scripts.App
{
    /// <summary>
    /// Play 관련 씬(Tutorial, Q1~Q15)에서 공통으로 사용되는 씬 전용 스코프.
    /// LevelManager와 LevelDataLoader의 의존성을 묶어줍니다.
    /// </summary>
    public class PlayLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // 1. 씬 하이어라키에 배치된 LevelManager를 찾아 의존성 주입
            builder.RegisterComponentInHierarchy<LevelManager>();
            
            // 2. LevelDataLoader는 하이어라키에 없는 순수 C# 클래스이므로 수동으로 등록.
            builder.Register<LevelDataLoader>(Lifetime.Scoped);
        }
    }
}