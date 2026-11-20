namespace ProjectNeo.Run
{
    /// <summary>런 점수(META-04)에 쓰는 수. 전투 결과를 반영할 때 쌓이고 런 저장에 남는다.</summary>
    public sealed partial class RunState
    {
        public RunScoreTally ScoreTally { get; } = new RunScoreTally();

        /// <summary>런이 끝날 때 살아 있는 영웅 수(플레이어 제외).</summary>
        public int LivingHeroCount
        {
            get
            {
                int count = 0;
                foreach (RunHero hero in heroes) if (!hero.IsPlayer) count++;
                return count;
            }
        }

        /// <summary>런이 끝날 때 살아 있는 잡졸 수.</summary>
        public int LivingGruntCount => soldiers.Count;

        void RestoreScore(ScoreRecord record)
        {
            if (record == null) return;
            ScoreTally.Restore(record.stagesCleared, record.bossesKilled, record.enemiesKilled, record.gateIntactVictories);
        }

        ScoreRecord CaptureScore() => new ScoreRecord
        {
            stagesCleared = ScoreTally.StagesCleared, bossesKilled = ScoreTally.BossesKilled,
            enemiesKilled = ScoreTally.EnemiesKilled, gateIntactVictories = ScoreTally.GateIntactVictories,
        };
    }
}
