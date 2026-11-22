namespace ProjectNeo.Combat
{
    /// <summary>
    /// 원거리 사선 막힘 처리(COMBAT-12, 사용자 결정 2026-11-22, Q-60 A안). 살아 있는 적이 사선을 막는 규칙은 그대로 두고,
    /// 막혔을 때 근접전으로 뛰어들지 않게 한다.
    /// </summary>
    public static class RangedOcclusionRules
    {
        /// <summary>
        /// 다른 살아 있는 적이 막으면, 그 적이 사거리 안이고 쏠 수 있을 때 대상을 그 적으로 바꾸고 아니면 사거리를 지켜 기다린다.
        /// 구조물이 막으면 예전처럼 다가간다.
        /// </summary>
        public static RangedBlockAction Decide(ShotBlock block, bool blockerStrikeable) => block switch
        {
            ShotBlock.Clear => RangedBlockAction.Attack,
            ShotBlock.LivingEnemy => blockerStrikeable ? RangedBlockAction.SwitchToBlocker : RangedBlockAction.HoldDistance,
            _ => RangedBlockAction.CloseIn,
        };

        /// <summary>
        /// 몸 가운데와 위쪽 몸통 두 선의 결과를 합친다. 하나라도 열려 있으면 열림, 둘 다 구조물이 막으면 구조물,
        /// 그 밖(적만 막는 선이 있으면)은 적이 막은 것이다.
        /// </summary>
        public static ShotBlock Combine(ShotBlock center, ShotBlock upper)
        {
            if (center == ShotBlock.Clear || upper == ShotBlock.Clear) return ShotBlock.Clear;
            if (center == ShotBlock.Structure && upper == ShotBlock.Structure) return ShotBlock.Structure;
            return ShotBlock.LivingEnemy;
        }
    }
}
