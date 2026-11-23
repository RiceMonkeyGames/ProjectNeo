namespace ProjectNeo.Siege
{
    /// <summary>
    /// 적이 어느 사다리로 갈지 매기는 점수(SIEGE-17). 낮을수록 좋다. 거리에 몰린 인원 벌점을 더하고,
    /// 이미 고른 사다리는 조금 깎아 이리저리 옮기지 않게 하며, 막혀서 포기한 사다리는 잠시 큰 벌점을 준다.
    /// 길찾기로 닿는지는 점수가 좋은 순서대로 따로 확인한다.
    /// </summary>
    public static class LadderChoiceRules
    {
        public static float Score(float distance, int othersCrowd, bool isCurrent, bool avoided,
            float crowdPenalty, float avoidPenalty)
        {
            float score = distance + (othersCrowd > 0 ? othersCrowd : 0) * crowdPenalty;
            if (isCurrent) score -= crowdPenalty;
            if (avoided) score += avoidPenalty;
            return score;
        }

        /// <summary>아직 확인하지 않은 후보 중 점수가 가장 낮은 것. 남은 후보가 없으면 -1.</summary>
        public static int NextBest(float[] scores, bool[] excluded, int count)
        {
            int best = -1;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (excluded[i] || float.IsPositiveInfinity(scores[i]) || scores[i] >= bestScore) continue;
                bestScore = scores[i];
                best = i;
            }
            return best;
        }
    }
}
