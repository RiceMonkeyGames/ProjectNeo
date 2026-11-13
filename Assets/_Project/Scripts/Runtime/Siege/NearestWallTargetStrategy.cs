using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Siege
{
    [CreateAssetMenu(menuName = "ProjectNeo/Siege/Nearest Wall Target")]
    public sealed class NearestWallTargetStrategy : WallTargetStrategy
    {
        public override int Select(IReadOnlyList<WallTargetCandidate> candidates)
        {
            int best = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                float distance = candidates[i].DistanceSquared;
                if (float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0f) continue;
                if (best < 0 || distance < candidates[best].DistanceSquared
                    || (distance == candidates[best].DistanceSquared && candidates[i].StructureId < candidates[best].StructureId))
                    best = i;
            }
            return best;
        }

        public static NearestWallTargetStrategy Create() => CreateInstance<NearestWallTargetStrategy>();
    }
}
