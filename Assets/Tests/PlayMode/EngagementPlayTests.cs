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
    /// COMBAT-12 교전: 다른 적에게 사선이 막힌 원거리 유닛은 뛰어들지 않고 막은 적을 쏘며,
    /// 근접 유닛 여럿은 대상 둘레 자리로 나뉘어 붙고 자리가 차면 바깥 링에서 기다린다.
    /// </summary>
    public class EngagementPlayTests
    {
        readonly List<Object> assets = new List<Object>();
        readonly Vector3 origin = new Vector3(4000f, 0f, 4000f);
        Scene scene;
        Scene originalScene;
        float previousCaptureDelta;
        GameObject projectilePrefab;
        UnitAISettings aiSettings;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalScene = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("EngagementTest");
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
            float health = 100f, float range = 16f, float attack = 1f)
        {
            var definition = Track(SoldierDefinition.Create(name, new Cost(0),
                new StatValue(StatType.MaxHealth, health), new StatValue(StatType.Attack, attack),
                new StatValue(StatType.Defense, 0f), new StatValue(StatType.AttackSpeed, 1f),
                new StatValue(StatType.AttackRange, range), new StatValue(StatType.MoveSpeed, 3.5f)));
            var profile = Track(CombatProfile.Create(0.05f, 70f, 0.4f, 2f, kind,
                kind == AttackKind.Projectile ? projectilePrefab : null, 18f));
            var root = new GameObject(name);
            root.SetActive(false);
            Vector3 world = origin + position;
            Vector3 facing = origin - world;
            facing.y = 0f;
            root.transform.SetPositionAndRotation(world,
                facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing) : Quaternion.identity);
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

        [UnityTest]
        public IEnumerator RangedEngage_BlockedByAnotherEnemy_ShootsTheBlocker_InsteadOfCharging()
        {
            // 궁수(0,0,0)가 14m 앞의 적을 노리는 중에, 다른 적이 6m 앞 사선 위로 들어온다.
            CharacterActor archer = Unit("Archer", CombatTeam.Allied, Vector3.zero, AttackKind.Projectile, true);
            var ai = archer.GetComponent<UnitCombatAI>();
            CharacterActor far = Unit("FarEnemy", CombatTeam.Hostile, new Vector3(0f, 0f, 14f), AttackKind.Melee, false, 1000f);
            for (int i = 0; i < 30 && ai.Target != far; i++) yield return null;
            Assert.AreSame(far, ai.Target, "first target is the only enemy");
            Vector3 start = archer.transform.position;
            CharacterActor blocker = Unit("Blocker", CombatTeam.Hostile, new Vector3(0f, 0f, 6f), AttackKind.Melee, false, 1000f);
            Assert.AreEqual(ShotBlock.LivingEnemy, CombatPhysics.ClassifyShot(archer, far, out CharacterActor found));
            Assert.AreSame(blocker, found);
            // 대상 전환 대기(1.5초)보다 짧게 본다. 예전에는 이 동안 정지 거리 0으로 앞으로 뛰어들었다.
            for (int i = 0; i < 60 && ai.Target != blocker; i++) yield return null;
            Assert.AreSame(blocker, ai.Target, "switches to the enemy that blocks the shot");
            // 공격 간격 1초 + 화살 비행 시간 안에 맞힌다.
            for (int i = 0; i < 150 && blocker.Health.Current >= blocker.Health.Maximum; i++) yield return null;
            Assert.Less(blocker.Health.Current, blocker.Health.Maximum, "shoots the blocker");
            Assert.Less(Vector3.Distance(start, archer.transform.position), 0.5f, "does not rush into melee");
        }

        [UnityTest]
        public IEnumerator MeleeAttackers_SpreadOverRingSlots_AroundOneTarget()
        {
            // 같은 쪽(남쪽 8m)에서 근접 4명이 한 적에게 붙는다. 각자 다른 자리를 맡고, 모두 닿는 거리에 선다.
            CharacterActor target = Unit("Target", CombatTeam.Hostile, Vector3.zero, AttackKind.Melee, false, 100000f);
            var units = new List<UnitCombatAI>();
            for (int i = 0; i < 4; i++)
                units.Add(Unit("Melee" + i, CombatTeam.Allied, new Vector3(-1.5f + i, 0f, -8f), AttackKind.Melee, true,
                    range: 1.5f).GetComponent<UnitCombatAI>());
            for (int i = 0; i < 300; i++) yield return null;
            var slots = new HashSet<(int, int)>();
            foreach (UnitCombatAI unit in units)
            {
                Assert.AreSame(target, unit.Target);
                Assert.IsTrue(unit.TryGetMeleeSlot(out int ring, out int index), unit.name + " has a slot");
                Assert.IsTrue(slots.Add((ring, index)), unit.name + " shares a slot");
                Assert.AreEqual(MeleeRingRules.InnerRing, ring, "4 attackers fit on the inner ring");
                Assert.IsTrue(CombatPhysics.IsInRange(unit.Actor, target), unit.name + " reaches the target");
            }
            for (int a = 0; a < units.Count; a++)
                for (int b = a + 1; b < units.Count; b++)
                {
                    Vector3 offset = units[a].transform.position - units[b].transform.position;
                    offset.y = 0f;
                    Assert.Greater(offset.magnitude, 0.5f, units[a].name + " and " + units[b].name + " stand apart");
                }
        }

        [UnityTest]
        public IEnumerator MeleeAttackers_WaitOnOuterRing_WhenInnerRingIsFull()
        {
            // 안쪽 링 자리 6개보다 많은 8명: 2명은 바깥 링을 맡는다.
            CharacterActor target = Unit("Target", CombatTeam.Hostile, Vector3.zero, AttackKind.Melee, false, 100000f);
            var units = new List<UnitCombatAI>();
            for (int i = 0; i < 8; i++)
                units.Add(Unit("Melee" + i, CombatTeam.Allied, new Vector3(-3.5f + i, 0f, -8f), AttackKind.Melee, true,
                    range: 1.5f).GetComponent<UnitCombatAI>());
            for (int i = 0; i < 300; i++) yield return null;
            int inner = 0, outer = 0;
            var slots = new HashSet<(int, int)>();
            foreach (UnitCombatAI unit in units)
            {
                Assert.IsTrue(unit.TryGetMeleeSlot(out int ring, out int index), unit.name + " has a slot");
                Assert.IsTrue(slots.Add((ring, index)), unit.name + " shares a slot");
                if (ring == MeleeRingRules.InnerRing) inner++; else outer++;
            }
            int innerCount = MeleeRingRules.SlotCount(MeleeRingRules.InnerRadius(0.4f, 0.4f, aiSettings.MeleeSlotGap, 1.5f),
                aiSettings.MeleeSlotSpacing);
            Assert.AreEqual(innerCount, inner, "inner ring fully used");
            Assert.AreEqual(units.Count - innerCount, outer, "the rest wait on the outer ring");
        }
    }
}
