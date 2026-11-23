using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Economy;
using ProjectNeo.Stats;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// COMBAT-17: 근접 유닛은 안쪽 자리가 다 찬 적 대신 옆의 빈 적으로, 원거리 유닛은 일반 적에게 몰린 화력을 나눠 쏘고
    /// (보스는 계속 집중), 구조물에 막혀 못 쏘는 수비병은 근처 빈 크레넬로 옮겨 그곳을 새 지킬 자리로 삼는다.
    /// </summary>
    public class TargetSpreadPlayTests
    {
        readonly List<Object> assets = new List<Object>();
        readonly Vector3 origin = new Vector3(4800f, 0f, 4800f);
        Scene scene;
        Scene originalScene;
        float previousCaptureDelta;
        GameObject projectilePrefab;
        UnitAISettings aiSettings;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalScene = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("TargetSpreadTest");
            SceneManager.SetActiveScene(scene);
            previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            aiSettings = Track(UnitAISettings.Create());
            projectilePrefab = new GameObject("TestProjectileTemplate");
            projectilePrefab.AddComponent<CombatProjectile>();
            UnitCombatAI.CrenelStandPoints = null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnitCombatAI.CrenelStandPoints = null;
            Time.captureDeltaTime = previousCaptureDelta;
            SceneManager.SetActiveScene(originalScene);
            yield return SceneManager.UnloadSceneAsync(scene);
            foreach (Object asset in assets) if (asset != null) Object.Destroy(asset);
            assets.Clear();
            yield return null;
        }

        T Track<T>(T value) where T : Object { assets.Add(value); return value; }

        // 바닥을 깔고(선택으로 높이 3m 통로와 성가퀴 한 칸) NavMesh를 굽는다.
        void BuildGround(bool walkway = false)
        {
            var geometry = new GameObject("Geometry");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = origin + new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            ground.transform.SetParent(geometry.transform, true);
            if (walkway)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.transform.position = origin + new Vector3(0f, 1.5f, 0f);
                block.transform.localScale = new Vector3(16f, 3f, 3f);
                block.transform.SetParent(geometry.transform, true);
                // 궁수 바로 앞 바깥(+z) 끝에 선 성가퀴 한 칸(높이 1.5m).
                var merlon = GameObject.CreatePrimitive(PrimitiveType.Cube);
                merlon.name = "Merlon";
                merlon.transform.position = origin + new Vector3(0f, 3.75f, 1.25f);
                merlon.transform.localScale = new Vector3(1.2f, 1.5f, 0.5f);
                merlon.transform.SetParent(geometry.transform, true);
            }
            var surface = geometry.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            assets.Add(surface.navMeshData);
            Physics.SyncTransforms();
        }

        CharacterActor Unit(string name, CombatTeam team, Vector3 position, AttackKind kind, bool ai,
            UnitAISettings settings = null)
        {
            var definition = Track(SoldierDefinition.Create(name, new Cost(0),
                new StatValue(StatType.MaxHealth, 100000f), new StatValue(StatType.Attack, 1f),
                new StatValue(StatType.Defense, 0f), new StatValue(StatType.AttackSpeed, 1f),
                new StatValue(StatType.AttackRange, kind == AttackKind.Projectile ? 16f : 1.5f),
                new StatValue(StatType.MoveSpeed, 3.5f)));
            var profile = Track(CombatProfile.Create(0.05f, 70f, 0.4f, 2f, kind,
                kind == AttackKind.Projectile ? projectilePrefab : null, 18f));
            var root = new GameObject(name);
            root.SetActive(false);
            root.transform.SetPositionAndRotation(origin + position, Quaternion.identity);
            var body = root.AddComponent<CapsuleCollider>();
            body.radius = profile.Radius;
            body.height = profile.Height;
            body.center = Vector3.up;
            var actor = root.AddComponent<CharacterActor>();
            actor.Configure(definition, profile, team);
            if (ai)
            {
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = profile.Radius;
                agent.height = profile.Height;
                root.AddComponent<UnitCombatAI>().Configure(settings != null ? settings : aiSettings);
            }
            root.SetActive(true);
            Physics.SyncTransforms();
            return actor;
        }

        static int CountTargeting(List<UnitCombatAI> units, CharacterActor target)
        {
            int count = 0;
            foreach (UnitCombatAI unit in units) if (ReferenceEquals(unit.Target, target)) count++;
            return count;
        }

        List<UnitCombatAI> MeleeLine(int count, UnitAISettings settings)
        {
            var units = new List<UnitCombatAI>();
            for (int i = 0; i < count; i++)
                units.Add(Unit("Melee" + i, CombatTeam.Allied, new Vector3(-4.5f + i, 0f, 0f), AttackKind.Melee, true, settings)
                    .GetComponent<UnitCombatAI>());
            return units;
        }

        [UnityTest]
        public IEnumerator MeleeLine_SaturatedFrontEnemy_SurplusGoesToEnemyBehind()
        {
            BuildGround();
            // 앞 적의 안쪽 자리는 6개다. 10명 중 남는 병사는 2.5m 뒤 빈 적으로 간다.
            CharacterActor front = Unit("Front", CombatTeam.Hostile, new Vector3(0f, 0f, 6f), AttackKind.Melee, false);
            CharacterActor back = Unit("Back", CombatTeam.Hostile, new Vector3(0f, 0f, 8.5f), AttackKind.Melee, false);
            List<UnitCombatAI> units = MeleeLine(10, aiSettings);
            for (int i = 0; i < 30; i++) yield return null;
            int toBack = CountTargeting(units, back);
            Assert.GreaterOrEqual(toBack, 2, "surplus attackers spread to the free enemy");
            Assert.GreaterOrEqual(CountTargeting(units, front), 4, "the front enemy keeps its attackers");
        }

        [UnityTest]
        public IEnumerator MeleeLine_SpreadOff_AllQueueOnFrontEnemy()
        {
            BuildGround();
            UnitAISettings off = Track(UnitAISettings.Create(targetSpread: false));
            CharacterActor front = Unit("Front", CombatTeam.Hostile, new Vector3(0f, 0f, 6f), AttackKind.Melee, false);
            Unit("Back", CombatTeam.Hostile, new Vector3(0f, 0f, 8.5f), AttackKind.Melee, false);
            List<UnitCombatAI> units = MeleeLine(10, off);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(10, CountTargeting(units, front), "without spread everyone picks the nearest enemy");
        }

        List<UnitCombatAI> HoldArchers(int count)
        {
            var units = new List<UnitCombatAI>();
            for (int i = 0; i < count; i++)
            {
                var ai = Unit("Archer" + i, CombatTeam.Allied, new Vector3(-2.5f + i, 0f, 0f), AttackKind.Projectile, true)
                    .GetComponent<UnitCombatAI>();
                ai.SetDirective(UnitDirective.Hold);
                units.Add(ai);
            }
            return units;
        }

        [UnityTest]
        public IEnumerator HoldArchers_SpreadFireAcrossGrunts()
        {
            BuildGround();
            CharacterActor near = Unit("Near", CombatTeam.Hostile, new Vector3(0f, 0f, 10f), AttackKind.Melee, false);
            CharacterActor far = Unit("Far", CombatTeam.Hostile, new Vector3(2f, 0f, 11f), AttackKind.Melee, false);
            List<UnitCombatAI> units = HoldArchers(6);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(3, CountTargeting(units, near), "three shooters on the nearer grunt");
            Assert.AreEqual(3, CountTargeting(units, far), "the rest shoot the other grunt");
        }

        [UnityTest]
        public IEnumerator HoldArchers_BossKeepsFocusFire()
        {
            BuildGround();
            CharacterActor boss = Unit("Boss", CombatTeam.Hostile, new Vector3(0f, 0f, 10f), AttackKind.Melee, false);
            boss.IsBoss = true;
            Unit("Grunt", CombatTeam.Hostile, new Vector3(2f, 0f, 11f), AttackKind.Melee, false);
            List<UnitCombatAI> units = HoldArchers(6);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(6, CountTargeting(units, boss), "bosses are not spread");
        }

        [UnityTest]
        public IEnumerator BlockedWallArcher_MovesToFreeCrenel_AndShoots()
        {
            BuildGround(true);
            Vector3 crenel = origin + new Vector3(2f, 3f, 0.6f);
            UnitCombatAI.CrenelStandPoints = (center, radius, output) =>
            {
                output.Clear();
                Vector3 offset = crenel - center;
                offset.y = 0f;
                if (offset.sqrMagnitude <= radius * radius) output.Add(crenel);
            };
            CharacterActor archer = Unit("Archer", CombatTeam.Allied, new Vector3(0f, 3f, 0.6f), AttackKind.Projectile, true);
            var ai = archer.GetComponent<UnitCombatAI>();
            ai.SetDirective(UnitDirective.Hold);
            yield return null;
            Vector3 anchor = ai.HoldPost;
            // 성벽 밖 땅 위 적. 성가퀴에 가려 지금 자리에서는 못 쏘고, 내려갈 길도 없다.
            CharacterActor enemy = Unit("Below", CombatTeam.Hostile, new Vector3(0f, 0f, 8f), AttackKind.Melee, false);
            Assert.AreEqual(ShotBlock.Structure, CombatPhysics.ClassifyShot(archer, enemy, out _), "merlon blocks the shot");
            for (int i = 0; i < 60; i++)
            {
                Assert.IsNull(ai.Target, "cannot shoot from behind the merlon");
                yield return null;
            }
            for (int i = 0; i < 300 && !ReferenceEquals(ai.Target, enemy); i++) yield return null;
            Assert.AreSame(enemy, ai.Target, "shoots from the crenel");
            Assert.Less(Vector3.Distance(ai.HoldPost, crenel), 0.6f, "the crenel becomes the new post");
            Assert.Less(Vector3.Distance(ai.CrenelAnchor, anchor), 0.01f, "the ordered post stays the search anchor");
            Assert.AreEqual(3f, archer.transform.position.y, 0.3f, "stays on the walkway");
        }

        [UnityTest]
        public IEnumerator BlockedWallArcher_WithoutCrenelSource_StaysPut()
        {
            BuildGround(true);
            CharacterActor archer = Unit("Archer", CombatTeam.Allied, new Vector3(0f, 3f, 0.6f), AttackKind.Projectile, true);
            var ai = archer.GetComponent<UnitCombatAI>();
            ai.SetDirective(UnitDirective.Hold);
            yield return null;
            Vector3 start = archer.transform.position;
            Unit("Below", CombatTeam.Hostile, new Vector3(0f, 0f, 8f), AttackKind.Melee, false);
            for (int i = 0; i < 240; i++) yield return null;
            Assert.IsNull(ai.Target);
            Assert.Less(Vector3.Distance(start, archer.transform.position), 0.3f, "no crenel data, no move");
        }
    }
}
