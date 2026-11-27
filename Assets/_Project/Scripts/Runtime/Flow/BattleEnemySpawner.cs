using System.Collections.Generic;
using ProjectNeo.Siege;
using ProjectNeo.Stages;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 편성(EncounterPlan)의 웨이브 하나를 씬에 만든다. 언제 내보낼지와 승패는 모르고, 만든 적은 꺼진 채로 돌려준다.
    /// 대형(열 수·간격)은 전투 설정에서 읽는다.
    /// </summary>
    public sealed class BattleEnemySpawner
    {
        public readonly struct Spawned
        {
            public SiegeAttacker Attacker { get; }
            /// <summary>적 종류 ID(EncounterGroup.EnemyId): 보스 ID, 적 종류 ID, 적 종류가 없으면 프리팹 이름.</summary>
            public string EnemyId { get; }
            /// <summary>웨이브 안에서 이 적이 속한 무리 번호(EncounterWave.Groups 순서).</summary>
            public int GroupIndex { get; }
            /// <summary>보스면 붙인 보스 부품(SIEGE-09). 아니면 null.</summary>
            public BossUnit Boss { get; }
            public Spawned(SiegeAttacker attacker, string enemyId, int groupIndex = 0, BossUnit boss = null)
            { Attacker = attacker; EnemyId = enemyId; GroupIndex = groupIndex; Boss = boss; }
        }

        readonly ConstructionWorld world;
        readonly GameBattleSettings settings;
        readonly FallSettings fallSettings;
        readonly SiegeLadder[] ladders;
        readonly Transform parent;
        readonly Object logContext;
        int spawnedCount;

        public BattleEnemySpawner(ConstructionWorld world, GameBattleSettings settings, FallSettings fallSettings,
            SiegeLadder[] ladders, Transform parent, Object logContext = null)
        {
            this.world = world; this.settings = settings; this.fallSettings = fallSettings;
            this.ladders = ladders; this.parent = parent; this.logContext = logContext;
        }

        /// <summary>새 전투의 이름 번호를 0부터 다시 센다.</summary>
        public void ResetNumbering() => spawnedCount = 0;

        /// <summary>
        /// 웨이브의 무리들을 각자의 스폰 칸에 줄지어 만든다. 같은 칸을 쓰는 무리는 바깥쪽으로 줄을 이어 선다.
        /// </summary>
        public List<Spawned> SpawnWave(EncounterWave wave)
        {
            var result = new List<Spawned>();
            Vector3 outward = world.transform.TransformDirection(new Vector3(settings.Outward.x, 0f, settings.Outward.y)).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, outward);
            var rows = new Dictionary<Vector2, int>();
            for (int groupIndex = 0; groupIndex < wave.Groups.Count; groupIndex++)
            {
                EncounterGroup group = wave.Groups[groupIndex];
                if (group.Prefab == null || group.Count <= 0) continue;
                if (group.Prefab.activeSelf)
                { Debug.LogError("Battle enemy prefab must be inactive: " + group.Prefab.name, logContext); continue; }
                rows.TryGetValue(group.Cell, out int row);
                Vector3 origin = world.transform.TransformPoint(new Vector3(group.Cell.x * world.Grid.CellSize,
                    0f, group.Cell.y * world.Grid.CellSize));
                for (int i = 0; i < group.Count; i++)
                {
                    int inRow = Mathf.Min(settings.Columns, group.Count - i / settings.Columns * settings.Columns);
                    Vector3 position = origin + right * ((i % settings.Columns - (inRow - 1) * 0.5f) * settings.Spacing)
                        + outward * ((row + i / settings.Columns) * settings.RowSpacing);
                    GameObject unit = Object.Instantiate(group.Prefab, parent);
                    unit.name = group.Prefab.name + "_" + spawnedCount++;
                    unit.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-outward));
                    var brain = unit.GetComponent<SiegeAttacker>();
                    if (brain == null) { Debug.LogError("Battle enemy needs a SiegeAttacker", unit); Object.Destroy(unit); continue; }
                    // 나는 적은 발판이 무너져도 떨어지지 않으므로 낙하 부품을 붙이지 않는다.
                    if (brain.UsesFallController)
                    {
                        var fall = unit.GetComponent<UnitFallController>();
                        if (fall == null) fall = unit.AddComponent<UnitFallController>();
                        fall.Configure(world, fallSettings);
                    }
                    brain.Bind(world, ladders, settings.Outward, settings.Siege);
                    // 보스(SIEGE-09)는 보스 부품을 붙이고 보스 ID로 센다. 정의 연결은 전투가 웨이브를 다 만든 뒤 한다.
                    if (group.IsBoss)
                    {
                        unit.name = group.Boss.Id + "_" + (spawnedCount - 1);
                        result.Add(new Spawned(brain, group.EnemyId, groupIndex, unit.AddComponent<BossUnit>()));
                    }
                    else result.Add(new Spawned(brain, group.EnemyId, groupIndex));
                }
                rows[group.Cell] = row + (group.Count + settings.Columns - 1) / settings.Columns;
            }
            return result;
        }
    }
}
