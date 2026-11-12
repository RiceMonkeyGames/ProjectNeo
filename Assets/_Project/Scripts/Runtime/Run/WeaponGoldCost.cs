using System;
using ProjectNeo.Walls;

namespace ProjectNeo.Run
{
    /// <summary>수성 무기 한 종류를 설치할 때 드는 골드.</summary>
    [Serializable]
    public struct WeaponGoldCost
    {
        public SiegeWeaponDefinition weapon;
        public int gold;

        public WeaponGoldCost(SiegeWeaponDefinition weapon, int gold) { this.weapon = weapon; this.gold = gold; }
    }
}
