using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Siege;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>SIEGE-17: 길찾기로 닿지 않는 사다리 건너뛰기와 여러 성벽 파괴병의 자리 나누기.</summary>
    public class SiegeApproachPlayTests
    {
        Scene scene;
        ConstructionWorld world;
        SiegeDirector director;
        SiegeLadder[] ladders;
        FallSettings fall;
        float previousCapture;
        CursorLockMode previousLock;
        bool previousVisible;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousCapture = Time.captureDeltaTime;
            previousLock = Cursor.lockState; previousVisible = Cursor.visible;
            Time.captureDeltaTime = 1f / 30f;
            yield return SceneManager.LoadSceneAsync("SiegePrototype", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            world = Find<ConstructionWorld>(); director = Find<SiegeDirector>();
            ladders = scene.GetRootGameObjects().Select(root => root.GetComponent<SiegeLadder>())
                .Where(ladder => ladder != null).OrderBy(ladder => ladder.Cell.x).ToArray();
            yield return null;
            director.enabled = false;
            Find<PlayerInputController>().enabled = false;
            Find<SquadCommandInput>().enabled = false;
            // 걸어서 닿는 아군이 없어야 공성 판단(사다리·성벽)을 한다.
            foreach (var member in Find<SquadCommandCenter>().Squads.SelectMany(s => s.Members))
            {
                member.enabled = false;
                member.GetComponent<NavMeshAgent>().enabled = false;
            }
            var wall = world.Structures.First(p => p.Placement.Definition.Kind == BuildPieceKind.Wall).Placement.Definition;
            foreach (int x in new[] { 4, 11, 12 })
                Assert.IsTrue(world.TryPlace(wall, new Vector2Int(x, 8), 0, out _));
            yield return NavigationReady();
            fall = FallSettings.Create();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (fall != null) Object.Destroy(fall);
            Time.captureDeltaTime = previousCapture;
            Cursor.lockState = previousLock; Cursor.visible = previousVisible;
        }

        T Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
        Vector3 Ground(int x, float z) => world.transform.TransformPoint(new Vector3((x + 0.5f) * world.Grid.CellSize, 0f, z * world.Grid.CellSize));

        IEnumerator NavigationReady()
        {
            for (int frame = 0; frame < 600; frame++)
            {
                yield return null;
                if (!world.IsRebuilding && !world.RebuildPending) yield break;
            }
            Assert.Fail("Navigation rebuild did not finish.");
        }

        [UnityTest]
        public IEnumerator LadderUnit_SkipsLadderWhoseBaseIsCutOff_AndUsesReachableOne()
        {
            SiegeLadder[] usable = ladders.Where(l => l.HasWall).ToArray();
            Assert.GreaterOrEqual(usable.Length, 2, "Need two usable ladders.");
            SiegeLadder blocked = usable[0];
            // 사다리 밑 주변을 길찾기 메시에서 파내 걸어서 닿지 않게 한다. 사용 반경보다 넓게 판다.
            float half = blocked.Settings.LadderUseRadius + 1.5f;
            var block = new GameObject("LadderBaseBlock");
            SceneManager.MoveGameObjectToScene(block, scene);
            block.transform.position = blocked.BasePoint;
            var obstacle = block.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(half * 2f, 3f, half * 2f);
            obstacle.carving = true;
            for (int frame = 0; frame < 10; frame++) yield return null;
            Assert.IsFalse(NavMesh.SamplePosition(blocked.BasePoint, out _, blocked.Settings.LadderUseRadius, NavMesh.AllAreas),
                "Carving did not cut off the ladder base.");

            var enemy = director.Enemies.OfType<SiegeAttackerBrain>()
                .First(e => e.Role == SiegeAttackerRole.Ladder && !e.gameObject.activeSelf);
            Vector3 start = blocked.BasePoint + blocked.OutwardWorld * (half + 4f);
            Assert.Less(Vector3.Distance(start, blocked.BasePoint), Vector3.Distance(start, usable[1].BasePoint),
                "The blocked ladder must be the nearest one.");
            enemy.transform.position = start;
            enemy.gameObject.SetActive(true);
            Assert.IsTrue(enemy.GetComponent<NavMeshAgent>().Warp(start));

            for (int frame = 0; frame < 90 && !usable.Skip(1).Any(l => l.Crowd > 0); frame++) yield return null;
            Assert.AreEqual(0, blocked.Crowd, "The enemy joined a ladder it cannot walk to.");
            Assert.IsTrue(usable.Skip(1).Any(l => l.Crowd > 0), "The enemy did not pick another reachable ladder.");
        }

        [UnityTest]
        public IEnumerator TwoWallBreakers_TakeSeparateStandPoints_AndBothStrike()
        {
            Vector3 at = Ground(2, 11f);
            WallBreakerAttack first = SpawnBreaker(at);
            WallBreakerAttack second = SpawnBreaker(at + Vector3.right * 0.6f);
            for (int frame = 0; frame < 60 && (first.Target == null || second.Target == null); frame++) yield return null;
            Assert.IsNotNull(first.Target);
            Assert.AreEqual(first.Target, second.Target, "Both breakers should pick the same nearest wall.");
            Vector3 gap = first.ApproachPoint - second.ApproachPoint;
            gap.y = 0f;
            Assert.GreaterOrEqual(gap.magnitude, 1f, "Breakers chose overlapping stand points.");

            PlacedStructure target = first.Target;
            var firstAi = first.GetComponent<UnitCombatAI>();
            var secondAi = second.GetComponent<UnitCombatAI>();
            for (int frame = 0; frame < 600 && !(firstAi.Directive == UnitDirective.Hold && secondAi.Directive == UnitDirective.Hold); frame++)
                yield return null;
            Assert.AreEqual(UnitDirective.Hold, firstAi.Directive, "First breaker never reached its stand point.");
            Assert.AreEqual(UnitDirective.Hold, secondAi.Directive, "Second breaker never reached its stand point.");
            Assert.AreEqual(target, first.Target);
            Assert.AreEqual(target, second.Target);
            Assert.Less(target.Health.Current, target.Health.Maximum);
        }

        WallBreakerAttack SpawnBreaker(Vector3 at)
        {
            GameObject prefab = director.Settings.EnemyGroups.Select(g => g.unitPrefab)
                .First(p => p != null && p.GetComponent<SiegeAttacker>()?.Role == SiegeAttackerRole.WallBreaker);
            GameObject unit = Object.Instantiate(prefab, at, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(unit, scene);
            unit.GetComponent<UnitFallController>().Configure(world, fall);
            unit.GetComponent<SiegeAttackerBrain>().Bind(world, new SiegeLadder[0], Vector2Int.up, director.Settings);
            unit.SetActive(true);
            Assert.IsTrue(unit.GetComponent<NavMeshAgent>().Warp(at));
            return unit.GetComponent<WallBreakerAttack>();
        }
    }
}
