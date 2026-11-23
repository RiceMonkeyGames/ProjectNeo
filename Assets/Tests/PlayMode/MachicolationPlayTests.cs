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
    /// SIEGE-12 마치콜레이션: 성문 위 구멍에 선 아군 궁수가 성문 앞에 붙은 적을 아래로 쏜다.
    /// 3m 높이, 바깥면 z = +1.5인 성문 상자 위에 구멍 하나(사격 자리 z = 0.65)를 직접 놓아 확인한다.
    /// </summary>
    public class MachicolationPlayTests
    {
        const float GateHeight = 3f;
        readonly List<Object> assets = new List<Object>();
        readonly Vector3 origin = new Vector3(3000f, 0f, 3000f);
        Scene scene;
        Scene originalScene;
        float previousCaptureDelta;
        GameObject projectilePrefab;
        UnitAISettings aiSettings;
        Transform gate;
        ShotOpening opening;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalScene = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("MachicolationTest");
            SceneManager.SetActiveScene(scene);
            previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            var geometry = new GameObject("Geometry");
            Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 60f), geometry.transform);
            gate = Box("Gate", new Vector3(0f, GateHeight * 0.5f, 0f), new Vector3(8f, GateHeight, 3f), geometry.transform);
            var surface = geometry.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            assets.Add(surface.navMeshData);
            aiSettings = Track(UnitAISettings.Create());
            projectilePrefab = new GameObject("TestProjectileTemplate");
            projectilePrefab.AddComponent<CombatProjectile>();

            var body = new GameObject("Machicolation");
            body.transform.SetPositionAndRotation(origin + new Vector3(0f, GateHeight, 0.65f), Quaternion.identity);
            opening = body.AddComponent<ShotOpening>();
            opening.Configure(CombatTeam.Allied, MachicolationShaft.FromFace(3f, 0.85f, 1.2f, 2f, GateHeight, 0.5f, 3f),
                0.6f, 12f, new[] { gate }, new MachicolationAttack[] { Track(ArrowMachicolationAttack.Create()) });
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

        Transform Box(string name, Vector3 position, Vector3 scale, Transform parent)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = origin + position;
            box.transform.localScale = scale;
            box.transform.SetParent(parent, true);
            return box.transform;
        }

        CharacterActor Unit(string name, CombatTeam team, Vector3 position, bool ai = false, float facingYaw = 0f)
        {
            var definition = Track(SoldierDefinition.Create(name, new Cost(0),
                new StatValue(StatType.MaxHealth, 100f), new StatValue(StatType.Attack, 30f),
                new StatValue(StatType.Defense, 0f), new StatValue(StatType.AttackSpeed, 2f),
                new StatValue(StatType.AttackRange, 16f), new StatValue(StatType.MoveSpeed, 3.5f)));
            var profile = Track(CombatProfile.Create(0.05f, 70f, 0.4f, 2f, AttackKind.Projectile, projectilePrefab, 18f));
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

        static IEnumerator WaitForHit(CharacterActor target, int frames = 180)
        {
            for (int i = 0; i < frames && target.Health.Current >= target.Health.Maximum; i++) yield return null;
        }

        // 성문 바깥면(z = +1.5)에 붙은 적. 몸통 반지름 0.4라 면에서 0.1m 떨어진다.
        static readonly Vector3 GateBase = new Vector3(0f, 0f, 2f);

        [UnityTest]
        public IEnumerator ArcherAtOpening_ShootsEnemyHuggingTheGate()
        {
            CharacterActor archer = Unit("GateArcher", CombatTeam.Allied, new Vector3(0f, GateHeight, 0.65f));
            CharacterActor enemy = Unit("GateBreaker", CombatTeam.Hostile, GateBase, facingYaw: 180f);
            Assert.AreSame(opening, ShotOpening.At(archer));
            Assert.IsTrue(CombatPhysics.HasClearShot(archer, enemy), "구멍 통로 안에서는 성문 조각이 막지 않는다.");
            Assert.IsTrue(archer.TryAttack(enemy));
            yield return WaitForHit(enemy);
            Assert.Less(enemy.Health.Current, enemy.Health.Maximum, "구멍으로 쏜 화살이 성문 앞 적에게 닿지 않았다.");
        }

        [UnityTest]
        public IEnumerator ArcherAwayFromOpening_IsStillBlockedByItsFooting()
        {
            CharacterActor archer = Unit("WallArcher", CombatTeam.Allied, new Vector3(-3f, GateHeight, 0f));
            CharacterActor enemy = Unit("GateBreaker", CombatTeam.Hostile, GateBase, facingYaw: 180f);
            Assert.IsNull(ShotOpening.At(archer));
            Assert.IsFalse(CombatPhysics.HasClearShot(archer, enemy));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnemyArcherOnTheOpening_CannotUseIt()
        {
            CharacterActor intruder = Unit("Intruder", CombatTeam.Hostile, new Vector3(0f, GateHeight, 0.65f));
            CharacterActor defender = Unit("Defender", CombatTeam.Allied, GateBase, facingYaw: 180f);
            Assert.IsNull(ShotOpening.At(intruder));
            Assert.IsFalse(CombatPhysics.HasClearShot(intruder, defender));
            yield return null;
        }

        [UnityTest]
        public IEnumerator HoldArcher_WalksToFreeOpening_ShootsDown_AndReturnsToPost()
        {
            CharacterActor archer = Unit("WallArcher", CombatTeam.Allied, new Vector3(-3f, GateHeight, 0f), ai: true);
            var ai = archer.GetComponent<UnitCombatAI>();
            Vector3 post = archer.transform.position;
            ai.SetDirective(UnitDirective.Hold, post);
            yield return null;
            CharacterActor enemy = Unit("GateBreaker", CombatTeam.Hostile, GateBase, facingYaw: 180f);
            for (int i = 0; i < 600 && !enemy.Health.IsDead; i++) yield return null;
            Assert.IsTrue(enemy.Health.IsDead, "구멍으로 가서 성문 앞 적을 쏘아 쓰러뜨려야 한다.");
            for (int i = 0; i < 600 && Vector3.Distance(post, archer.transform.position) > 0.5f; i++) yield return null;
            Assert.IsNull(ai.Opening, "아래에 적이 없으면 구멍을 놓는다.");
            Assert.Less(Vector3.Distance(post, archer.transform.position), 0.5f, "구멍을 놓고 자리로 돌아온다.");
            Assert.IsNull(opening.Claimant);
        }

        [UnityTest]
        public IEnumerator HoldArcher_DoesNotTakeOpening_SomeoneElseIsStandingOn()
        {
            Unit("Player", CombatTeam.Allied, new Vector3(0f, GateHeight, 0.65f));
            CharacterActor archer = Unit("WallArcher", CombatTeam.Allied, new Vector3(-3f, GateHeight, 0f), ai: true);
            var ai = archer.GetComponent<UnitCombatAI>();
            ai.SetDirective(UnitDirective.Hold, archer.transform.position);
            Unit("GateBreaker", CombatTeam.Hostile, GateBase, facingYaw: 180f);
            for (int i = 0; i < 90; i++) yield return null;
            Assert.IsNull(ai.Opening);
            Assert.IsNull(opening.Claimant);
        }

        [UnityTest]
        public IEnumerator HoldArcher_ReleasesOpening_EvenWhenSquadNeighborhoodClears()
        {
            CharacterActor archer = Unit("WallArcher", CombatTeam.Allied, new Vector3(-3f, GateHeight, 0f), ai: true);
            var ai = archer.GetComponent<UnitCombatAI>();
            Vector3 post = archer.transform.position;
            ai.SetDirective(UnitDirective.Hold, post);
            yield return null;

            CharacterActor enemy = Unit("GateBreaker", CombatTeam.Hostile, GateBase, facingYaw: 180f);
            for (int i = 0; i < 300 && ai.Opening == null; i++) yield return null;
            Assert.AreSame(opening, ai.Opening, "궁수가 성문 앞 적을 보고 구멍을 점유해야 한다.");
            Assert.AreSame(ai, opening.Claimant);

            // 적이 제거되고 동시에 분대 대기 슬립(squadNeighborhoodClear=true)이 걸려도 구멍 점유를 놓아야 한다(BUG-ai-core-defects).
            enemy.Health.TakeDamage(9999f);
            ai.SetSquadNeighborhoodClear(true);
            for (int i = 0; i < 120 && ai.Opening != null; i++) yield return null;
            Assert.IsNull(ai.Opening, "분대 슬립 상태여도 적이 없으면 마치콜레이션 점유를 해제해야 한다.");
            Assert.IsNull(opening.Claimant, "해제 후 다른 아군이 쓸 수 있도록 Claimant가 비어야 한다.");
        }
    }
}
