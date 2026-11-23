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
    /// COMBAT-14: 원거리 유닛은 위협이 큰 대상(사다리 적 등)을 먼저 쏘고, 붙은 근접 적으로 바로 대상을 바꿔 짧게 물러나며,
    /// 대형 방향을 받은 자리 지키기는 싸울 대상이 없으면 그 방향으로 돌아선다.
    /// </summary>
    public class RangedThreatPlayTests
    {
        sealed class FixedThreat : ITargetThreatSource
        {
            public TargetThreat Threat { get; set; }
        }

        readonly List<Object> assets = new List<Object>();
        readonly Vector3 origin = new Vector3(4400f, 0f, 4400f);
        Scene scene;
        Scene originalScene;
        float previousCaptureDelta;
        GameObject projectilePrefab;
        UnitAISettings aiSettings;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalScene = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("RangedThreatTest");
            SceneManager.SetActiveScene(scene);
            previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            var geometry = new GameObject("Geometry");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = origin + new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(80f, 1f, 80f);
            ground.transform.SetParent(geometry.transform, true);
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

        CharacterActor Unit(string name, CombatTeam team, Vector3 position, AttackKind kind, bool ai,
            UnitAISettings settings = null, float health = 1000f, float attack = 1f)
        {
            var definition = Track(SoldierDefinition.Create(name, new Cost(0),
                new StatValue(StatType.MaxHealth, health), new StatValue(StatType.Attack, attack),
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

        [UnityTest]
        public IEnumerator HoldArcher_PrefersLadderClimber_OverCloserPlainEnemy()
        {
            // 8m 앞 일반 적과 12m 옆 사다리 적. 사다리 적(배율 0.5)은 6m처럼 보여 먼저 고른다.
            CharacterActor archer = Unit("Archer", CombatTeam.Allied, Vector3.zero, AttackKind.Projectile, true);
            var ai = archer.GetComponent<UnitCombatAI>();
            ai.SetDirective(UnitDirective.Hold);
            Unit("Plain", CombatTeam.Hostile, new Vector3(0f, 0f, 8f), AttackKind.Melee, false);
            CharacterActor climber = Unit("Climber", CombatTeam.Hostile, new Vector3(6f, 0f, 10.4f), AttackKind.Melee, false);
            climber.ThreatSource = new FixedThreat { Threat = TargetThreat.LadderClimber };
            for (int i = 0; i < 30 && ai.Target == null; i++) yield return null;
            Assert.AreSame(climber, ai.Target, "ladder climber comes first");
        }

        [UnityTest]
        public IEnumerator HoldArcher_SwitchesToNewLadderClimber_AfterTargetLock()
        {
            CharacterActor archer = Unit("Archer", CombatTeam.Allied, Vector3.zero, AttackKind.Projectile, true);
            var ai = archer.GetComponent<UnitCombatAI>();
            ai.SetDirective(UnitDirective.Hold);
            CharacterActor plain = Unit("Plain", CombatTeam.Hostile, new Vector3(0f, 0f, 8f), AttackKind.Melee, false);
            CharacterActor other = Unit("Other", CombatTeam.Hostile, new Vector3(-6f, 0f, 10.4f), AttackKind.Melee, false);
            for (int i = 0; i < 30 && ai.Target == null; i++) yield return null;
            Assert.AreSame(plain, ai.Target, "nearest plain enemy first");
            // 다른 적이 사다리를 오르기 시작한다. 대상 유지(1.5초)와 재검사 간격이 지나면 그 적으로 바꾼다.
            var threat = new FixedThreat { Threat = TargetThreat.LadderClimber };
            other.ThreatSource = threat;
            for (int i = 0; i < 300 && ai.Target != other; i++) yield return null;
            Assert.AreSame(other, ai.Target, "switches to the higher threat it can shoot");
        }

        [UnityTest]
        public IEnumerator EngageArcher_MeleeEnemyCloses_SwitchesAndFallsBack()
        {
            CharacterActor archer = Unit("Archer", CombatTeam.Allied, Vector3.zero, AttackKind.Projectile, true);
            var ai = archer.GetComponent<UnitCombatAI>();
            CharacterActor far = Unit("Far", CombatTeam.Hostile, new Vector3(0f, 0f, 14f), AttackKind.Melee, false);
            for (int i = 0; i < 30 && ai.Target != far; i++) yield return null;
            Assert.AreSame(far, ai.Target);
            // 대상 유지 시간 안에 근접 적이 2m 옆에 붙는다.
            CharacterActor rusher = Unit("Rusher", CombatTeam.Hostile, new Vector3(2f, 0f, 0f), AttackKind.Melee, false);
            float before = Vector3.Distance(archer.transform.position, rusher.transform.position);
            bool fellBack = false;
            for (int i = 0; i < 30 && ai.Target != rusher; i++) yield return null;
            Assert.AreSame(rusher, ai.Target, "switches to the melee threat without waiting for the lock");
            for (int i = 0; i < 90; i++)
            {
                fellBack |= ai.IsFallingBack;
                yield return null;
            }
            Assert.IsTrue(fellBack, "starts a short fallback");
            float after = Vector3.Distance(archer.transform.position, rusher.transform.position);
            Assert.Greater(after, before + 1.5f, "moves away from the melee enemy");
            Assert.Less(after, before + aiSettings.RangedFallbackDistance + 1f, "only a short step");
        }

        [UnityTest]
        public IEnumerator HoldArcher_FallbackDisabled_OnlySwitchesTarget()
        {
            UnitAISettings noFallback = Track(UnitAISettings.Create(rangedFallbackDistance: 0f));
            CharacterActor archer = Unit("Archer", CombatTeam.Allied, Vector3.zero, AttackKind.Projectile, true, noFallback);
            var ai = archer.GetComponent<UnitCombatAI>();
            ai.SetDirective(UnitDirective.Hold);
            CharacterActor far = Unit("Far", CombatTeam.Hostile, new Vector3(0f, 0f, 14f), AttackKind.Melee, false);
            for (int i = 0; i < 30 && ai.Target != far; i++) yield return null;
            Assert.AreSame(far, ai.Target);
            CharacterActor rusher = Unit("Rusher", CombatTeam.Hostile, new Vector3(2f, 0f, 0f), AttackKind.Melee, false);
            Vector3 start = archer.transform.position;
            for (int i = 0; i < 30 && ai.Target != rusher; i++) yield return null;
            Assert.AreSame(rusher, ai.Target);
            for (int i = 0; i < 60; i++)
            {
                Assert.IsFalse(ai.IsFallingBack);
                yield return null;
            }
            Assert.Less(Vector3.Distance(start, archer.transform.position), 0.3f, "stays on post");
        }

        // 본 바닥과 떨어진 곳에 바닥과 높이 3m 상자(성벽 통로/기둥)를 만들고 NavMesh를 따로 굽는다. 상자 윗면 중심(origin 기준)을 돌려준다.
        Vector3 BuildRaisedBlock(Vector3 localCenter, Vector3 blockSize)
        {
            var geometry = new GameObject("RaisedGeometry");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = origin + localCenter + new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(30f, 1f, 30f);
            ground.transform.SetParent(geometry.transform, true);
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.transform.position = origin + localCenter + new Vector3(0f, blockSize.y * 0.5f, 0f);
            block.transform.localScale = blockSize;
            block.transform.SetParent(geometry.transform, true);
            var surface = geometry.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            assets.Add(surface.navMeshData);
            return localCenter + new Vector3(0f, blockSize.y, 0f);
        }

        [UnityTest]
        public IEnumerator HoldArcherOnWalkway_ThreatFromSide_SidestepsAlongWalkway()
        {
            // 폭 3m 통로(걸을 수 있는 폭 약 2m). 근접 적이 앞옆에 붙어 정반대 방향은 통로 밖 허공이라 옆으로 비켜서야 한다.
            Vector3 top = BuildRaisedBlock(new Vector3(200f, 0f, 0f), new Vector3(16f, 3f, 3f));
            CharacterActor archer = Unit("Archer", CombatTeam.Allied, top, AttackKind.Projectile, true);
            var ai = archer.GetComponent<UnitCombatAI>();
            ai.SetDirective(UnitDirective.Hold);
            yield return null;
            Vector3 start = archer.transform.position;
            CharacterActor rusher = Unit("Rusher", CombatTeam.Hostile, top + new Vector3(0.5f, 0f, 1f),
                AttackKind.Melee, false);
            float before = HorizontalDistance(archer.transform.position, rusher.transform.position);
            for (int i = 0; i < 30 && ai.Target != rusher; i++) yield return null;
            Assert.AreSame(rusher, ai.Target, "switches to the melee threat");
            bool fellBack = false;
            for (int i = 0; i < 90; i++)
            {
                fellBack |= ai.IsFallingBack;
                yield return null;
            }
            Assert.IsTrue(fellBack, "finds a sidestep along the walkway");
            Vector3 end = archer.transform.position;
            Assert.AreEqual(start.y, end.y, 0.3f, "stays on the walkway");
            Assert.Less(Mathf.Abs(end.z - start.z), 1.2f, "stays inside the walkway width");
            Assert.Less(end.x, start.x - 1.5f, "moves along the walkway, away from the threat side");
            Assert.Greater(HorizontalDistance(end, rusher.transform.position), before + 1.5f, "separation grows");
            Assert.LessOrEqual(HorizontalDistance(end, ai.HoldPost), aiSettings.RangedFallbackHoldLeash + 0.3f,
                "within the hold leash");
        }

        [UnityTest]
        public IEnumerator HoldArcherOnPillar_NoRoomInAnyDirection_StaysAndShoots()
        {
            // 3m 기둥 위(걸을 수 있는 곳 약 2m). 다섯 방향 후보 모두 허공이라 물러나지 않고 대상만 바꾼다.
            Vector3 top = BuildRaisedBlock(new Vector3(-200f, 0f, 0f), new Vector3(3f, 3f, 3f));
            Assert.IsTrue(NavMesh.SamplePosition(origin + top, out _, 0.3f, NavMesh.AllAreas), "pillar top has NavMesh");
            CharacterActor archer = Unit("Archer", CombatTeam.Allied, top, AttackKind.Projectile, true);
            var ai = archer.GetComponent<UnitCombatAI>();
            ai.SetDirective(UnitDirective.Hold);
            yield return null;
            Vector3 start = archer.transform.position;
            CharacterActor rusher = Unit("Rusher", CombatTeam.Hostile, top + new Vector3(0f, 0f, 1f),
                AttackKind.Melee, false);
            for (int i = 0; i < 30 && ai.Target != rusher; i++) yield return null;
            Assert.AreSame(rusher, ai.Target, "switches to the melee threat");
            for (int i = 0; i < 90; i++)
            {
                Assert.IsFalse(ai.IsFallingBack, "no fallback point on a pillar");
                yield return null;
            }
            Assert.Less(Vector3.Distance(start, archer.transform.position), 0.3f, "stays on the pillar");
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        [UnityTest]
        public IEnumerator HoldWithFormationFacing_TurnsBackWhenIdle()
        {
            CharacterActor unit = Unit("Soldier", CombatTeam.Allied, Vector3.zero, AttackKind.Melee, true);
            var ai = unit.GetComponent<UnitCombatAI>();
            ai.SetDirective(UnitDirective.Hold, unit.transform.position, 90f);
            Assert.AreEqual(90f, ai.HoldFacing.Value, 1e-4f);
            // 회전 속도 540도/초라 90도는 0.2초 안에 돈다.
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(0f, Mathf.DeltaAngle(unit.transform.eulerAngles.y, 90f), HoldFacingRules.Tolerance + 0.1f);
            // 다른 쪽을 보게 돌려도 다시 대형 방향으로 돌아온다.
            unit.transform.rotation = Quaternion.Euler(0f, 250f, 0f);
            for (int i = 0; i < 40; i++) yield return null;
            Assert.AreEqual(0f, Mathf.DeltaAngle(unit.transform.eulerAngles.y, 90f), HoldFacingRules.Tolerance + 0.1f);
        }

        [UnityTest]
        public IEnumerator PlainHold_HasNoFacing_AndDoesNotTurn()
        {
            CharacterActor unit = Unit("Soldier", CombatTeam.Allied, Vector3.zero, AttackKind.Melee, true);
            var ai = unit.GetComponent<UnitCombatAI>();
            unit.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            ai.SetDirective(UnitDirective.Hold);
            Assert.IsFalse(ai.HoldFacing.HasValue);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(0f, Mathf.DeltaAngle(unit.transform.eulerAngles.y, 30f), 0.5f);
        }

        [UnityTest]
        public IEnumerator MoveWithFacing_ArrivesAndFacesFormationDirection()
        {
            CharacterActor unit = Unit("Soldier", CombatTeam.Allied, Vector3.zero, AttackKind.Melee, true);
            var ai = unit.GetComponent<UnitCombatAI>();
            // 앞(+z)으로 걸어가 도착한 뒤 왼쪽(-90도)을 본다.
            ai.SetDirective(UnitDirective.Move, origin + new Vector3(0f, 0f, 4f), -90f);
            for (int i = 0; i < 300 && ai.Directive != UnitDirective.Hold; i++) yield return null;
            Assert.AreEqual(UnitDirective.Hold, ai.Directive, "arrives");
            for (int i = 0; i < 40; i++) yield return null;
            Assert.AreEqual(0f, Mathf.DeltaAngle(unit.transform.eulerAngles.y, -90f), HoldFacingRules.Tolerance + 0.1f);
        }
    }
}
