using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 마치콜레이션에서 아래를 공격하는 방식 하나(SIEGE-12, 사용자 결정 2026-11-22). 데이터로 골라 끼우는 모듈이며,
    /// 구멍(<see cref="ShotOpening"/>)은 그대로 두고 방식만 더한다. 방식은 공격자 종류로 자동으로 정해진다(고르는 화면 없음).
    /// 지금은 화살(<see cref="ArrowMachicolationAttack"/>)만 있다. 돌 떨어뜨리기·끓는 기름은 나중에 이 형태로 더한다.
    /// </summary>
    public abstract class MachicolationAttack : ScriptableObject
    {
        /// <summary>이 공격자가 이 방식으로 구멍을 쓸 수 있는지.</summary>
        public abstract bool CanUse(CharacterActor attacker);
    }
}
