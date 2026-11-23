using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Economy;
using ProjectNeo.Siege;
using ProjectNeo.Squads;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// BUG-ai-core-defects 회귀 검증:
    /// 1) 성벽 위 Hold 근접 수비병이 벽 아래 다수 적에 가리지 않고 같은 층 도달 가능 적을 찾는다.
    /// 2) 성벽 위 수비병이 섞여 있어도 BattleFlowFieldService의 지면 높이가 지상 기준으로 유지되고,
    ///    Flow Field는 원경(LOD2) 지상 유닛에게만 적용된다.
    /// 3) 아군 분대가 벽 너머로 이동할 때 접근 예약으로 여러 사다리에 분산된다.
    /// </summary>
    public class AiCoreDefectsPlayTests
    {
        readonly List<Object> assets = new List<Object>();
        readonly Vector3 origin = new Vector3(3600f, 0f, 3600f);
        Scene scene;
        Scene originalScene;
        float previousCaptureDelta;
        CursorLockMode previousLock;
        bool previousVisible;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalScene = SceneManager.GetActiveScene();
            previousCaptureDelta = Time.captureDeltaTime;
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            Time.captureDeltaTime = 1f / 60f;
            BattleFlowFieldService.Invalidate();
            AILodContext.ClearViewOverride();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            AILodContext.ClearViewOverride();
            BattleFlowFieldService.Invalidate();
            Time.captureDeltaTime = previousCaptureDelta;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
            if (originalScene.IsValid() && originalScene.isLoaded)
                SceneManager.SetActiveScene(originalScene);
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
            foreach (Object asset in assets)
                if (asset != null) Object.Destroy(asset);
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

        UnitCombatAI SpawnUnit(string name, CombatTeam team, Vector3 localPos, UnitAISettings aiSettings,
            float range = 1.8f, float maxHp = 200f)
        {
            var def = Track(SoldierDefinition.Create(name, new Cost(0),
                new StatValue(StatType.MaxHealth, maxHp), new StatValue(StatType.Attack, 10f),
                new StatValue(StatType.Defense, 0f), new StatValue(StatType.AttackSpeed, 1f),
                new StatValue(StatType.AttackRange, range), new StatValue(StatType.MoveSpeed, 3.5f)));
            var profile = Track(CombatProfile.Create(0.1f, 70f, 0.4f, 2f, AttackKind.Melee));
            var root = new GameObject(name);
            root.SetActive(false);
            root.transform.position = origin + localPos;
            var body = root.AddComponent<CapsuleCollider>();
            body.radius = profile.Radius;
            body.height = profile.Height;
            body.center = Vector3.up;
            var actor = root.AddComponent<CharacterActor>();
            actor.Configure(def, profile, team);
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = profile.Radius;
            agent.height = profile.Height;
            var ai = root.AddComponent<UnitCombatAI>();
            ai.Configure(aiSettings);
            root.SetActive(true);
            Assert.IsTrue(agent.isOnNavMesh, $"{name} must stand on NavMesh");
            return ai;
        }

        [UnityTest]
        public IEnumerator HoldWallDefender_FindsSameLevelEnemy_WhenLowerLevelEnemiesCrowdTopK()
        {
            scene = SceneManager.CreateScene("AiCoreDefects_HoldWall");
            SceneManager.SetActiveScene(scene);

            var geometry = new GameObject("Geometry");
            Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f), geometry.transform);
            Box("Wall", new Vector3(0f, 1.5f, 0f), new Vector3(16f, 3f, 4f), geometry.transform);
            var surface = geometry.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            assets.Add(surface.navMeshData);

            UnitAISettings settings = Track(UnitAISettings.Create());
            // 성벽 위(y=3) 중앙에 근접 수비병 배치.
            UnitCombatAI defender = SpawnUnit("WallDefender", CombatTeam.Allied, new Vector3(0f, 3f, 0f), settings);
            defender.SetDirective(UnitDirective.Hold, origin + new Vector3(0f, 3f, 0f));

            // 성벽 바로 아래(y=0, 수평 2.3m -> 3D 거리 ~3.8m)에 5명의 도달 불가 적을 배치해 기존 상위 K=4를 모두 채운다.
            for (int i = 0; i < 5; i++)
            {
                UnitCombatAI below = SpawnUnit($"GroundEnemy_{i}", CombatTeam.Hostile,
                    new Vector3((i - 2) * 0.6f, 0f, 2.3f), settings);
                below.SetDirective(UnitDirective.Hold, below.transform.position);
            }
            // 같은 성벽 위(y=3, 거리 4.8m < HoldEngageRadius 6m)에 도달 가능한 적 1명 배치.
            UnitCombatAI climber = SpawnUnit("WallEnemy", CombatTeam.Hostile, new Vector3(4.8f, 3f, 0f), settings);
            climber.SetDirective(UnitDirective.Hold, climber.transform.position);

            for (int f = 0; f < 120 && !ReferenceEquals(defender.Target, climber.Actor); f++)
                yield return null;

            Assert.AreSame(climber.Actor, defender.Target,
                "벽 아래 도달 불가 적이 상위 K를 채워도 성벽 위 같은 층 적을 찾아 교전해야 한다.");
        }

        [UnityTest]
        public IEnumerator FlowField_UsesMinimumGroundElevation_AndOnlyAppliesToLod2GroundUnits()
        {
            scene = SceneManager.CreateScene("AiCoreDefects_FlowField");
            SceneManager.SetActiveScene(scene);

            var geometry = new GameObject("Geometry");
            Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(200f, 1f, 200f), geometry.transform);
            Box("HighWall", new Vector3(0f, 3f, -10f), new Vector3(30f, 6f, 4f), geometry.transform);
            var surface = geometry.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            assets.Add(surface.navMeshData);

            UnitAISettings settings = Track(UnitAISettings.Create());
            // 카메라는 지상 남쪽(z=-50)에서 북쪽(+z)을 본다.
            // z=-30은 카메라 20m(LOD0), z=+25는 카메라 75m(LOD2), 양쪽 간격 55m(DetectionRange 60m 안, FlowFieldMinDistance 18m 밖).
            AILodContext.SetViewOverride(origin + new Vector3(0f, 1.7f, -50f), Vector3.forward, topDown: false);

            // 성벽 위(y=6) 아군 14명 배치 (평균 높이를 크게 끌어올리는 조건).
            for (int i = 0; i < 14; i++)
            {
                UnitCombatAI wallAlly = SpawnUnit($"WallAlly_{i}", CombatTeam.Allied,
                    new Vector3((i - 7) * 1.8f, 6f, -10f), settings);
                wallAlly.SetDirective(UnitDirective.Hold, wallAlly.transform.position);
            }

            // 지상 근경(LOD0) 아군 1명: z=-30 (카메라 20m, 적까지 55m).
            UnitCombatAI lod0GroundAlly = SpawnUnit("Lod0GroundAlly", CombatTeam.Allied,
                new Vector3(0f, 0f, -30f), settings);
            lod0GroundAlly.SetDirective(UnitDirective.Engage);
            lod0GroundAlly.SetDecisionOffset(0f);

            // 지상 원경(LOD2) 적 12명: z=+25 (카메라 75m -> LOD2, 지상 아군까지 55m).
            UnitCombatAI lod2GroundEnemy = null;
            for (int i = 0; i < 12; i++)
            {
                UnitCombatAI enemy = SpawnUnit($"Lod2Enemy_{i}", CombatTeam.Hostile,
                    new Vector3((i - 6) * 2f, 0f, 25f), settings);
                enemy.SetDirective(UnitDirective.Engage);
                if (i == 6)
                {
                    enemy.SetDecisionOffset(0f);
                    lod2GroundEnemy = enemy;
                }
            }
            // 총 27명 >= 24 (FlowField 활성 임계 충족).
            Physics.SyncTransforms();

            for (int f = 0; f < 60; f++)
                yield return null;

            Assert.AreEqual(AILodTier.Lod0, lod0GroundAlly.LodTier, "카메라 20m 정면 유닛은 LOD0이어야 한다.");
            Assert.IsFalse(lod0GroundAlly.IsUsingFlowField, "LOD0 유닛은 Flow Field 대신 정밀 NavMesh를 써야 한다.");

            Assert.AreEqual(AILodTier.Lod2, lod2GroundEnemy.LodTier, "카메라 75m 원경 적은 LOD2여야 한다.");
            Assert.AreEqual(0f, BattleFlowFieldService.GroundElevation, 0.25f,
                "성벽 위 수비병이 과반이어도 Flow Field 지면 기준 높이는 지상(0m)이어야 한다.");
            Assert.IsTrue(lod2GroundEnemy.IsUsingFlowField,
                "지상 높이가 정상 베이크되어 원경(LOD2) 지상 유닛이 Flow Field로 행군해야 한다.");
        }

        [UnityTest]
        public IEnumerator AlliedSquadMove_WithTwoLadders_DistributesAcrossBothViaApproachReservation()
        {
            yield return SceneManager.LoadSceneAsync("SiegePrototype", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            SceneManager.SetActiveScene(scene);

            var world = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ConstructionWorld>(true)).Single();
            var director = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SiegeDirector>(true)).Single();
            yield return null;

            director.enabled = false;
            var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerInputController>(true)).Single();
            player.enabled = false;
            var commandInput = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SquadCommandInput>(true)).Single();
            commandInput.enabled = false;

            // 성벽 틈을 막아 지상 도보 경로를 차단하고 사다리만 성벽 위로 이어지게 만든다.
            BuildPieceDefinition wall = world.Structures.First(p => p.Placement.Definition.Kind == BuildPieceKind.Wall).Placement.Definition;
            foreach (int x in new[] { 4, 11, 12 })
                world.TryPlace(wall, new Vector2Int(x, 8), 0, out _);
            for (int f = 0; f < 600 && (world.IsRebuilding || world.RebuildPending); f++)
                yield return null;

            List<SiegeLadder> ladders = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<SiegeLadder>(true))
                .Where(l => l.isActiveAndEnabled && l.HasWall)
                .OrderBy(l => l.BasePoint.x)
                .ToList();
            Assert.GreaterOrEqual(ladders.Count, 2, "SiegePrototype must have at least two active wall ladders.");
            SiegeLadder ladderA = ladders[0];
            SiegeLadder ladderB = ladders[1];

            // 두 사다리 사이 외곽 들판(입구에서 충분히 먼 위치, Waiting.Count == 0 상태 유지)에 아군 4명 배치.
            var commandCenter = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SquadCommandCenter>(true)).Single();
            List<UnitCombatAI> allies = commandCenter.Squads.SelectMany(s => s.Members)
                .Where(m => m != null && m.Actor != null && m.Actor.Health != null && !m.Actor.Health.IsDead)
                .Take(4)
                .ToList();
            Assert.AreEqual(4, allies.Count, "Need 4 allied units.");

            // 두 사다리 사이 외곽 들판(입구에서 10m 떨어진 위치, Waiting.Count == 0 상태 유지)에 아군 4명 배치.
            // 목표 지점도 두 사다리 꼭대기 중간으로 두어 접근 예약 페널티가 자연스럽게 양쪽으로 분산되게 한다.
            Vector3 midpoint = Vector3.Lerp(ladderA.BasePoint, ladderB.BasePoint, 0.48f) + ladderA.OutwardWorld * 10f;
            Assert.IsTrue(NavMesh.SamplePosition(midpoint, out NavMeshHit startHit, 4f, NavMesh.AllAreas));
            Vector3 topMid = Vector3.Lerp(ladderA.TopPoint, ladderB.TopPoint, 0.5f);
            Vector3 wallTopDestination = NavMesh.SamplePosition(topMid, out NavMeshHit topHit, 3f, NavMesh.AllAreas)
                && Mathf.Abs(topHit.position.y - ladderA.TopPoint.y) < 1f ? topHit.position : ladderA.TopPoint;

            for (int i = 0; i < allies.Count; i++)
            {
                UnitCombatAI ally = allies[i];
                var agent = ally.GetComponent<NavMeshAgent>();
                agent.enabled = true;
                ally.enabled = true;
                Vector3 spawn = startHit.position + Vector3.right * ((i - 1.5f) * 0.9f);
                if (NavMesh.SamplePosition(spawn, out NavMeshHit unitHit, 2f, NavMesh.AllAreas))
                    agent.Warp(unitHit.position);
                else
                    agent.Warp(startHit.position);
                ally.SetDirective(UnitDirective.Move, wallTopDestination);
            }

            // 몇 프레임만 진행해 아직 어느 누구도 사다리 입구에 도착하기 전에 경로 예약이 분산되는지 확인한다.
            for (int f = 0; f < 10; f++)
                yield return null;

            int countA = 0, countB = 0;
            foreach (UnitCombatAI ally in allies)
            {
                var traveller = ally.GetComponent<LadderTraveller>();
                Assert.IsNotNull(traveller, $"{ally.name} should attach LadderTraveller");
                if (traveller.AssignedLadder == ladderA) countA++;
                else if (traveller.AssignedLadder == ladderB) countB++;
            }

            Assert.Greater(countA, 0, "최소 1명 이상은 첫 번째 사다리를 예약해야 한다.");
            Assert.Greater(countB, 0, "접근 예약 분산으로 두 번째 사다리에도 배분되어야 한다.");
            Assert.AreEqual(0, ladderA.Waiting.Count, "입구 도착 전(Waiting=0) 시점에 이미 분산이 이루어져야 한다.");
            Assert.Greater(ladderA.Approaching.Count + ladderB.Approaching.Count, 0, "접근 예약 목록이 채워져야 한다.");
        }
    }
}
