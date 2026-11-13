using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>성벽 위 아군 중 가장 가까운 대상을 먼저, 없으면 남은 아군 중 가장 가까운 대상을 고른다(Q-43).</summary>
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

        static bool Better(FlyerTargetCandidate a, FlyerTargetCandidate b)
        {
            if (a.OnWallTop != b.OnWallTop) return a.OnWallTop;
            if (a.DistanceSquared != b.DistanceSquared) return a.DistanceSquared < b.DistanceSquared;
            return a.Id < b.Id;
        }

        public static WallTopFirstFlyerTargetStrategy Create() => CreateInstance<WallTopFirstFlyerTargetStrategy>();
    }
}
