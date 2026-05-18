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
            if (gameManager) builder.RegisterComponent(gameManager);
            if (sessionManager) builder.RegisterComponent(sessionManager);
            if (arduinoManager) builder.RegisterComponent(arduinoManager);
            if (hueManager) builder.RegisterComponent(hueManager);
            if (timeLapseRecorder) builder.RegisterComponent(timeLapseRecorder);
            if (soundManager) builder.RegisterComponent(soundManager);
            if (fadeManager) builder.RegisterComponent(fadeManager);
            if (uiManager) builder.RegisterComponent(uiManager);
            if (videoManager) builder.RegisterComponent(videoManager);
        }
    }
}