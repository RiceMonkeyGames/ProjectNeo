using System;

namespace ProjectNeo.Meta
{
    /// <summary>
    /// 승천 단계 해금 규칙(FLOW-03, Q-78). 열린 가장 높은 단계를 깨야 다음 단계가 열린다.
    /// 낮은 단계를 다시 깨면 보상은 받지만 새 단계는 열리지 않는다.
    /// </summary>
    public static class AscensionProgress
    {
        /// <summary>지금 고를 수 있는 가장 높은 단계. 저장 값이 범위를 벗어나도 0 ~ maxLevel로 맞춘다.</summary>
        public static int HighestUnlocked(MetaProgress meta, int maxLevel)
        {
            if (meta == null) return 0;
            return Math.Min(Math.Max(0, meta.ascensionUnlocked), Math.Max(0, maxLevel));
        }

        /// <summary>이 단계로 새 런을 시작할 수 있는지.</summary>
        public static bool CanStart(MetaProgress meta, int level, int maxLevel) =>
            level >= 0 && level <= HighestUnlocked(meta, maxLevel);

        /// <summary>
        /// <paramref name="clearedLevel"/>로 마지막 스테이지까지 깼음을 기록한다. 다음 단계가 새로 열렸으면 true.
        /// 최고 단계를 깼거나 낮은 단계를 다시 깼으면 바뀌는 것이 없다.
        /// </summary>
        public static bool RecordClear(MetaProgress meta, int clearedLevel, int maxLevel)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            int highest = HighestUnlocked(meta, maxLevel);
            if (clearedLevel != highest || highest >= maxLevel) return false;
            meta.ascensionUnlocked = highest + 1;
            return true;
        }
    }
}
