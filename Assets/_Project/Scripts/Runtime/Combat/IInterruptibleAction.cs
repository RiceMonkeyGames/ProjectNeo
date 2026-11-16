namespace ProjectNeo.Combat
{
    /// <summary>피격 경직도 비교로 중단될 수 있는 행동 계약.</summary>
    public interface IInterruptibleAction
    {
        ActionPoise InterruptPoise { get; }
        void InterruptAction();
    }
}
