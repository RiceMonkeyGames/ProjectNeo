using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Siege;
using ProjectNeo.Stages;
using UnityEditor;

namespace ProjectNeo.Tests
{
    /// <summary>실제 적 종류 데이터(CONTENT-04)가 연결되어 있고 옮기기 전 처치 보상과 같은지 본다.</summary>
    public class EnemyCatalogDataTests
    {
        const string CatalogPath = "Assets/_Project/Data/GameFlow/Enemies/EnemyCatalog.asset";

        static EnemyCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "Run ProjectNeo/Setup/Create Enemy Definitions (CONTENT-04).");
            return catalog;
        }

        [Test]
        public void ProjectCatalog_IsUsedByBattleSettingsAndRewardTable()
        {
            EnemyCatalog catalog = LoadCatalog();
            var battle = AssetDatabase.LoadAssetAtPath<GameBattleSettings>("Assets/_Project/Data/GameFlow/Battle.asset");
            var table = AssetDatabase.LoadAssetAtPath<StageRewardTable>("Assets/_Project/Data/GameFlow/Rewards/StageRewards.asset");
            Assert.AreSame(catalog, battle.EnemyCatalog);
            Assert.AreSame(catalog, table.Enemies);
        }

        [Test]
        public void ProjectEnemies_HaveUniqueIdsMatchingPrefabs_AndRolesMatchPrefabBehaviour()
        {
            var ids = new HashSet<string>();
            foreach (EnemyDefinition enemy in LoadCatalog().Enemies)
            {
                Assert.IsNotNull(enemy, "empty catalog row");
                Assert.IsTrue(ids.Add(enemy.Id), "duplicate id " + enemy.Id);
                Assert.IsFalse(enemy.Id.StartsWith("boss_"), "enemy ids must not clash with boss ids");
                Assert.IsNotEmpty(enemy.DisplayName, enemy.Id);
                Assert.IsNotNull(enemy.Prefab, enemy.Id + " without a prefab");
                Assert.IsFalse(enemy.Prefab.activeSelf, enemy.Id + " prefab must be inactive");
                Assert.AreEqual(enemy.Prefab.name, enemy.Id, "ids keep the prefab name so kill tallies do not change");
                Assert.IsFalse(enemy.AffectedByMorale, "enemies have no morale (Q-44)");
                var attacker = enemy.Prefab.GetComponent<SiegeAttacker>();
                Assert.IsNotNull(attacker, enemy.Id + " prefab needs a SiegeAttacker");
                Assert.AreEqual(Expected(enemy.Role), attacker.Role, enemy.Id + " role tag disagrees with its prefab");
            }
            Assert.AreEqual(6, ids.Count);
        }

        [TestCase("enemy_ladder", 3, "mat_bone", 1)]
        [TestCase("enemy_gate_breaker", 5, "mat_hide", 2)]
        [TestCase("enemy_archer", 3, "mat_feather", 1)]
        [TestCase("enemy_wall_breaker", 0, null, 0)]
        [TestCase("enemy_flyer", 0, null, 0)]
        [TestCase("enemy_flyer_high_archer", 0, null, 0)]
        public void ProjectEnemies_KeepThePreMigrationKillRewards(string id, int gold, string material, int amount)
        {
            EnemyDefinition enemy = LoadCatalog().Find(id);
            Assert.IsNotNull(enemy, id);
            Assert.AreEqual(gold, enemy.KillGold);
            if (material == null) { Assert.AreEqual(0, enemy.MaterialDrops.Count); return; }
            Assert.AreEqual(1, enemy.MaterialDrops.Count);
            Assert.AreEqual(material, enemy.MaterialDrops[0].material.name);
            Assert.AreEqual(amount, enemy.MaterialDrops[0].amountPerKill);
        }

        static SiegeAttackerRole Expected(EnemyRole role) => role switch
        {
            EnemyRole.GroundLadder => SiegeAttackerRole.Ladder,
            EnemyRole.GateBreaker => SiegeAttackerRole.GateBreaker,
            EnemyRole.WallBreaker => SiegeAttackerRole.WallBreaker,
            EnemyRole.Ranged => SiegeAttackerRole.Archer,
            _ => SiegeAttackerRole.Flyer,
        };
    }
}
