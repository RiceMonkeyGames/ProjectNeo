using System.Collections.Generic;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 무리·위협 점수로 고르는 기본 자동 사격 전략(SIEGE-33, 사용자 결정 2026-11-24).
    /// 사거리·회전 범위 안의 후보마다 그 자리를 중심으로 폭발 반경 안에 든 모든 적의
    /// (폭발 감쇠 × 위협 가중치)를 더한다. 가장 높은 후보를 고르고, 같으면 수평 거리가 가까운 쪽, 그것도 같으면 앞 순번.
    /// </summary>
    public sealed class ClusterThreatCannonTargetSelector : ICannonTargetSelector
    {
        const float ScoreEpsilon = 0.0001f;
        readonly float blastRadius;
        readonly float edgeRatio;
        readonly float ladderClimberWeight;
        readonly float siegeBreakerWeight;
        readonly float rangedWeight;

        public ClusterThreatCannonTargetSelector(float blastRadius, float edgeRatio,
            float ladderClimberWeight = 3f, float siegeBreakerWeight = 3f, float rangedWeight = 1.5f)
        {
            this.blastRadius = Mathf.Max(0.01f, blastRadius);
            this.edgeRatio = Mathf.Clamp01(edgeRatio);
            this.ladderClimberWeight = Mathf.Max(0f, ladderClimberWeight);
            this.siegeBreakerWeight = Mathf.Max(0f, siegeBreakerWeight);
            this.rangedWeight = Mathf.Max(0f, rangedWeight);
        }

        public static ClusterThreatCannonTargetSelector From(CannonSettings settings) =>
            new ClusterThreatCannonTargetSelector(settings.BlastRadius, settings.EdgeDamageRatio,
                settings.LadderClimberThreatWeight, settings.SiegeBreakerThreatWeight, settings.RangedThreatWeight);

        /// <summary>위협 종류의 가중치. 일반 적은 1이다.</summary>
        public float ThreatWeight(TargetThreat threat) => threat switch
        {
            TargetThreat.LadderClimber => ladderClimberWeight,
            TargetThreat.SiegeBreaker => siegeBreakerWeight,
            TargetThreat.Ranged => rangedWeight,
            _ => 1f,
        };

        /// <summary>폭발 감쇠. 중심 1, 가장자리 edgeRatio, 반경 밖 0.</summary>
        public float Falloff(float distance)
        {
            if (distance > blastRadius) return 0f;
            return Mathf.Lerp(1f, edgeRatio, distance / blastRadius);
        }

        /// <summary>center를 노렸을 때의 점수. 모든 후보(Excluded·범위 밖 포함)를 센다.</summary>
        public float Score(Vector3 center, IReadOnlyList<CannonTargetCandidate> candidates)
        {
            float score = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                float distance = Vector3.Distance(candidates[i].Position, center);
                if (distance > blastRadius) continue;
                score += Falloff(distance) * ThreatWeight(candidates[i].Threat);
            }
            return score;
        }

        public int Select(in CannonAimContext context, IReadOnlyList<CannonTargetCandidate> candidates)
        {
            if (candidates == null) return -1;
            int best = -1;
            float bestScore = float.NegativeInfinity, bestDistance = float.PositiveInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                CannonTargetCandidate candidate = candidates[i];
                if (candidate.Excluded || !CannonAim.CanTarget(context, candidate.Position)) continue;
                float score = Score(candidate.Position, candidates);
                float distance = CannonAim.HorizontalDistance(context.Origin, candidate.Position);
                bool better = score > bestScore + ScoreEpsilon
                    || (score > bestScore - ScoreEpsilon && distance < bestDistance);
                if (!better) continue;
                best = i;
                bestScore = score;
                bestDistance = distance;
            }
            return best;
        }
    }
}
