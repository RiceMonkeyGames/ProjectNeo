using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 적 근접 유닛이 수성 무기(대포)를 칠 수 있는지 판정한다(SIEGE-02). 무기는 통로를 막지 않도록 충돌체가 없어서
    /// 무기 정의의 크기로 몸통을 대신한다.
    /// </summary>
    public static class SiegeWeaponStrikeRules
    {
        /// <summary>공격 지점에서 무기 중심까지 수평 거리가 사거리 + 무기 반 폭 이내이고 높이 차가 무기 높이 이내면 닿는다.</summary>
        public static bool InReach(Vector3 attacker, Vector3 weaponBase, Vector3 weaponSize, float range)
        {
            Vector3 offset = weaponBase - attacker;
            float height = offset.y;
            offset.y = 0f;
            float halfWidth = Mathf.Max(weaponSize.x, weaponSize.z) * 0.5f;
            float reach = Mathf.Max(0f, range) + halfWidth;
            return offset.sqrMagnitude <= reach * reach && Mathf.Abs(height) <= Mathf.Max(weaponSize.y, 0f) + Mathf.Max(0f, range);
        }
    }
}
