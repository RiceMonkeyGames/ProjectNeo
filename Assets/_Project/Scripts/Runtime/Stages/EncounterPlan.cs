using System.Collections.Generic;
using ProjectNeo.Siege;
using UnityEngine;

namespace ProjectNeo.Stages
{
    /// <summary>이번 전투에 실제로 만들 적 한 무리. 스폰 지점은 이미 격자 칸으로 풀려 있다.</summary>
    public readonly struct EncounterGroup
    {
        public GameObject Prefab { get; }
        public int Count { get; }
        public Vector2 Cell { get; }
        /// <summary>미리보기에서 종류를 숨기는 무리(C-5). 전투에는 영향이 없다.</summary>
        public bool Hidden { get; }
        /// <summary>보스 무리면 그 보스(SIEGE-09). 보스 무리는 언제나 1마리이고 숨기지 않는다.</summary>
        public BossDefinition Boss { get; }
        public bool IsBoss => Boss != null;
        public EncounterGroup(GameObject prefab, int count, Vector2 cell, bool hidden = false)
        { Prefab = prefab; Count = count; Cell = cell; Hidden = hidden; Boss = null; }

        /// <summary>보스 한 마리 무리. 바탕 프리팹은 보스 정의에서 읽는다.</summary>
        public static EncounterGroup ForBoss(BossDefinition boss, Vector2 cell) => new EncounterGroup(boss, cell);

        EncounterGroup(BossDefinition boss, Vector2 cell)
        { Prefab = boss != null ? boss.BasePrefab : null; Count = 1; Cell = cell; Hidden = false; Boss = boss; }
    }

    public sealed class EncounterWave
    {
        public float StartSeconds { get; }
        public IReadOnlyList<EncounterGroup> Groups { get; }
        public int UnitCount { get; }
        public EncounterWave(float startSeconds, IReadOnlyList<EncounterGroup> groups)
        {
            StartSeconds = Mathf.Max(0f, startSeconds);
            Groups = groups ?? new EncounterGroup[0];
            int total = 0;
            foreach (EncounterGroup group in Groups) total += group.Count;
            UnitCount = total;
        }
    }

    /// <summary>
    /// 전투가 읽는 단 하나의 적 편성 형식. 스테이지 에셋에서도, 예전의 단일 적 목록에서도 만들 수 있어
    /// 전투·스폰 코드는 편성이 어디서 왔는지 모른다.
    /// </summary>
    public sealed class EncounterPlan
    {
        public IReadOnlyList<EncounterWave> Waves { get; }
        public int TotalUnits { get; }

        public EncounterPlan(IReadOnlyList<EncounterWave> waves)
        {
            Waves = waves ?? new EncounterWave[0];
            int total = 0;
            foreach (EncounterWave wave in Waves) total += wave.UnitCount;
            TotalUnits = total;
        }

        /// <summary>
        /// 무리마다 적 수를 바꾼 새 편성(승천의 적 수 증가, FLOW-03). 웨이브 시작 시간·스폰 칸·숨김은 그대로다.
        /// 0 이하가 된 무리는 뺀다. <paramref name="count"/>가 null이면 이 편성을 그대로 돌려준다.
        /// </summary>
        public EncounterPlan WithCounts(System.Func<int, int> count)
        {
            if (count == null) return this;
            var waves = new List<EncounterWave>();
            foreach (EncounterWave wave in Waves)
            {
                var groups = new List<EncounterGroup>();
                foreach (EncounterGroup group in wave.Groups)
                {
                    // 보스는 승천 적 수 증가에도 한 마리다.
                    if (group.IsBoss) { groups.Add(group); continue; }
                    int scaled = count(group.Count);
                    if (scaled > 0) groups.Add(new EncounterGroup(group.Prefab, scaled, group.Cell, group.Hidden));
                }
                if (groups.Count > 0) waves.Add(new EncounterWave(wave.StartSeconds, groups));
            }
            return new EncounterPlan(waves);
        }

        /// <summary>시간표 규칙(WaveSchedule)에 넘길 값.</summary>
        public WaveTiming[] Timings()
        {
            var timings = new WaveTiming[Waves.Count];
            for (int i = 0; i < timings.Length; i++) timings[i] = new WaveTiming(Waves[i].StartSeconds, Waves[i].UnitCount);
            return timings;
        }

        /// <summary>스테이지 에셋으로 만든다. 프리팹이 없거나 수가 0인 무리와 빈 웨이브는 뺀다.</summary>
        public static EncounterPlan FromStage(StageDefinition stage, Vector2 defaultCell)
        {
            var waves = new List<EncounterWave>();
            if (stage != null)
                foreach (StageWave wave in stage.Waves)
                {
                    var groups = new List<EncounterGroup>();
                    if (wave.groups != null)
                        foreach (StageWaveGroup group in wave.groups)
                            if (group.enemyPrefab != null && group.count > 0)
                                groups.Add(new EncounterGroup(group.enemyPrefab, group.count,
                                    group.spawnPoint != null ? group.spawnPoint.Cell : defaultCell, group.hidden));
                    // 보스는 그 웨이브의 마지막 무리로 둔다(SIEGE-09). 바탕 프리팹이 없으면 뺀다.
                    if (wave.boss != null && wave.boss.BasePrefab != null)
                        groups.Add(EncounterGroup.ForBoss(wave.boss,
                            wave.bossSpawnPoint != null ? wave.bossSpawnPoint.Cell : defaultCell));
                    if (groups.Count > 0) waves.Add(new EncounterWave(wave.startSeconds, groups));
                }
            return new EncounterPlan(waves);
        }

        /// <summary>예전 방식의 단일 적 목록을 전투 시작과 함께 나오는 웨이브 하나로 감싼다.</summary>
        public static EncounterPlan FromGroups(IReadOnlyList<SiegeUnitGroup> enemies, Vector2 cell)
        {
            var groups = new List<EncounterGroup>();
            if (enemies != null)
                foreach (SiegeUnitGroup group in enemies)
                    if (group.unitPrefab != null && group.count > 0)
                        groups.Add(new EncounterGroup(group.unitPrefab, group.count, cell));
            return new EncounterPlan(groups.Count > 0 ? new[] { new EncounterWave(0f, groups) } : new EncounterWave[0]);
        }
    }
}
