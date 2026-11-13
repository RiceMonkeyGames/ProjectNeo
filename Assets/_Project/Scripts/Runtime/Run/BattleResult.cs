using System;
using System.Collections.Generic;
using ProjectNeo.Flow;

namespace ProjectNeo.Run
{
    /// <summary>전투가 끝난 이유. 저장될 수 있으므로 번호를 바꾸지 않고 새 값은 끝에 더한다.</summary>
    public enum BattleEndReason
    {
        /// <summary>적이 모두 죽었거나 일부가 도망쳐 남은 적이 없다.</summary>
        EnemiesDefeated = 0,
        /// <summary>공성 수단을 잃은 남은 적이 물러났다(Q-30).</summary>
        EnemiesRetreated = 1,
        PlayerDied = 2,
        /// <summary>메인 성문이 부서지고 아군이 모두 죽거나 도망쳤다.</summary>
        GateAndAlliesLost = 3,
    }

    /// <summary>전투가 끝났을 때 아군 유닛 하나의 상태. 저장될 수 있으므로 번호를 바꾸지 않는다.</summary>
    public enum UnitFate
    {
        Alive = 0,
        /// <summary>죽었다. 잡졸은 사라지고 영웅은 영구 사망한다.</summary>
        Dead = 1,
        /// <summary>도망쳤다. 런에 남아 다음 전투에 돌아온다.</summary>
        Fled = 2,
    }

    /// <summary>
    /// 전투가 런에 돌려주는 결과. <see cref="BattleInput.BattleNumber"/>를 그대로 담아 한 번만 반영되게 한다.
    /// 결과에 없는 아군 유닛은 살아남은 것으로 본다. 성 기록이 null이면 성은 바뀌지 않은 것으로 본다.
    /// 초안(2026-11-12)이며 전투 담당과 확인한 뒤 확정한다.
    /// </summary>
    public sealed class BattleResult
    {
        readonly Dictionary<int, UnitFate> fates = new Dictionary<int, UnitFate>();
        readonly Dictionary<string, int> enemiesKilled = new Dictionary<string, int>();
        readonly Dictionary<string, int> enemiesRetreated = new Dictionary<string, int>();

        public int BattleNumber { get; }
        public BattleOutcome Outcome { get; }
        public BattleEndReason EndReason { get; }
        /// <summary>전투가 끝난 뒤의 성. 부서진 조각은 빠지고 내려앉은 조각은 바뀐 층으로 담긴다.</summary>
        public CastleState CastleAfter { get; set; }
        public IReadOnlyDictionary<int, UnitFate> Fates => fates;
        /// <summary>처치한 적 종류별 수. 키는 적 정의의 ID다. 재료 보상(C-4)이 읽는다.</summary>
        public IReadOnlyDictionary<string, int> EnemiesKilled => enemiesKilled;
        /// <summary>
        /// 퇴각(Q-30)으로 전투에서 빠진 적 종류별 수. 처치 수와 따로 센다(Q-44). 키는 적 정의의 ID다.
        /// 적은 사기가 없어 사기 도망은 없다.
        /// </summary>
        public IReadOnlyDictionary<string, int> EnemiesRetreated => enemiesRetreated;

        public BattleResult(int battleNumber, BattleOutcome outcome, BattleEndReason endReason)
        {
            if (battleNumber <= 0) throw new ArgumentOutOfRangeException(nameof(battleNumber));
            BattleNumber = battleNumber;
            Outcome = outcome;
            EndReason = endReason;
        }

        public void SetFate(int unitId, UnitFate fate) => fates[unitId] = fate;

        public UnitFate FateOf(int unitId) => fates.TryGetValue(unitId, out UnitFate fate) ? fate : UnitFate.Alive;

        public void AddKill(string enemyId, int count = 1)
        {
            if (string.IsNullOrEmpty(enemyId)) throw new ArgumentException("적 ID가 비어 있다.", nameof(enemyId));
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            enemiesKilled.TryGetValue(enemyId, out int current);
            enemiesKilled[enemyId] = current + count;
        }

        public void AddRetreat(string enemyId, int count = 1)
        {
            if (string.IsNullOrEmpty(enemyId)) throw new ArgumentException("적 ID가 비어 있다.", nameof(enemyId));
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            enemiesRetreated.TryGetValue(enemyId, out int current);
            enemiesRetreated[enemyId] = current + count;
        }
    }
}
