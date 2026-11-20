using System;

namespace ProjectNeo.Meta
{
    /// <summary>
    /// 지휘관 경험치와 퍽 포인트를 주는 규칙. 경험치는 스테이지 클리어로만 얻고(Q-71: 환생 보너스 없음)
    /// AddXp로 들어오며, 레벨이 오르면 바로 퍽 포인트가 생긴다.
    /// </summary>
    public static class CommanderProgression
    {
        /// <summary>경험치를 주고, 오른 레벨 수를 돌려준다. 레벨마다 퍽 포인트를 지급한다.</summary>
        public static int AddXp(MetaProgress progress, MetaConfig config, int xp)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (xp < 0) throw new ArgumentOutOfRangeException(nameof(xp));

            progress.commanderXp += xp;
            int levelsGained = 0;
            while (progress.commanderXp >= config.GetXpToNextLevel(progress.commanderLevel))
            {
                progress.commanderXp -= config.GetXpToNextLevel(progress.commanderLevel);
                progress.commanderLevel++;
                levelsGained++;
            }
            progress.perkPoints += levelsGained * config.PerkPointsPerLevel;
            return levelsGained;
        }

        /// <summary>레벨업 외의 출처로 퍽 포인트를 직접 준다.</summary>
        public static void AddPerkPoints(MetaProgress progress, int points)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (points < 0) throw new ArgumentOutOfRangeException(nameof(points));
            progress.perkPoints += points;
        }
    }
}
