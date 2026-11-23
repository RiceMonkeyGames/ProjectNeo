using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 성벽 위 아군 중 가장 가까운 대상을 먼저(Q-43), 없으면 가장 가까운 수성 무기를(SIEGE-16), 그것도 없으면 남은 아군 중
    /// 가장 가까운 대상을 고른다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Siege/Flyer Target - Wall Top First")]
    public sealed class WallTopFirstFlyerTargetStrategy : FlyerTargetStrategy
    {
        public override int Select(IReadOnlyList<FlyerTargetCandidate> candidates)
        {
            int best = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                FlyerTargetCandidate candidate = candidates[i];
                float distance = candidate.DistanceSquared;
                if (float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0f) continue;
                if (best < 0 || Better(candidate, candidates[best])) best = i;
            }
            return best;
        }

        // 성벽 위 아군 0, 수성 무기 1, 그 밖의 아군 2 순서로 앞선다.
        static int Rank(FlyerTargetCandidate candidate) =>
            candidate.IsSiegeWeapon ? 1 : candidate.OnWallTop ? 0 : 2;

        static bool Better(FlyerTargetCandidate a, FlyerTargetCandidate b)
        {
            int rankA = Rank(a), rankB = Rank(b);
            if (rankA != rankB) return rankA < rankB;
            if (a.DistanceSquared != b.DistanceSquared) return a.DistanceSquared < b.DistanceSquared;
            return a.Id < b.Id;
        }

        public static WallTopFirstFlyerTargetStrategy Create() => CreateInstance<WallTopFirstFlyerTargetStrategy>();
    }
}
