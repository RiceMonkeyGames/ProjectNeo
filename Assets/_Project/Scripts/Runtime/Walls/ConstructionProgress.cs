using System;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 공사 하나의 진행 상태. 공사 기간은 "거쳐야 하는 전투 스테이지 수"로 센다.
    /// 지금은 모든 공사가 0스테이지라서 만들자마자 완료된다.
    /// </summary>
    public sealed class ConstructionProgress
    {
        public int TotalStages { get; }
        public int RemainingStages { get; private set; }

        public ConstructionProgress(int totalStages)
        {
            if (totalStages < 0) throw new ArgumentOutOfRangeException(nameof(totalStages));
            TotalStages = totalStages;
            RemainingStages = totalStages;
        }

        public bool IsComplete => RemainingStages == 0;

        /// <summary>진행률 0~1. 덜 지은 성벽의 전투 시작 체력을 정할 때 쓴다.</summary>
        public float Progress01 => TotalStages == 0 ? 1f : 1f - (float)RemainingStages / TotalStages;

        /// <summary>전투 스테이지 하나가 끝날 때 호출한다.</summary>
        public void AdvanceStage()
        {
            if (RemainingStages > 0) RemainingStages--;
        }
    }
}
