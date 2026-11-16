namespace ProjectNeo.Combat
{
    /// <summary>행동의 피격 중단 가능 여부와 강인도(Poise) 수치.</summary>
    public readonly struct ActionPoise
    {
        public readonly bool IsInterruptible;
        public readonly float Poise;

        ActionPoise(bool isInterruptible, float poise)
        {
            IsInterruptible = isInterruptible;
            Poise = poise < 0f ? 0f : poise;
        }

        public static ActionPoise Uninterruptible => new ActionPoise(false, float.PositiveInfinity);

        public static ActionPoise Interruptible(float poise) => new ActionPoise(true, poise);
    }
}
