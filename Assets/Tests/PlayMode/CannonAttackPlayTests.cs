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
    /// <summary>성벽 위에 올라온 사다리병이 아군이 없을 때 대포를 부순다(SIEGE-02 6단계).</summary>
    public class CannonAttackPlayTests
    {
        Scene scene;
        ConstructionWorld world;
        SiegeDirector director;
        FallSettings fall;
        GameObject template;
        SiegeWeaponDefinition cannonDefinition;
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
            Find<SquadCommandInput>().enabled = false;
            // 걸어서 닿는 아군이 없도록 플레이어와 아군 부대를 모두 끈다.
            Find<PlayerInputController>().gameObject.SetActive(false);
            foreach (UnitCombatAI member in Find<SquadCommandCenter>().Squads.SelectMany(s => s.Members))
                member.gameObject.SetActive(false);
            fall = FallSettings.Create();
            template = new GameObject("TestCannonTemplate");
            template.SetActive(false);
            SceneManager.MoveGameObjectToScene(template, scene);
            template.AddComponent<PlacedSiegeWeapon>();
            cannonDefinition = SiegeWeaponDefinition.Create("test_cannon", "test_cannon", new Vector3(1.2f, 1.1f, 1.8f),
                template, 150f);
            yield return NavigationReady();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (fall != null) Object.Destroy(fall);
            if (cannonDefinition != null) Object.Destroy(cannonDefinition);
            Time.captureDeltaTime = previousCapture;
            Cursor.lockState = previousLock; Cursor.visible = previousVisible;
        }

        T Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();

        IEnumerator NavigationReady()
        {
            for (int frame = 0; frame < 600; frame++)
            {
                yield return null;
                if (!world.IsRebuilding && !world.RebuildPending) yield break;
            }
            Assert.Fail("Navigation rebuild did not finish.");
        }

        // 바닥층 성벽 셋이 나란히 이어진 줄을 찾는다. 가운데를 건너 양 끝에 적과 대포를 둔다.
        void FindWallRun(out PlacedStructure start, out PlacedStructure end)
        {
            var walls = world.Structures.Where(p => p.Placement.Definition.Kind == BuildPieceKind.Wall
                && p.Placement.Level == 0 && !world.Layout.IsCovered(p.Placement))
                .ToDictionary(p => p.Placement.Cell);
            foreach (var pair in walls)
            {
                Vector2Int cell = pair.Key;
                foreach (Vector2Int step in new[] { Vector2Int.right, Vector2Int.up })
                    if (walls.ContainsKey(cell + step) && walls.TryGetValue(cell + step * 2, out PlacedStructure far))
                    {
                        start = pair.Value; end = far;
                        return;
                    }
            }
            Assert.Fail("이어진 성벽 줄이 없다.");
            start = end = null;
        }

        Vector3 Top(PlacedStructure wall) =>
            wall.transform.position + Vector3.up * wall.Placement.Definition.Height;

        SiegeAttackerBrain SpawnClimber(Vector3 at)
        {
            GameObject prefab = director.Settings.EnemyGroups.Select(g => g.unitPrefab)
                .First(p => p != null && p.GetComponent<SiegeAttackerBrain>().Role == SiegeAttackerRole.Ladder);
            Assert.IsFalse(prefab.activeSelf);
            GameObject unit = Object.Instantiate(prefab, at, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(unit, scene);
            unit.GetComponent<UnitFallController>().Configure(world, fall);
            var brain = unit.GetComponent<SiegeAttackerBrain>();
            brain.Bind(world, new SiegeLadder[0], Vector2Int.up, director.Settings);
            unit.SetActive(true);
            Assert.IsTrue(NavMesh.SamplePosition(at, out NavMeshHit hit, 1.5f, NavMesh.AllAreas), "성벽 위에 길이 있어야 한다.");
            Assert.IsTrue(unit.GetComponent<NavMeshAgent>().Warp(hit.position));
            return brain;
        }

        [UnityTest]
        public IEnumerator WallTopClimber_WithoutReachableAllies_DestroysNearbyCannon()
        {
            FindWallRun(out PlacedStructure start, out PlacedStructure end);
            Assert.IsTrue(world.TryMount(cannonDefinition, end, "top", out PlacedSiegeWeapon weapon));
            weapon.gameObject.SetActive(true);
            SiegeAttackerBrain climber = SpawnClimber(Top(start));
            int removed = 0;
            SiegeWeaponRemoval reason = SiegeWeaponRemoval.Dismantled;
            weapon.Removed += (_, why) => { removed++; reason = why; };

            for (int frame = 0; frame < 90 && climber.CannonTarget != weapon; frame++) yield return null;
            Assert.AreSame(weapon, climber.CannonTarget, "닿는 아군이 없으면 걸어서 닿는 대포를 노린다.");
            Assert.IsFalse(climber.IsAssaulting);
            for (int frame = 0; frame < 300 && weapon.Health.Current >= weapon.Health.Maximum; frame++) yield return null;
            Assert.Less(weapon.Health.Current, weapon.Health.Maximum, "대포 앞까지 가서 친다.");
            for (int frame = 0; frame < 2400 && removed == 0; frame++) yield return null;
            Assert.AreEqual(1, removed, "대포가 부서져야 한다.");
            Assert.AreEqual(SiegeWeaponRemoval.Destroyed, reason);
            Assert.IsTrue(world.Structures.Contains(end), "받침 성벽은 남는다.");
            for (int frame = 0; frame < 30 && climber.CannonTarget != null; frame++) yield return null;
            Assert.IsNull(climber.CannonTarget, "부순 뒤에는 다른 할 일을 고른다.");
        }

        [UnityTest]
        public IEnumerator WallTopClimber_PrefersReachableAlly_OverCannon()
        {
            FindWallRun(out PlacedStructure start, out PlacedStructure end);
            Assert.IsTrue(world.TryMount(cannonDefinition, end, "top", out PlacedSiegeWeapon weapon));
            weapon.gameObject.SetActive(true);
            UnitCombatAI ally = Find<SquadCommandCenter>().Squads.SelectMany(s => s.Members).First();
            ally.GetComponent<NavMeshAgent>().enabled = false;
            ally.enabled = false;
            ally.transform.position = Top(end) + (Top(end) - Top(start)).normalized * 1.5f;
            ally.gameObject.SetActive(true);
            Physics.SyncTransforms();
            SiegeAttackerBrain climber = SpawnClimber(Top(start));

            for (int frame = 0; frame < 90 && !climber.IsAssaulting; frame++) yield return null;
            Assert.IsTrue(climber.IsAssaulting, "걸어서 닿는 아군이 있으면 아군과 싸운다.");
            Assert.IsNull(climber.CannonTarget);
            Assert.AreEqual(weapon.Health.Maximum, weapon.Health.Current);
        }
    }
}
