namespace My.Scripts.Core
{
    /// <summary>
    /// 이전 페이지나 매니저로부터 상태값(트리거)을 전달받기 위한 공용 인터페이스입니다.
    /// </summary>
    public interface ITriggerReceiver
    {
        void ReceiveTrigger(int triggerInfo);
    }
}