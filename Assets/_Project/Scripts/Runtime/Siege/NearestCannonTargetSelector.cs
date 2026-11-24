using System.Collections.Generic;

namespace ProjectNeo.Siege
{
    /// <summary>사거리·회전 범위 안에서 수평 거리가 가장 가까운 후보를 고른다. 같으면 앞 순번.</summary>
    public sealed class NearestCannonTargetSelector : ICannonTargetSelector
    {
        public int Select(in CannonAimContext context, IReadOnlyList<CannonTargetCandidate> candidates)
        {
            if (candidates == null) return -1;
            int best = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                CannonTargetCandidate candidate = candidates[i];
                if (candidate.Excluded || !CannonAim.CanTarget(context, candidate.Position)) continue;
                float distance = CannonAim.HorizontalDistance(context.Origin, candidate.Position);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }
    }
}
