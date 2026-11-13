using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>공중 대상에 대한 근접 사거리 규칙. 씬을 알지 않는다.</summary>
    public static class AirborneReach
    {
        /// <summary>
        /// 근접 공격자가 공중 대상에 닿는지. 대상이 낮게 떠 있을 때만, 공격자 발에서 대상의 바닥 투영점까지
        /// 거리가 사거리 + 대상 반지름 이내면 닿는다. 다른 층(성벽 위·아래)이면 높이 차이만큼 멀어진다.
        /// </summary>
        public static bool InMeleeReach(Vector3 attackerFeet, Vector3 targetGround, bool exposed, float range, float targetRadius)
        {
            if (!exposed || float.IsNaN(range) || range < 0f) return false;
            float reach = range + Mathf.Max(0f, targetRadius);
            return (targetGround - attackerFeet).sqrMagnitude <= reach * reach;
        }

        /// <summary>근접 공격자가 지금 노릴 수 있는 공중 대상인지. 높이 떠 있으면 노리지 않는다.</summary>
        public static bool CanMeleeTarget(bool hasGround, bool exposed) => hasGround && exposed;
    }
}
