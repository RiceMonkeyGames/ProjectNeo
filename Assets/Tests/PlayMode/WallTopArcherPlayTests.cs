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
        NavMeshSurface surface;

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
            surface = geometry.AddComponent<NavMeshSurface>();
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

        // 성벽 오른쪽 끝(x = +4)에서 땅(x = +22)까지 내려가는 경사로(약 27도). 무너진 틈과 계단으로 성 밖에 닿는 길을 흉내 낸다.
        void AddRampAndRebuild()
        {
            const float run = 18f;
            const float thickness = 1f;
            float angle = Mathf.Atan2(WallHeight, run);
            var up = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f);
            Vector3 topMiddle = new Vector3(4f + run * 0.5f, WallHeight * 0.5f, 0f);
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Ramp";
            ramp.transform.SetPositionAndRotation(origin + topMiddle - up * (thickness * 0.5f),
                Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg));
            ramp.transform.localScale = new Vector3(Mathf.Sqrt(run * run + WallHeight * WallHeight), thickness, 3f);
            ramp.transform.SetParent(surface.transform, true);
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            assets.Add(surface.navMeshData);
        }

        static bool HasWalkablePath(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            return NavMesh.SamplePosition(from, out NavMeshHit start, 1f, NavMesh.AllAreas)
                && NavMesh.SamplePosition(to, out NavMeshHit end, 1f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete;
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
            // 이렇게 가파른 각도는 한가운데서는 발밑 성벽에 가리므로(BUG-wall-archer-shoots-through-footing),
            // 궁수와 대상을 함께 바깥 모서리 쪽(모서리에서 0.6m 안)으로 옮긴다.
            var nearEdge = new Vector3(0f, 0f, 0.9f);
            CharacterActor archer = Archer("WallArcher", CombatTeam.Allied, new Vector3(0f, WallHeight, 0f) + nearEdge, 0f);
            CharacterActor enemy = Archer("GroundEnemy", CombatTeam.Hostile, GroundAtReach(10.95f) + nearEdge, 0f,
                facingYaw: 180f);
            Assert.AreEqual(Range, CombatPhysics.AttackRange(archer, enemy), 0.001f, "보너스가 0이면 사거리는 그대로다.");
            Assert.Greater(Vector3.Distance(archer.AimPoint, enemy.AimPoint), Range, "겨눈 지점은 사거리보다 멀다.");
            Assert.IsTrue(archer.CanAttack(enemy));
            Assert.IsTrue(archer.TryAttack(enemy));
            yield return WaitForHit(enemy);
            Assert.Less(enemy.Health.Current, enemy.Health.Maximum, "사거리 안이라고 판정한 대상에게 화살이 닿지 않았다.");
        }

        [UnityTest]
        public IEnumerator WallArcher_CannotShootThroughItsOwnWallPiece_AtEnemyHuggingTheWallBase()
        {
            // BUG-wall-archer-shoots-through-footing: 성벽 바깥면(z = +1.5)에 바짝 붙은 적은 한가운데 궁수의
            // 발밑 성벽 몸통 뒤에 숨는다. 사선 검사도, 억지로 쏜 화살도 성벽을 뚫지 못한다.
            CharacterActor archer = Archer("WallArcher", CombatTeam.Allied, new Vector3(0f, WallHeight, 0f), 0.25f);
            CharacterActor enemy = Archer("GroundEnemy", CombatTeam.Hostile, new Vector3(0f, 0f, 2f), 0.25f, facingYaw: 180f);
            Assert.IsTrue(CombatPhysics.IsInRange(archer, enemy), "사거리 안이어야 사선만 따질 수 있다.");
            Assert.IsFalse(CombatPhysics.HasClearShot(archer, enemy), "발밑 성벽 몸통을 지나는 선은 막혀야 한다.");
            Assert.IsFalse(archer.CanAttack(enemy));
            Vector3 launch = ArrowBallistics.LaunchVelocity(archer.AimPoint, enemy.AimPoint,
                archer.Profile.ProjectileSpeed, archer.Profile.ProjectileGravity);
            GameObject shot = Object.Instantiate(projectilePrefab, archer.AimPoint, Quaternion.LookRotation(launch));
            shot.GetComponent<CombatProjectile>().Launch(archer, 30f, launch);
            yield return WaitForHit(enemy);
            Assert.AreEqual(enemy.Health.Maximum, enemy.Health.Current, "화살이 발밑 성벽을 뚫고 맞혔다.");
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

        [UnityTest]
        public IEnumerator HoldArcherOnWallTop_DoesNotLeaveTheWall_WhenAPathDownExists()
        {
            // BUG-wall-archer-leaves-wall: 성 밖으로 내려가는 길이 있어도, 중지 중에는 성벽을 내려가 쫓지 않는다.
            AddRampAndRebuild();
            yield return null;
            CharacterActor archer = Archer("WallArcher", CombatTeam.Allied, new Vector3(0f, WallHeight, 0f), 0.25f, ai: true);
            var ai = archer.GetComponent<UnitCombatAI>();
            Vector3 post = archer.transform.position;
            ai.SetDirective(UnitDirective.Hold, post);
            CharacterActor enemy = Archer("GroundEnemy", CombatTeam.Hostile, GroundAtReach(15f), 0.25f, facingYaw: 180f);
            Assert.IsTrue(HasWalkablePath(post, enemy.transform.position), "시험 지형에 성벽에서 적까지 내려가는 길이 있어야 한다.");
            Assert.Less(Vector3.Distance(enemy.transform.position, post), ai.HoldChaseRadius, "적은 중지 추격 반경 안에 있다.");
            for (int i = 0; i < 180; i++) yield return null;
            Assert.IsNull(ai.Target, "성벽 아래로 내려가야 닿는 적은 중지 중의 대상이 아니다.");
            Assert.Less(Vector3.Distance(post, archer.transform.position), 0.3f, "성벽 위 자리를 떠나지 않는다.");
        }

        [UnityTest]
        public IEnumerator EngageArcherOnWallTop_StillLeavesTheWall_ToChaseAnEnemyBelow()
        {
            // 공격(Engage) 지시는 지금처럼 성벽을 내려가 쫓아도 된다. 위 시험의 지형이 실제로 내려가는 길을 주는지도 확인한다.
            AddRampAndRebuild();
            yield return null;
            CharacterActor archer = Archer("WallArcher", CombatTeam.Allied, new Vector3(0f, WallHeight, 0f), 0.25f, ai: true);
            var ai = archer.GetComponent<UnitCombatAI>();
            Vector3 post = archer.transform.position;
            Assert.AreEqual(UnitDirective.Engage, ai.Directive);
            CharacterActor enemy = Archer("GroundEnemy", CombatTeam.Hostile, GroundAtReach(15f), 0.25f, facingYaw: 180f);
            Assert.IsTrue(HasWalkablePath(post, enemy.transform.position), "시험 지형에 성벽에서 적까지 내려가는 길이 있어야 한다.");
            for (int i = 0; i < 180; i++) yield return null;
            Assert.Greater(Vector3.Distance(post, archer.transform.position), 2f, "공격 지시에서는 성벽을 내려가 쫓는다.");
        }
    }
}
