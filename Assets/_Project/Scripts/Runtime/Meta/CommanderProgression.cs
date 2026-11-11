using System;
using UnityEngine;

namespace ProjectNeo.Meta
{
    /// <summary>
    /// 지휘관 경험치와 퍽 포인트를 주는 규칙. 경험치 출처(스테이지 클리어, 환생 보너스 등)는
    /// 모두 AddXp로 들어오고, 레벨이 오르면 바로 퍽 포인트가 생긴다.
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

        /// <summary>환생 보너스: 런 점수의 일정 비율을 경험치로 준다. 준 경험치를 돌려준다.</summary>
        public static int AddRunScoreBonus(MetaProgress progress, MetaConfig config, int runScore)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            int xp = Mathf.FloorToInt(Mathf.Max(runScore, 0) * config.RunScoreToXpRate);
            AddXp(progress, config, xp);
            return xp;
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
