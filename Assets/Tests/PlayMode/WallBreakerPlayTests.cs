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
    public class WallBreakerPlayTests
    {
        Scene scene;
        ConstructionWorld world;
        SiegeDirector director;
        BuildPieceDefinition wall;
        FallSettings fall;
        SiegeAttackerBrain brain;
        WallBreakerAttack attack;
        UnitCombatAI ai;
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
            yield return null;
            director.enabled = false;
            Find<PlayerInputController>().enabled = false;
            Find<SquadCommandInput>().enabled = false;
            foreach (var member in Find<SquadCommandCenter>().Squads.SelectMany(s => s.Members))
            {
                member.enabled = false;
                member.GetComponent<NavMeshAgent>().enabled = false;
            }
            wall = world.Structures.First(p => p.Placement.Definition.Kind == BuildPieceKind.Wall).Placement.Definition;
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
        PlacedStructure At(int x) => world.Structures.Single(p => p.Placement.Cell == new Vector2Int(x, 8) && p.Placement.Level == 0);

        IEnumerator NavigationReady()
        {
            for (int frame = 0; frame < 600; frame++)
            {
                yield return null;
                if (!world.IsRebuilding && !world.RebuildPending) yield break;
            }
            Assert.Fail("Navigation rebuild did not finish.");
        }

        void Spawn(Vector3 at)
        {
            GameObject prefab = director.Settings.EnemyGroups.Select(g => g.unitPrefab)
                .First(p => p != null && p.GetComponent<SiegeAttacker>()?.Role == SiegeAttackerRole.WallBreaker);
            Assert.IsFalse(prefab.activeSelf);
            GameObject unit = Object.Instantiate(prefab, at, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(unit, scene);
            unit.GetComponent<UnitFallController>().Configure(world, fall);
            brain = unit.GetComponent<SiegeAttackerBrain>();
            brain.Bind(world, new SiegeLadder[0], Vector2Int.up, director.Settings);
            attack = unit.GetComponent<WallBreakerAttack>();
            ai = unit.GetComponent<UnitCombatAI>();
            unit.SetActive(true);
            Assert.IsTrue(unit.GetComponent<NavMeshAgent>().Warp(at));
        }

        [UnityTest]
        public IEnumerator ClosedWall_BreaksNearestBottom_ThenEntersBreach()
        {
            var nearest = At(2);
            Spawn(Ground(2, 11f));
            for (int frame = 0; frame < 30 && attack.Target == null; frame++) yield return null;
            Assert.AreEqual(nearest, attack.Target);
            Assert.IsFalse(brain.IsAssaulting);
            for (int frame = 0; frame < 1200 && nearest != null; frame++) yield return null;
            Assert.IsTrue(nearest == null, "Breaker did not destroy its wall.");
            for (int frame = 0; frame < 600 && !brain.IsAssaulting; frame++) yield return null;
            Assert.IsTrue(brain.IsAssaulting, "New breach did not switch to normal assault.");
            Assert.IsNull(attack.Target);
            for (int frame = 0; frame < 600 && world.LocalPoint(brain.transform.position).z >= 8f * world.Grid.CellSize; frame++) yield return null;
            Assert.Less(world.LocalPoint(brain.transform.position).z, 8f * world.Grid.CellSize);
        }

        [UnityTest]
        public IEnumerator Gate_IsValidNearestWallTarget_AndTakesDamage()
        {
            var gate = world.Structures.Single(p => p.Placement.Definition.Kind == BuildPieceKind.Gate);
            Vector3 front = gate.transform.position + Vector3.forward * 7f;
            Spawn(front);
            float hp = gate.Health.Current;
            for (int frame = 0; frame < 300 && gate.Health.Current >= hp; frame++) yield return null;
            Assert.AreEqual(gate, attack.Target);
            Assert.Less(gate.Health.Current, hp);
        }

        [UnityTest]
        public IEnumerator StackedWall_AttacksBottom_ThenRetargetsSettledPiece()
        {
            Assert.GreaterOrEqual(world.Grid.MaxLevels, 3, "The actual SiegePrototype must support the demonstrated stacking.");
            var lower = At(2);
            Assert.IsTrue(world.TryPlace(wall, lower.Placement.Cell, 0, out var upper));
            yield return NavigationReady();
            Spawn(Ground(2, 11f));
            for (int frame = 0; frame < 30 && attack.Target == null; frame++) yield return null;
            Assert.AreEqual(lower, attack.Target);
            Assert.AreEqual(1, upper.Placement.Level);
            lower.ReceiveDamage(lower.Health.Maximum);
            yield return NavigationReady();
            for (int frame = 0; frame < 60 && attack.Target != upper; frame++) yield return null;
            Assert.AreEqual(0, upper.Placement.Level);
            Assert.AreEqual(upper, attack.Target, "Dropped upper wall must become the next bottom target.");
            Assert.AreEqual(upper.Health.Maximum, upper.Health.Current, "Do not hit through the lower wall before it collapses.");
        }

        [UnityTest]
        public IEnumerator OpenGap_UsesNormalAssault_WithoutDamagingWalls()
        {
            var gap = At(2);
            gap.ReceiveDamage(gap.Health.Maximum);
            yield return NavigationReady();
            float totalHp = world.Structures.Sum(p => p.Health.Current);
            Spawn(Ground(2, 11f));
            for (int frame = 0; frame < 60 && !brain.IsAssaulting; frame++) yield return null;
            Assert.IsTrue(brain.IsAssaulting);
            Assert.IsNull(attack.Target);
            Assert.AreEqual(totalHp, world.Structures.Sum(p => p.Health.Current));
        }

        [UnityTest]
        public IEnumerator UnreachableWallBase_HoldsAndRetriesAfterReachabilityChanges()
        {
            foreach (var stairs in world.Structures.Where(p => p.Placement.Definition.Kind == BuildPieceKind.Stairs).ToArray())
                stairs.ReceiveDamage(stairs.Health.Maximum);
            yield return NavigationReady();
            var support = At(2);
            Spawn(support.transform.position + Vector3.up * wall.Height);
            for (int frame = 0; frame < 60; frame++) yield return null;
            Assert.IsNull(attack.Target);
            Assert.IsFalse(brain.IsAssaulting);
            Assert.AreEqual(UnitDirective.Hold, ai.Directive);
            Assert.IsTrue(brain.GetComponent<NavMeshAgent>().Warp(Ground(2, 11f)));
            for (int frame = 0; frame < 60 && attack.Target == null; frame++) yield return null;
            Assert.AreEqual(support, attack.Target, "Hold must retry when a ground approach becomes reachable.");
        }

        [UnityTest]
        public IEnumerator Retreat_StopsStructureDamage()
        {
            Spawn(Ground(2, 8f + 1.2f));
            for (int frame = 0; frame < 60 && attack.Target == null; frame++) yield return null;
            Assert.IsNotNull(attack.Target);
            float hp = world.Structures.Sum(p => p.Health.Current);
            brain.Retreat();
            for (int frame = 0; frame < 60; frame++) yield return null;
            Assert.AreEqual(hp, world.Structures.Sum(p => p.Health.Current));
            Assert.IsTrue(brain.IsRetreating || brain.Fled);
        }
    }
}
