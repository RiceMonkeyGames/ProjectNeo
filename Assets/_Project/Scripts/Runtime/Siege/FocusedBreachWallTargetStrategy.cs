using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 여러 성벽 파괴병이 한 곳을 빨리 뚫도록 고르는 전략(SIEGE-34). 점수 = 거리 − 잃은 체력 비율 × <c>damageBonus</c>
    /// − 이미 치고 있는 다른 파괴병 수(최대 <c>engagedCap</c>) × <c>engagedBonus</c>. 낮을수록 좋고, 같으면 배치 번호가 작은 쪽.
    /// 다친 곳도 치는 동료도 없으면 가장 가까운 성벽을 고르는 것과 같다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Siege/Focused Breach Wall Target")]
    public sealed class FocusedBreachWallTargetStrategy : WallTargetStrategy
    {
        [SerializeField, Min(0f)] float damageBonus = 15f;
        [SerializeField, Min(0f)] float engagedBonus = 8f;
        [SerializeField, Range(0, 10)] int engagedCap = 3;
        public float DamageBonus => damageBonus;
        public float EngagedBonus => engagedBonus;
        public int EngagedCap => engagedCap;

        public float Score(WallTargetCandidate candidate)
        {
            float distance = Mathf.Sqrt(candidate.DistanceSquared);
            int engaged = Mathf.Min(candidate.EngagedBreakers, engagedCap);
            return distance - (1f - candidate.HealthRatio) * damageBonus - engaged * engagedBonus;
        }

        public override int Select(IReadOnlyList<WallTargetCandidate> candidates)
        {
            int best = -1;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                float distance = candidates[i].DistanceSquared;
                if (float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0f) continue;
                float score = Score(candidates[i]);
                if (best < 0 || score < bestScore
                    || (score == bestScore && candidates[i].StructureId < candidates[best].StructureId))
                {
                    best = i;
                    bestScore = score;
                }
            }
            return best;
        }

        public static FocusedBreachWallTargetStrategy Create(float damageBonus = 15f, float engagedBonus = 8f, int engagedCap = 3)
        {
            var value = CreateInstance<FocusedBreachWallTargetStrategy>();
            value.damageBonus = Mathf.Max(0f, damageBonus);
            value.engagedBonus = Mathf.Max(0f, engagedBonus);
            value.engagedCap = Mathf.Clamp(engagedCap, 0, 10);
            return value;
        }
    }
}
