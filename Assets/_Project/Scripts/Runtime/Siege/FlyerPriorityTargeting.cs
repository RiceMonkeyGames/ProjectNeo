using System.Collections.Generic;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 먼저 노릴 대상(<see cref="FlyerTargetCandidate.IsPriority"/>)이 있으면 그중 가장 가까운 대상을, 없으면 원래 전략의
    /// 선택을 쓴다(SIEGE-09 결투형 비행 보스). 순수 규칙이다.
    /// </summary>
    public static class FlyerPriorityTargeting
    {
        public static int Select(IReadOnlyList<FlyerTargetCandidate> candidates, FlyerTargetStrategy fallback)
        {
            int best = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                FlyerTargetCandidate candidate = candidates[i];
                float distance = candidate.DistanceSquared;
                if (!candidate.IsPriority || float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0f) continue;
                if (best < 0 || distance < candidates[best].DistanceSquared
                    || (distance == candidates[best].DistanceSquared && candidate.Id < candidates[best].Id)) best = i;
            }
            if (best >= 0) return best;
            return fallback != null ? fallback.Select(candidates) : -1;
        }
    }
}
