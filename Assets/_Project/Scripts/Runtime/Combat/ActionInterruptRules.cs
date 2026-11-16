namespace ProjectNeo.Combat
{
    /// <summary>단발 임계치 기반 피격 중단 판정 규칙.</summary>
    public static class ActionInterruptRules
    {
        public static bool ShouldInterrupt(ActionPoise action, float incomingStagger)
            => action.IsInterruptible && incomingStagger >= action.Poise;
    }
}
