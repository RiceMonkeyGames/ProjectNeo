using NUnit.Framework;
using ProjectNeo.Stages;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>C-5 적 정보 규칙: 종류별 합계, 숨긴 무리 "? × 수", 공개, 남은 수.</summary>
    public class EnemyIntelTests
    {
        static EnemyIntel Intel() => new EnemyIntel(new[]
        {
            new EnemyIntelGroup("enemy_ladder", "Ladder", 4, false),
            new EnemyIntelGroup("enemy_archer", "Archer", 2, true),
            new EnemyIntelGroup("enemy_ladder", "Ladder", 3, false),
            new EnemyIntelGroup("enemy_gate_breaker", "Gate Breaker", 1, true),
            new EnemyIntelGroup("enemy_archer", "Archer", 1, false)
        });

        [Test]
        public void Rows_MergeKnownTypesAcrossWaves_AndShowHiddenAsQuestionMarks()
        {
            EnemyIntel intel = Intel();
            Assert.AreEqual("Ladder × 7\nArcher × 1\n? × 2\n? × 1", intel.Describe());
            Assert.IsTrue(intel.AnyHidden);
            Assert.AreEqual(11, intel.TotalRemaining);
            foreach (EnemyIntelRow row in intel.Rows())
                if (row.Hidden) Assert.IsEmpty(row.TypeName, "숨긴 줄에는 종류 이름이 들어 있지 않다.");
        }

        [Test]
        public void Reveal_MovesGroupIntoItsTypeRow_Once()
        {
            EnemyIntel intel = Intel();
            int version = intel.Version;
            Assert.IsTrue(intel.Reveal(1));
            Assert.Greater(intel.Version, version);
            Assert.AreEqual("Ladder × 7\nArcher × 3\n? × 1", intel.Describe());
            Assert.IsFalse(intel.Reveal(1), "이미 공개된 무리는 바뀌지 않는다.");
            Assert.IsFalse(intel.Reveal(0), "숨기지 않은 무리는 바뀌지 않는다.");
            Assert.IsFalse(intel.Reveal(99));
        }

        [Test]
        public void RevealAll_RevealsEveryHiddenGroup()
        {
            EnemyIntel intel = Intel();
            Assert.AreEqual(2, intel.RevealAll());
            Assert.IsFalse(intel.AnyHidden);
            Assert.AreEqual("Ladder × 7\nArcher × 3\nGate Breaker × 1", intel.Describe());
            Assert.AreEqual(0, intel.RevealAll());
        }

        [Test]
        public void SetRemaining_ClampsAndKeepsEmptyRows()
        {
            EnemyIntel intel = Intel();
            intel.SetRemaining(0, 1);
            intel.SetRemaining(2, -5);
            intel.SetRemaining(4, 10);
            Assert.AreEqual("Ladder × 1\nArcher × 1\n? × 2\n? × 1", intel.Describe());
            intel.SetRemaining(0, 0);
            intel.SetRemaining(4, 0);
            Assert.AreEqual("Ladder × 0\nArcher × 0\n? × 2\n? × 1", intel.Describe());
            Assert.AreEqual(3, intel.TotalRemaining);
            int version = intel.Version;
            intel.SetRemaining(0, 0);
            Assert.AreEqual(version, intel.Version, "같은 값이면 다시 그릴 필요가 없다.");
        }

        [Test]
        public void Empty_DescribesNoEnemies()
        {
            Assert.AreEqual("No enemies", new EnemyIntel(null).Describe());
            Assert.IsFalse(new EnemyIntel(null).AnyHidden);
        }

        [TestCase("enemy_gate_breaker", "Gate Breaker")]
        [TestCase("enemy_archer", "Archer")]
        [TestCase("Flyer", "Flyer")]
        [TestCase("", "Unknown")]
        public void DisplayName_FromPrefabName(string prefab, string expected) =>
            Assert.AreEqual(expected, EnemyIntel.DisplayName(prefab));

        [Test]
        public void FromPlan_KeepsWaveThenGroupOrder_AndHiddenFlag()
        {
            var ladder = new GameObject("enemy_ladder");
            var archer = new GameObject("enemy_archer");
            try
            {
                var plan = new EncounterPlan(new[]
                {
                    new EncounterWave(0f, new[] { new EncounterGroup(ladder, 2, Vector2.zero) }),
                    new EncounterWave(5f, new[]
                    {
                        new EncounterGroup(archer, 1, Vector2.zero, hidden: true),
                        new EncounterGroup(ladder, 3, Vector2.zero)
                    })
                });
                EnemyIntel intel = EnemyIntel.FromPlan(plan);
                Assert.AreEqual(3, intel.GroupCount);
                Assert.IsTrue(intel.Group(1).Hidden);
                Assert.AreEqual("Ladder × 5\n? × 1", intel.Describe());
            }
            finally
            {
                Object.DestroyImmediate(ladder);
                Object.DestroyImmediate(archer);
            }
        }

        [Test]
        public void StageGroupHiddenFlag_CarriesIntoEncounterPlan()
        {
            var prefab = new GameObject("enemy_archer");
            var stage = StageDefinition.Create("Test",
                new StageWave { startSeconds = 0f, groups = new[] { new StageWaveGroup { enemyPrefab = prefab, count = 2, hidden = true } } });
            try
            {
                EncounterPlan plan = EncounterPlan.FromStage(stage, Vector2.zero);
                Assert.IsTrue(plan.Waves[0].Groups[0].Hidden);
                Assert.AreEqual("? × 2", EnemyIntel.FromPlan(plan).Describe());
            }
            finally
            {
                Object.DestroyImmediate(stage);
                Object.DestroyImmediate(prefab);
            }
        }
    }
}
