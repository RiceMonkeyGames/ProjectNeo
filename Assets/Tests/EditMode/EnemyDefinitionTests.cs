using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Economy;
using ProjectNeo.Siege;
using ProjectNeo.Stages;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>CONTENT-04 적 종류: 목록 찾기, 편성이 적 종류를 싣는 방법, 미리보기 이름·ID.</summary>
    public class EnemyDefinitionTests
    {
        readonly List<Object> created = new List<Object>();
        GameObject ladderPrefab, archerPrefab;
        EnemyDefinition ladder, archer;

        T Track<T>(T value) where T : Object { created.Add(value); return value; }

        [SetUp]
        public void SetUp()
        {
            ladderPrefab = Track(new GameObject("enemy_ladder"));
            archerPrefab = Track(new GameObject("enemy_archer"));
            MaterialDefinition bone = Track(MaterialDefinition.Create("bone"));
            ladder = Track(EnemyDefinition.Create("enemy_ladder", "사다리병", EnemyRole.GroundLadder, ladderPrefab, 3,
                new EnemyMaterialDrop(bone, 1)));
            archer = Track(EnemyDefinition.Create("enemy_archer", "궁수", EnemyRole.Ranged, archerPrefab));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object value in created) if (value != null) Object.DestroyImmediate(value);
            created.Clear();
        }

        [Test]
        public void Create_StoresValues_AndMoraleIsOffByDefault()
        {
            Assert.AreEqual("enemy_ladder", ladder.Id);
            Assert.AreEqual("enemy_ladder", ladder.name);
            Assert.AreEqual("사다리병", ladder.DisplayName);
            Assert.AreEqual(EnemyRole.GroundLadder, ladder.Role);
            Assert.AreSame(ladderPrefab, ladder.Prefab);
            Assert.AreEqual(3, ladder.KillGold);
            Assert.AreEqual(1, ladder.MaterialDrops.Count);
            Assert.IsFalse(ladder.AffectedByMorale, "적은 사기가 없다(Q-44).");
            Assert.AreEqual(0, archer.KillGold);
            Assert.AreEqual(0, archer.MaterialDrops.Count);
        }

        [Test]
        public void Catalog_FindsByIdAndPrefab_AndIgnoresMissing()
        {
            EnemyCatalog catalog = Track(EnemyCatalog.Create(ladder, null, archer));
            Assert.AreSame(archer, catalog.Find("enemy_archer"));
            Assert.AreSame(ladder, catalog.FindByPrefab(ladderPrefab));
            Assert.IsNull(catalog.Find("enemy_unknown"));
            Assert.IsNull(catalog.Find(null));
            Assert.IsNull(catalog.FindByPrefab(null));
            Assert.IsNull(catalog.FindByPrefab(Track(new GameObject("enemy_other"))));
        }

        [Test]
        public void Catalog_UsesFirstEntryForDuplicates()
        {
            EnemyDefinition copy = Track(EnemyDefinition.Create("enemy_ladder", "Copy", EnemyRole.GroundLadder, ladderPrefab));
            EnemyCatalog catalog = Track(EnemyCatalog.Create(ladder, copy));
            Assert.AreSame(ladder, catalog.Find("enemy_ladder"));
            Assert.AreSame(ladder, catalog.FindByPrefab(ladderPrefab));
        }

        [Test]
        public void StageGroup_PrefersEnemyDefinition_OverFallbackPrefab()
        {
            var stage = Track(StageDefinition.Create("Test", new StageWave
            {
                startSeconds = 0f,
                groups = new[]
                {
                    new StageWaveGroup { enemy = ladder, enemyPrefab = archerPrefab, count = 2 },
                    new StageWaveGroup { enemyPrefab = archerPrefab, count = 1 },
                    new StageWaveGroup { enemy = Track(EnemyDefinition.Create("enemy_empty", "Empty", EnemyRole.Flyer, null)), count = 4 },
                }
            }));
            EncounterPlan plan = EncounterPlan.FromStage(stage, Vector2.zero);
            IReadOnlyList<EncounterGroup> groups = plan.Waves[0].Groups;
            Assert.AreEqual(2, groups.Count, "프리팹이 없는 적 종류는 뺀다.");
            Assert.AreSame(ladder, groups[0].Enemy);
            Assert.AreSame(ladderPrefab, groups[0].Prefab, "적 종류의 프리팹을 쓴다.");
            Assert.AreEqual("enemy_ladder", groups[0].EnemyId);
            Assert.IsNull(groups[1].Enemy, "예비 프리팹만 있으면 적 종류가 없다.");
            Assert.AreEqual("enemy_archer", groups[1].EnemyId, "예비 프리팹은 프리팹 이름을 ID로 쓴다.");
        }

        [Test]
        public void WithCounts_KeepsEnemyDefinition()
        {
            var plan = new EncounterPlan(new[] { new EncounterWave(0f, new[] { new EncounterGroup(ladder, 3, Vector2.one, true) }) });
            EncounterGroup scaled = plan.WithCounts(count => count * 2).Waves[0].Groups[0];
            Assert.AreEqual(6, scaled.Count);
            Assert.AreSame(ladder, scaled.Enemy);
            Assert.AreSame(ladderPrefab, scaled.Prefab);
            Assert.IsTrue(scaled.Hidden);
            Assert.AreEqual(Vector2.one, scaled.Cell);
        }

        [Test]
        public void FromGroups_ResolvesDefinitionsByPrefab_WhenCatalogGiven()
        {
            var unknown = Track(new GameObject("enemy_mystery_unit"));
            var groups = new[]
            {
                new SiegeUnitGroup { unitPrefab = ladderPrefab, count = 2 },
                new SiegeUnitGroup { unitPrefab = unknown, count = 1 },
            };
            EncounterPlan withCatalog = EncounterPlan.FromGroups(groups, Vector2.zero, Track(EnemyCatalog.Create(ladder)));
            Assert.AreSame(ladder, withCatalog.Waves[0].Groups[0].Enemy);
            Assert.IsNull(withCatalog.Waves[0].Groups[1].Enemy);
            Assert.IsNull(EncounterPlan.FromGroups(groups, Vector2.zero).Waves[0].Groups[0].Enemy, "목록이 없으면 프리팹만으로 둔다.");
            Assert.AreEqual("사다리병 × 2\nMystery Unit × 1", EnemyIntel.FromPlan(withCatalog).Describe());
        }

        [Test]
        public void Intel_UsesDefinitionNameAndId_AndMergesSameKind()
        {
            var plan = new EncounterPlan(new[]
            {
                new EncounterWave(0f, new[] { new EncounterGroup(ladder, 2, Vector2.zero), new EncounterGroup(archer, 1, Vector2.zero, hidden: true) }),
                new EncounterWave(5f, new[] { new EncounterGroup(ladder, 3, Vector2.zero) }),
            });
            EnemyIntel intel = EnemyIntel.FromPlan(plan);
            Assert.AreEqual("enemy_ladder", intel.Group(0).TypeId);
            Assert.AreEqual("enemy_archer", intel.Group(1).TypeId);
            Assert.AreEqual("사다리병 × 5\n? × 1", intel.Describe());
            intel.RevealAll();
            Assert.AreEqual("사다리병 × 5\n궁수 × 1", intel.Describe());
        }

        [Test]
        public void Boss_ReadsPrefabFromBaseEnemy_AndKeepsBossId()
        {
            BossDefinition boss = Track(BossDefinition.Create("boss_test", "Test Boss", BossRole.Siege, ladder, 1.5f, 400f, 21f, 4f));
            Assert.AreSame(ladder, boss.BaseEnemy);
            Assert.AreSame(ladderPrefab, boss.BasePrefab);
            EncounterGroup group = EncounterGroup.ForBoss(boss, Vector2.zero);
            Assert.AreSame(ladderPrefab, group.Prefab);
            Assert.AreEqual("boss_test", group.EnemyId, "보스는 바탕 적이 아니라 보스 ID로 센다.");
            Assert.IsNull(Track(BossDefinition.Create("boss_none", "None", BossRole.Siege, null, 1f, 1f, 1f, 1f)).BasePrefab);
        }
    }
}
