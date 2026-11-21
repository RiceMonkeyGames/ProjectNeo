namespace ProjectNeo.Combat
{
    /// <summary>
    /// 성가퀴 뒤 웅크려 재장전하기 규칙(COMBAT-10). 씬을 알지 않는다.
    /// 쏜 직후부터 다음 발사 가능 시점 <c>riseLead</c>초 전까지 받침 뒤에 웅크리고, 그 뒤에는 일어나 겨누고 쏜다.
    /// 쏘는 중(판정 대기)이거나 움직이거나 사다리 위면 서 있는다. 대상이 없어 쿨다운이 끝나 있으면 서서 지켜본다.
    /// </summary>
    public static class CoverCrouchRules
    {
        public static bool ShouldCrouch(bool behindCover, bool attacking, bool moving, float cooldownRemaining, float riseLead) =>
            behindCover && !attacking && !moving && cooldownRemaining > riseLead;
    }
}
