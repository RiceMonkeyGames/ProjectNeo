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
    /// 성벽 위 궁수가 아래의 적을 쏠 때의 사거리와 화살 도달(BUG-wall-archer-los).
    /// 3층(9m) 성벽 위와 땅을 만든 시험 지형에서, 사거리 11의 궁수로 확인한다.
    /// </summary>
    public class WallTopArcherPlayTests
    {
        const float WallHeight = 9f;
        const float Range = 11f;
        readonly List<Object> assets = new List<Object>();
        readonly Vector3 origin = new Vector3(2000f, 0f, 2000f);
        Scene scene;
        Scene originalScene;
        float previousCaptureDelta;
        GameObject projectilePrefab;
        UnitAISettings aiSettings;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalScene = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("WallTopArcherTest");
            SceneManager.SetActiveScene(scene);
            previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            var geometry = new GameObject("Geometry");
            Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 60f), geometry.transform);
            // 바깥면이 z = +1.5인 성벽. 궁수는 윗면 한가운데(바깥 모서리에서 1.5m 안쪽)에 선다.
            Box("Wall", new Vector3(0f, WallHeight * 0.5f, 0f), new Vector3(8f, WallHeight, 3f), geometry.transform);
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

        void Box(string name, Vector3 position, Vector3 scale, Transform parent)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = origin + position;
            box.transform.localScale = scale;
            box.transform.SetParent(parent, true);
        }

        CharacterActor Archer(string name, CombatTeam team, Vector3 position, float highGroundBonus, bool ai = false,
            float facingYaw = 0f)
        {
            var definition = Track(SoldierDefinition.Create(name, new Cost(0),
                new StatValue(StatType.MaxHealth, 100f), new StatValue(StatType.Attack, 30f),
                new StatValue(StatType.Defense, 0f), new StatValue(StatType.AttackSpeed, 2f),
                new StatValue(StatType.AttackRange, Range), new StatValue(StatType.MoveSpeed, 3.5f)));
            var profile = Track(CombatProfile.Create(0.05f, 70f, 0.4f, 2f, AttackKind.Projectile, projectilePrefab, 18f,
                highGroundRangeBonus: highGroundBonus));
            var root = new GameObject(name);
            root.SetActive(false);
            root.transform.SetPositionAndRotation(origin + position, Quaternion.Euler(0f, facingYaw, 0f));
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

        // 성벽 위 한가운데의 궁수에게서, 몸통의 가장 가까운 점까지가 reach가 되는 땅 위의 자리(성 바깥쪽).
        // 가까운 점은 머리 쪽 둥근 끝이다: 중심 높이 1.6m, 반지름 0.4m.
        static Vector3 GroundAtReach(float reach)
        {
            float toCap = reach + 0.4f;
            float drop = WallHeight + 1f - 1.6f;
            return new Vector3(0f, 0f, Mathf.Sqrt(toCap * toCap - drop * drop));
        }

        static IEnumerator WaitForHit(CharacterActor target, int frames = 180)
        {
            for (int i = 0; i < frames && target.Health.Current >= target.Health.Maximum; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator ArrowReachesEveryTargetThatPassedTheRangeCheck_WhenShootingSteeplyDown()
        {
            // 보너스 없이도, 사거리 안(10.95m)이라고 판정한 대상에게는 화살이 닿아야 한다.
            // 겨눈 가슴까지는 약 11.7m라서, 고치기 전에는 화살이 사거리 11m에서 사라져 닿지 않았다.
            CharacterActor archer = Archer("WallArcher", CombatTeam.Allied, new Vector3(0f, WallHeight, 0f), 0f);
            CharacterActor enemy = Archer("GroundEnemy", CombatTeam.Hostile, GroundAtReach(10.95f), 0f, facingYaw: 180f);
            Assert.AreEqual(Range, CombatPhysics.AttackRange(archer, enemy), 0.001f, "보너스가 0이면 사거리는 그대로다.");
            Assert.Greater(Vector3.Distance(archer.AimPoint, enemy.AimPoint), Range, "겨눈 지점은 사거리보다 멀다.");
            Assert.IsTrue(archer.CanAttack(enemy));
            Assert.IsTrue(archer.TryAttack(enemy));
            yield return WaitForHit(enemy);
            Assert.Less(enemy.Health.Current, enemy.Health.Maximum, "사거리 안이라고 판정한 대상에게 화살이 닿지 않았다.");
        }

        [UnityTest]
        public IEnumerator HighGround_ExtendsRangeDownwardOnly_AndTheArrowLands()
        {
            // 기본 보너스 25%: 9m 위에서 아래로는 13.75m, 아래에서 위로와 같은 높이끼리는 11m 그대로다.
            CharacterActor archer = Archer("WallArcher", CombatTeam.Allied, new Vector3(0f, WallHeight, 0f), 0.25f);
            CharacterActor enemy = Archer("GroundEnemy", CombatTeam.Hostile, GroundAtReach(12.8f), 0.25f, facingYaw: 180f);
            CharacterActor groundAlly = Archer("GroundAlly", CombatTeam.Allied, GroundAtReach(12.8f) + new Vector3(0f, 0f, 12.5f), 0.25f,
                facingYaw: 180f);
            Assert.AreEqual(Range * 1.25f, CombatPhysics.AttackRange(archer, enemy), 0.001f);
            Assert.AreEqual(Range * 1.25f, CombatPhysics.MaximumAttackRange(archer), 0.001f);
            Assert.AreEqual(Range, CombatPhysics.AttackRange(enemy, archer), 0.001f, "위로 쏘는 쪽은 보너스가 없다.");
            Assert.AreEqual(Range, CombatPhysics.AttackRange(groundAlly, enemy), 0.001f, "같은 높이끼리는 보너스가 없다.");
            Assert.IsTrue(archer.CanAttack(enemy), "높은 곳에서는 기본 사거리 밖(12.8m)도 쏜다.");
            Assert.IsFalse(enemy.CanAttack(archer), "아래의 적은 같은 거리에서 위를 쏘지 못한다.");
            Assert.IsFalse(groundAlly.CanAttack(enemy), "땅에서는 12m 밖의 적을 쏘지 못한다.");
            Assert.IsTrue(archer.TryAttack(enemy));
            yield return WaitForHit(enemy);
            Assert.Less(enemy.Health.Current, enemy.Health.Maximum, "늘어난 사거리 끝의 적에게도 화살이 닿아야 한다.");
        }

        [UnityTest]
        public IEnumerator HoldArcherOnWallTop_FindsAndShootsEnemyInsideHighGroundRange_WithoutLeavingPost()
        {
            // 뒷줄 궁수처럼 기본 사거리에서 1~2m 모자란 자리에서도, 자리를 지킨 채 아래의 적을 찾아 쏜다.
            CharacterActor archer = Archer("WallArcher", CombatTeam.Allied, new Vector3(0f, WallHeight, 0f), 0.25f, ai: true);
            var ai = archer.GetComponent<UnitCombatAI>();
            Vector3 post = archer.transform.position;
            ai.SetDirective(UnitDirective.Hold, post);
            yield return null;
            CharacterActor enemy = Archer("GroundEnemy", CombatTeam.Hostile, GroundAtReach(12.8f), 0.25f, facingYaw: 180f);
            yield return WaitForHit(enemy, 300);
            Assert.AreSame(enemy, ai.Target, "사거리 안에 들어온 아래의 적을 대상으로 잡아야 한다.");
            Assert.Less(enemy.Health.Current, enemy.Health.Maximum, "성벽 위 궁수가 아래의 적을 쏘지 못했다.");
            Assert.Less(Vector3.Distance(post, archer.transform.position), 0.3f, "자리를 떠나지 않는다.");
        }

        [UnityTest]
        public IEnumerator HoldArcherOnWallTop_StaysOnPost_WhenEnemyIsBeyondHighGroundRange()
        {
            // 늘어난 사거리 밖이고 걸어갈 길도 없으면 자리를 지킨다(쫓아 내려가지 않는다).
            CharacterActor archer = Archer("WallArcher", CombatTeam.Allied, new Vector3(0f, WallHeight, 0f), 0.25f, ai: true);
            var ai = archer.GetComponent<UnitCombatAI>();
            Vector3 post = archer.transform.position;
            ai.SetDirective(UnitDirective.Hold, post);
            CharacterActor enemy = Archer("GroundEnemy", CombatTeam.Hostile, GroundAtReach(15f), 0.25f, facingYaw: 180f);
            for (int i = 0; i < 120; i++) yield return null;
            Assert.IsNull(ai.Target);
            Assert.AreEqual(enemy.Health.Maximum, enemy.Health.Current);
            Assert.Less(Vector3.Distance(post, archer.transform.position), 0.3f);
        }
    }
}
