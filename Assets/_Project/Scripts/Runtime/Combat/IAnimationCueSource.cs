namespace ProjectNeo.Combat
{
    /// <summary>
    /// 캐릭터 모델 애니메이션(<see cref="CharacterModelAnimator"/>)에 행동 상태를 알려 주는 두뇌(ART-04).
    /// 없으면 <see cref="UnitCombatAI.Target"/>과 공격 신호만으로 자세를 정한다.
    /// </summary>
    public interface IAnimationCueSource
    {
        /// <summary>겨누는 대상. 없으면 null(그때는 UnitCombatAI의 대상을 쓴다).</summary>
        CharacterActor AimTarget { get; }

        /// <summary>쏠 곳이 없어도 활을 당긴 채 기다려야 하는지.</summary>
        bool HoldsDrawnBow { get; }
    }
}
