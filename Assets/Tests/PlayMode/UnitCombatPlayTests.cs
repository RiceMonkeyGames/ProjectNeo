using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public class UnitCombatPlayTests
    {
        readonly List<Object> assets = new List<Object>();
        readonly Vector3 origin = new Vector3(1000f, 0f, 1000f);
        Scene scene;
        Scene originalScene;
        float previousCaptureDelta;
        GameObject geometry;
        GameObject projectilePrefab;
        UnitAISettings aiSettings;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalScene = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("UnitCombatTest");
            SceneManager.SetActiveScene(scene);
            previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            geometry = new GameObject("Geometry");
            Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f), geometry.transform);
            Box("Island", new Vector3(30f, -0.5f, 0f), new Vector3(4f, 1f, 4f), geometry.transform);
            var surface = geometry.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            assets.Add(surface.navMeshData);
            aiSettings = Track(UnitAISettings.Create());
            projectilePrefab = new GameObject("TestProjectileTemplate");
            projectilePrefab.AddComponent<CombatProjectile>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = previousCaptureDelta;
            SceneManager.SetActiveScene(originalScene);
            yield return SceneManager.UnloadSceneAsync(scene);
            foreach (Object asset in assets) if (asset != null) Object.Destroy(asset);
            assets.Clear();
            yield return null;
        }

        T Track<T>(T value) where T : Object { assets.Add(value); return value; }

        GameObject Box(string name, Vector3 position, Vector3 scale, Transform parent = null)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = origin + position;
            box.transform.localScale = scale;
            if (parent != null) box.transform.SetParent(parent, true);
            return box;
        }

        CharacterActor Unit(string name, CombatTeam team, Vector3 position, bool ai = false,
            bool ranged = false, float attack = 30f, float defense = 0f, float health = 100f, float projectileSpeed = 18f)
        {
            var definition = Track(SoldierDefinition.Create(name, new Cost(0),
                new StatValue(StatType.MaxHealth, health), new StatValue(StatType.Attack, attack),
                new StatValue(StatType.Defense, defense), new StatValue(StatType.AttackSpeed, 2f),
                new StatValue(StatType.AttackRange, ranged ? 15f : 2.5f), new StatValue(StatType.MoveSpeed, 3.5f)));
            var profile = Track(CombatProfile.Create(0.05f, 70f, 0.4f, 2f,
                ranged ? AttackKind.Projectile : AttackKind.Melee, projectilePrefab, projectileSpeed));
            var root = new GameObject(name);
            root.SetActive(false);
            root.transform.position = origin + position;
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
                root.AddComponent<UnitCombatAI>().Configure(aiSettings);
            }
            root.SetActive(true);
            Physics.SyncTransforms();
            return actor;
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Melee_UsesDefense_AndIgnoresFriendlyBody()
        {
            var attacker = Unit("Attacker", CombatTeam.Allied, Vector3.zero);
            var ally = Unit("Ally", CombatTeam.Allied, new Vector3(0f, 0f, 0.8f));
            var enemy = Unit("Enemy", CombatTeam.Hostile, new Vector3(0f, 0f, 2f), defense: 7f);
            Assert.IsFalse(attacker.TryAttack(ally));
            Assert.IsTrue(attacker.TryAttack(enemy));
            yield return Frames(12);
            Assert.AreEqual(77f, enemy.Health.Current);
            Assert.AreEqual(100f, ally.Health.Current);
        }

        [UnityTest]
        public IEnumerator Target_RemainsLockedUntilDeath_ThenChanges()
        {
            var actor = Unit("AI", CombatTeam.Allied, Vector3.zero, ai: true);
            actor.Stats.SetBase(StatType.MoveSpeed, 0f);
            var first = Unit("First", CombatTeam.Hostile, new Vector3(0f, 0f, 5f));
            var second = Unit("Second", CombatTeam.Hostile, new Vector3(8f, 0f, 0f));
            var ai = actor.GetComponent<UnitCombatAI>();
            yield return Frames(3);
            Assert.AreSame(first, ai.Target);
            second.transform.position = origin + new Vector3(1f, 0f, 0f);
            Physics.SyncTransforms();
            yield return Frames(30);
            Assert.AreSame(first, ai.Target);
            first.ReceiveDamage(1000f);
            yield return Frames(3);
            Assert.AreSame(second, ai.Target);
        }

        [UnityTest]
        public IEnumerator Target_UnreachableTargetIsReplacedByReachableEnemy()
        {
            var actor = Unit("AI", CombatTeam.Allied, Vector3.zero, ai: true);
            actor.Stats.SetBase(StatType.MoveSpeed, 0f);
            var first = Unit("First", CombatTeam.Hostile, new Vector3(0f, 0f, 5f));
            var second = Unit("Second", CombatTeam.Hostile, new Vector3(-8f, 0f, 0f));
            var ai = actor.GetComponent<UnitCombatAI>();
            yield return Frames(3);
            Assert.AreSame(first, ai.Target);
            first.transform.position = origin + new Vector3(30f, 0f, 0f);
            Physics.SyncTransforms();
            yield return Frames(30);
            Assert.AreSame(second, ai.Target);
        }

        [UnityTest]
        public IEnumerator Projectile_PassesAllies_AndAppliesDefenseAtImpact()
        {
            var archer = Unit("Archer", CombatTeam.Allied, Vector3.zero, ranged: true);
            var ally = Unit("Ally", CombatTeam.Allied, new Vector3(0f, 0f, 4f));
            var enemy = Unit("Enemy", CombatTeam.Hostile, new Vector3(0f, 0f, 10f), defense: 8f);
            Assert.IsTrue(archer.TryAttack(enemy));
            yield return Frames(60);
            Assert.AreEqual(78f, enemy.Health.Current);
            Assert.AreEqual(100f, ally.Health.Current);
        }

        [UnityTest]
        public IEnumerator Projectile_DoesNotFollowTargetAfterLaunch()
        {
            var archer = Unit("Archer", CombatTeam.Allied, Vector3.zero, ranged: true);
            var enemy = Unit("Enemy", CombatTeam.Hostile, new Vector3(0f, 0f, 10f));
            Assert.IsTrue(archer.TryAttack(enemy));
            yield return Frames(8);
            enemy.transform.position += Vector3.right * 4f;
            Physics.SyncTransforms();
            yield return Frames(60);
            Assert.AreEqual(100f, enemy.Health.Current);
        }

        [UnityTest]
        public IEnumerator FastProjectile_StopsAtThinWall()
        {
            var archer = Unit("Archer", CombatTeam.Allied, Vector3.zero, ranged: true, projectileSpeed: 1200f);
            var enemy = Unit("Enemy", CombatTeam.Hostile, new Vector3(0f, 0f, 10f));
            Box("ThinWall", new Vector3(0f, 1f, 4f), new Vector3(4f, 2f, 0.05f));
            Physics.SyncTransforms();
            var projectile = Object.Instantiate(projectilePrefab, archer.AimPoint, Quaternion.identity).GetComponent<CombatProjectile>();
            projectile.Launch(archer, 30f, Vector3.forward, 15f);
            yield return Frames(3);
            Assert.IsTrue(projectile == null);
            Assert.AreEqual(100f, enemy.Health.Current);
        }

        [UnityTest]
        public IEnumerator Death_StopsAgentAndRemovesUnitFromTargetCandidates()
        {
            var actor = Unit("AI", CombatTeam.Allied, Vector3.zero, ai: true);
            Unit("Enemy", CombatTeam.Hostile, new Vector3(0f, 0f, 10f));
            yield return Frames(20);
            actor.ReceiveDamage(1000f);
            Vector3 position = actor.transform.position;
            yield return Frames(20);
            Assert.IsFalse(actor.GetComponent<NavMeshAgent>().enabled);
            Assert.AreEqual(position, actor.transform.position);
            Assert.IsFalse(CharacterActor.ActiveActors.Contains(actor));
        }

        [UnityTest]
        public IEnumerator SerializedBattle_TenVersusTen_StrongerArmyWins()
        {
            yield return SceneManager.LoadSceneAsync("UnitBattlePrototype", LoadSceneMode.Additive);
            Scene battle = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            try
            {
                var units = battle.GetRootGameObjects().Select(root => root.GetComponent<CharacterActor>())
                    .Where(actor => actor != null).ToArray();
                Assert.AreEqual(10, units.Count(unit => unit.Team == CombatTeam.Allied));
                Assert.AreEqual(10, units.Count(unit => unit.Team == CombatTeam.Hostile));
                Assert.AreEqual(6, units.Count(unit => unit.Profile.AttackKind == AttackKind.Projectile));
                int frames = 0;
                while (frames++ < 2400 && units.Any(unit => unit.Team == CombatTeam.Hostile && !unit.Health.IsDead))
                    yield return null;
                Assert.IsTrue(units.Where(unit => unit.Team == CombatTeam.Hostile).All(unit => unit.Health.IsDead),
                    "40초 안에 적을 모두 쓰러뜨려야 한다.");
                Assert.IsTrue(units.Any(unit => unit.Team == CombatTeam.Allied && !unit.Health.IsDead));
            }
            finally
            {
                SceneManager.UnloadSceneAsync(battle);
            }
            yield return null;
        }
    }
}
