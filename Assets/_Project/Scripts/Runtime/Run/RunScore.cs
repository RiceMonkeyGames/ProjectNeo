using System;
using System.Collections.Generic;
using System.Globalization;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 런 점수(META-04, Q-70). 스테이지·보스·처치·성문 유지는 런 동안 쌓인 수(<see cref="RunScoreTally"/>),
    /// 생존 영웅·잡졸·골드는 런이 끝날 때의 상태로 센다. 엔딩에 닿으면 보너스를 더하고, 합계에 승천 배율(Q-78)을 곱해 반올림한다.
    /// 값은 <see cref="StageRewardTable"/> 에셋에 있다. 순수 C#이다.
    /// </summary>
    public sealed class RunScore
    {
        readonly List<KeyValuePair<string, int>> parts = new List<KeyValuePair<string, int>>();

        /// <summary>항목별 점수(이름, 배율 전 점수). 0인 항목은 빠진다.</summary>
        public IReadOnlyList<KeyValuePair<string, int>> Parts => parts;
        public int Subtotal { get; private set; }
        public float Multiplier { get; private set; } = 1f;
        public int Total { get; private set; }

        public static RunScore Calculate(StageRewardTable table, RunScoreTally tally, int livingHeroes, int livingGrunts,
            int gold, bool reachedEnding, float multiplier)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (tally == null) throw new ArgumentNullException(nameof(tally));
            var score = new RunScore();
            score.Add("Stages", tally.StagesCleared * table.ScorePerStageCleared);
            score.Add("Bosses", tally.BossesKilled * table.ScorePerBossKilled);
            score.Add("Enemies", tally.EnemiesKilled * table.ScorePerEnemyKilled);
            score.Add("Gate held", tally.GateIntactVictories * table.ScorePerGateIntactVictory);
            score.Add("Heroes", Math.Max(0, livingHeroes) * table.ScorePerLivingHero);
            score.Add("Grunts", Math.Max(0, livingGrunts) * table.ScorePerLivingGrunt);
            score.Add("Gold", Math.Max(0, gold) / table.GoldPerScorePoint);
            if (reachedEnding) score.Add("Ending", table.ScoreForEnding);
            score.Multiplier = Math.Max(0f, multiplier);
            score.Total = (int)Math.Round(score.Subtotal * (double)score.Multiplier, MidpointRounding.AwayFromZero);
            return score;
        }

        void Add(string name, int points)
        {
            if (points <= 0) return;
            parts.Add(new KeyValuePair<string, int>(name, points));
            Subtotal += points;
        }

        /// <summary>결과 화면용 한 줄: "Stages 3,000 + Enemies 120 ... x1.2".</summary>
        public string Breakdown()
        {
            if (parts.Count == 0) return "No points";
            var items = new List<string>();
            foreach (KeyValuePair<string, int> part in parts) items.Add($"{part.Key} {Number(part.Value)}");
            string text = string.Join(" + ", items);
            if (Math.Abs(Multiplier - 1f) > 0.0001f) text += $" x{Multiplier.ToString("0.0#", CultureInfo.InvariantCulture)}";
            return text;
        }

        public static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
