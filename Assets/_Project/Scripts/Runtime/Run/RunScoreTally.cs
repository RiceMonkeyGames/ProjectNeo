using System;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 런 점수(META-04)를 위해 런 동안 쌓는 수. 전투 결과를 반영할 때 늘고 런 저장에 남는다(이어 하기에서도 이어진다).
    /// 런이 끝날 때의 생존 영웅·잡졸·골드는 여기에 두지 않고 그때의 런 상태에서 센다.
    /// </summary>
    public sealed class RunScoreTally
    {
        /// <summary>이긴 전투(= 클리어한 스테이지) 수.</summary>
        public int StagesCleared { get; private set; }
        /// <summary>처치한 보스 수. 보스는 SIEGE-09가 <see cref="AddBossKills"/>로 넘긴다.</summary>
        public int BossesKilled { get; private set; }
        /// <summary>모든 전투에서 처치한 적 수(퇴각한 적은 빠진다).</summary>
        public int EnemiesKilled { get; private set; }
        /// <summary>성문이 멀쩡한 채로 이긴 전투 수.</summary>
        public int GateIntactVictories { get; private set; }

        /// <summary>전투 결과 하나를 센다. <see cref="RunState.Apply"/>가 결과를 받아들일 때 한 번 부른다.</summary>
        public void Record(BattleResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            foreach (int count in result.EnemiesKilled.Values) EnemiesKilled += Math.Max(0, count);
            if (result.Outcome != Flow.BattleOutcome.Victory) return;
            StagesCleared++;
            if (result.GateIntact) GateIntactVictories++;
        }

        public void AddBossKills(int count)
        {
            if (count > 0) BossesKilled += count;
        }

        /// <summary>저장된 수로 되돌린다(런 저장, 테스트).</summary>
        public void Restore(int stages, int bosses, int enemies, int gateIntact)
        {
            StagesCleared = Math.Max(0, stages);
            BossesKilled = Math.Max(0, bosses);
            EnemiesKilled = Math.Max(0, enemies);
            GateIntactVictories = Math.Max(0, gateIntact);
        }
    }
}
