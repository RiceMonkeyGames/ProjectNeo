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
        public IEnumerator MoveDirective_StopsAtTheClosestReachablePoint_WhenDestinationIsCutOff()
        {
            var unit = Unit("Mover", CombatTeam.Allied, Vector3.zero, ai: true);
            var ai = unit.GetComponent<UnitCombatAI>();
            yield return null;
            // 섬은 본토와 떨어져 있어 끝까지 갈 길이 없다.
            ai.SetDirective(UnitDirective.Move, origin + new Vector3(30f, 0f, 0f));
            int frames = 0;
            while (ai.Directive == UnitDirective.Move && frames++ < 1200) yield return null;
            Assert.AreEqual(UnitDirective.Hold, ai.Directive, "갈 수 없는 목적지에서 이동이 끝나지 않았다.");
            Assert.That(unit.transform.position.x - origin.x, Is.InRange(15f, 20.5f));
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
        public IEnumerator Hold_FindsEnemyWhoseOffsetColliderIsInRange_WhenRootIsFarAway()
        {
            var actor = Unit("Holder", CombatTeam.Allied, Vector3.zero, ai: true);
            actor.Stats.SetBase(StatType.MoveSpeed, 0f);
            var enemy = Unit("OffsetBody", CombatTeam.Hostile, new Vector3(0f, 0f, 10f));
            // Root is outside both attack range and Hold chase radius, but the actual body is in reach.
            var body = enemy.GetComponent<CapsuleCollider>();
            body.center = new Vector3(0f, 1f, -8f);
            Physics.SyncTransforms();
            var ai = actor.GetComponent<UnitCombatAI>();
            ai.SetDirective(UnitDirective.Hold);
            yield return Frames(3);
            Assert.AreSame(enemy, ai.Target, "Broad-phase filtering must retain an in-range offset collider.");
        }

        [UnityTest]
        public IEnumerator Hold_EmptyTargetSearch_UsesDecisionInterval_AndReacquiresAfterDeath()
        {
            yield return CheckEmptyTargetSearch(UnitDirective.Hold);
        }

        [UnityTest]
        public IEnumerator Engage_EmptyTargetSearch_UsesDecisionInterval_AndReacquiresAfterDeath()
        {
            yield return CheckEmptyTargetSearch(UnitDirective.Engage);
        }

        IEnumerator CheckEmptyTargetSearch(UnitDirective directive)
        {
            var actor = Unit("WaitingAI", CombatTeam.Allied, Vector3.zero, ai: true);
            actor.Stats.SetBase(StatType.MoveSpeed, 0f);
            var ai = actor.GetComponent<UnitCombatAI>();
            ai.SetDirective(directive);
            yield return Frames(3);
            Assert.IsNull(ai.Target);
            var first = Unit("FirstArrival", CombatTeam.Hostile, new Vector3(0f, 0f, 4f));
            yield return Frames(2);
            Assert.IsNull(ai.Target, "Empty-target scans must honor the configured decision interval.");
            yield return Frames(Mathf.CeilToInt(aiSettings.DecisionInterval / Time.captureDeltaTime) + 2);
            Assert.AreSame(first, ai.Target, "A new enemy must be acquired on the next decision tick.");
            var second = Unit("Replacement", CombatTeam.Hostile, new Vector3(3f, 0f, 0f));
            first.ReceiveDamage(1000f);
            yield return Frames(2);
            Assert.AreSame(second, ai.Target, "A dead existing target must be replaced without waiting for the next tick.");
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

        [UnityTest]
        public IEnumerator Hold_RetaliatesOutsideHoldChaseRadius_WithinRetaliationLeash_AndReturnsToPost()
        {
            var defender = Unit("Defender", CombatTeam.Allied, Vector3.zero, ai: true, health: 200f);
            var archer = Unit("EnemyArcher", CombatTeam.Hostile, new Vector3(0f, 0f, 10f), ranged: true, health: 40f);
            var ai = defender.GetComponent<UnitCombatAI>();
            ai.SetDirective(UnitDirective.Hold);
            yield return Frames(10);
            Assert.IsNull(ai.Target, "Hold 반경(6m) 밖 적은 선제 추격하지 않아야 한다.");

            defender.ReceiveDamage(10f, archer);
            yield return Frames(5);
            Assert.AreEqual(archer, ai.Target, "반격 한계(15m) 안의 공격자에게는 반격 타겟을 잡아야 한다.");

            int frames = 0;
            while (frames++ < 600 && !archer.Health.IsDead) yield return null;
            Assert.IsTrue(archer.Health.IsDead, "반격 추격으로 적을 처치해야 한다.");

            frames = 0;
            while (frames++ < 600 && (defender.transform.position - origin).magnitude > 1.2f) yield return null;
            Assert.LessOrEqual((defender.transform.position - origin).magnitude, 1.2f, "반격 후 HoldPost로 복귀해야 한다.");
        }

        [UnityTest]
        public IEnumerator Hold_DoesNotRetaliate_BeyondRetaliationLeash()
        {
            var defender = Unit("Defender", CombatTeam.Allied, Vector3.zero, ai: true, health: 200f);
            var farArcher = Unit("FarArcher", CombatTeam.Hostile, new Vector3(0f, 0f, 18f), ranged: true);
            var ai = defender.GetComponent<UnitCombatAI>();
            ai.SetDirective(UnitDirective.Hold);
            yield return Frames(5);

            defender.ReceiveDamage(10f, farArcher);
            yield return Frames(20);
            Assert.IsNull(ai.Target, "Hold 반격 한계(15m) 밖의 적에게는 자리를 이탈해 추격하지 않아야 한다.");
            Assert.LessOrEqual((defender.transform.position - origin).magnitude, 0.5f);
        }

        [UnityTest]
        public IEnumerator Engage_SwitchesTarget_WhenHitByMeleeWhileChasingFleeingTarget()
        {
            var chaser = Unit("Chaser", CombatTeam.Hostile, Vector3.zero, ai: true, health: 200f);
            var farPlayer = Unit("FarPlayer", CombatTeam.Allied, new Vector3(0f, 0f, 15f), health: 200f);
            var ai = chaser.GetComponent<UnitCombatAI>();
            yield return Frames(10);
            Assert.AreEqual(farPlayer, ai.Target, "먼저 먼 대상을 추적해야 한다.");

            yield return Frames(100); // 1.67초 경과 (> 1.5초 쿨다운)
            var nearbyAlly = Unit("NearbyAlly", CombatTeam.Allied, chaser.transform.position - origin + new Vector3(1.5f, 0f, 0f), health: 200f);
            chaser.ReceiveDamage(10f, nearbyAlly);
            yield return null;
            Assert.AreEqual(nearbyAlly, ai.Target, "먼 대상을 쫓는 중 옆에서 근접 공격당하면 타겟을 전환해야 한다.");
        }

        [UnityTest]
        public IEnumerator InRangeCombat_IgnoresRangedHit_FromOtherEnemy()
        {
            var fighter = Unit("Fighter", CombatTeam.Allied, Vector3.zero, ai: true, health: 200f);
            var meleeEnemy = Unit("MeleeEnemy", CombatTeam.Hostile, new Vector3(0f, 0f, 1.5f), health: 200f);
            var rangedEnemy = Unit("RangedEnemy", CombatTeam.Hostile, new Vector3(0f, 0f, 12f), ranged: true, health: 200f);
            var ai = fighter.GetComponent<UnitCombatAI>();
            yield return Frames(105); // 교전 시작 + 쿨다운 경과
            Assert.AreEqual(meleeEnemy, ai.Target);

            fighter.ReceiveDamage(10f, rangedEnemy);
            yield return Frames(5);
            Assert.AreEqual(meleeEnemy, ai.Target, "이미 사거리 안에서 교전 중일 때는 다른 원거리 피격에 타겟을 바꾸지 않아야 한다.");
        }

        [UnityTest]
        public IEnumerator MoveDirective_And_SuppressedRetaliation_IgnoreHits()
        {
            var unit = Unit("Mover", CombatTeam.Allied, Vector3.zero, ai: true, health: 200f);
            var attacker = Unit("Attacker", CombatTeam.Hostile, new Vector3(0f, 0f, 10f), ranged: true, health: 200f);
            var ai = unit.GetComponent<UnitCombatAI>();
            ai.SetDirective(UnitDirective.Move, origin + new Vector3(15f, 0f, 0f));
            yield return Frames(3);

            unit.ReceiveDamage(10f, attacker);
            yield return Frames(3);
            Assert.IsNull(ai.Target, "Move 지시 중에는 피격당해도 적을 무시해야 한다.");

            ai.SetDirective(UnitDirective.Hold);
            ai.SetRetaliationSuppressed(true);
            yield return Frames(3);
            unit.ReceiveDamage(10f, attacker);
            yield return Frames(3);
            Assert.IsNull(ai.Target, "공성 임무 등 반격 억제 상태에서는 피격당해도 타겟을 바꾸지 않아야 한다.");
        }
    }
}
