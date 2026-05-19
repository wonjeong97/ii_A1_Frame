using System;
using My.Scripts.Global;
using My.Scripts.Hardware;
using My.Scripts.Timelapse;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Wonjeong.App;
using Wonjeong.UI;

namespace My.Scripts.App
{
    /// <summary>
    /// 프로젝트 전역의 생명주기와 의존성을 관리하는 최상위 스코프.
    /// 기존 싱글톤 패턴을 대체하여 7개의 핵심 매니저를 VContainer에 등록함.
    /// </summary>
    public class GameLifetimeScope : RootLifetimeScope
    {
        [Header("Global Managers")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private SessionManager sessionManager;
        [SerializeField] private ArduinoManager arduinoManager;
        [SerializeField] private HueManager hueManager;
        [SerializeField] private TimeLapseRecorder timeLapseRecorder;
        [SerializeField] private SoundManager soundManager;
        [SerializeField] private FadeManager fadeManager;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private VideoManager videoManager;

        protected override void Configure(IContainerBuilder builder)
        {
            // 1. ZLogger 및 MessagePipe 초기화 상속
            base.Configure(builder);

            // 2. 핵심 매니저를 컨테이너에 등록 (싱글톤 수명)
            if (!gameManager) throw new InvalidOperationException("GameLifetimeScope.gameManager is required.");
            if (!sessionManager) throw new InvalidOperationException("GameLifetimeScope.sessionManager is required.");
            if (!arduinoManager) throw new InvalidOperationException("GameLifetimeScope.arduinoManager is required.");
            if (!hueManager) throw new InvalidOperationException("GameLifetimeScope.hueManager is required.");
            if (!timeLapseRecorder) throw new InvalidOperationException("GameLifetimeScope.timeLapseRecorder is required.");
            if (!soundManager) throw new InvalidOperationException("GameLifetimeScope.soundManager is required.");
            if (!fadeManager) throw new InvalidOperationException("GameLifetimeScope.fadeManager is required.");
            if (!uiManager) throw new InvalidOperationException("GameLifetimeScope.uiManager is required.");
            if (!videoManager) throw new InvalidOperationException("GameLifetimeScope.videoManager is required.");
            
            builder.RegisterComponent(gameManager);
            builder.RegisterComponent(sessionManager);
            builder.RegisterComponent(arduinoManager);
            builder.RegisterComponent(hueManager);
            builder.RegisterComponent(timeLapseRecorder);
            builder.RegisterComponent(soundManager);
            builder.RegisterComponent(fadeManager);
            builder.RegisterComponent(uiManager);
            builder.RegisterComponent(videoManager);
        }
    }
}