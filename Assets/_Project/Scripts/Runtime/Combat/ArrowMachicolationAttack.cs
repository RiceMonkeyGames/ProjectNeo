using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 구멍으로 화살을 곧장 아래로 쏜다(SIEGE-12). 원거리 공격자만 쓴다. 따로 공격을 만들지 않고 평소 화살·피해 규칙을 그대로 쓰며,
    /// 사격 자리에 서 있는 동안 구멍 통로 안의 성문 조각이 사선과 화살을 막지 않을 뿐이다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Combat/Machicolation Arrow Attack")]
    public sealed class ArrowMachicolationAttack : MachicolationAttack
    {
        public override bool CanUse(CharacterActor attacker) =>
            attacker != null && attacker.Profile != null && attacker.Profile.AttackKind == AttackKind.Projectile;

        public static ArrowMachicolationAttack Create() => CreateInstance<ArrowMachicolationAttack>();
    }
}
