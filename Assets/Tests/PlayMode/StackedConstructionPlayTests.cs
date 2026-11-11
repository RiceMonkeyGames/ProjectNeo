using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Flow;
using ProjectNeo.Squads;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>게임 씬에서 층 쌓기를 확인한다. 2층 성벽과 2단 계단의 길, 미리보기 높이, 아래가 부서질 때 내려앉기.</summary>
    public class StackedConstructionPlayTests
    {
        static int cleanupCount;
        ConstructionWorld world;
        BuildPieceDefinition wall, stairs;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            world = Object.FindAnyObjectByType<ConstructionWorld>();
            wall = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall).Placement.Definition;
            stairs = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Stairs).Placement.Definition;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene empty = SceneManager.CreateScene("StackedConstructionCleanup" + cleanupCount++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene != empty) yield return SceneManager.UnloadSceneAsync(scene);
            }
            GameSession.Reset();
        }

        IEnumerator WaitForNavigation()
        {
            yield return null;
            for (int frame = 0; frame < 600 && (world.IsRebuilding || world.RebuildPending); frame++) yield return null;
            Assert.IsFalse(world.IsRebuilding || world.RebuildPending);
            yield return null;
        }

        PlacedStructure Lower(Vector2Int cell) => world.Structures.Single(piece => piece.Placement.Cell == cell && piece.Placement.Level == 0);

        [UnityTest]
        public IEnumerator 적층_성벽은_맨_위부터_철거하고_점유와_길찾기를_갱신한다()
        {
            var cell = new Vector2Int(5, 17);
            PlacedStructure lower = Lower(cell);
            Assert.IsTrue(world.TryPlace(wall, cell, 0, out PlacedStructure upper));
            yield return WaitForNavigation();
            Assert.IsFalse(world.TryDismantle(lower), "위층을 공중에 남기는 철거는 거부한다.");
            int removedId = upper.Placement.Id;
            int revision = world.NavigationRevision;
            Assert.IsTrue(world.TryDismantle(upper));
            Assert.IsNull(world.Layout.Find(removedId));
            Assert.AreSame(lower.Placement, world.Layout.Top(cell));
            Assert.IsTrue(world.RebuildPending);
            yield return WaitForNavigation();
            Assert.Greater(world.NavigationRevision, revision);
            Assert.IsTrue(world.TryPlace(wall, cell, 0, out _), "철거한 위층 자리에 다시 지을 수 있다.");
            yield return WaitForNavigation();
        }

        [UnityTest]
        public IEnumerator 플레이어와_여러_병력이_겹쳐도_건설하고_새_성벽_위로_피해없이_옮긴다()
        {
            var player = Object.FindAnyObjectByType<ProjectNeo.Characters.PlayerInputController>();
            var playerActor = player.GetComponent<ProjectNeo.Combat.CharacterActor>();
            var members = Object.FindAnyObjectByType<SquadCommandCenter>().Squads[0].Members.Take(2).ToArray();
            Vector2Int cell = world.Layout.WorldToCell(world.LocalPoint(player.transform.position));
            Vector3 center = world.transform.TransformPoint(world.Layout.Center(cell, Vector2Int.one));
            var motor = player.GetComponent<CharacterController>();
            motor.enabled = false;
            player.transform.position = center;
            motor.enabled = true;
            for (int i = 0; i < members.Length; i++)
                Assert.IsTrue(members[i].GetComponent<NavMeshAgent>().Warp(center + Vector3.right * (i == 0 ? -0.9f : 0.9f)));
            float health = playerActor.Health.Current;
            var unitHealth = members.Select(member => member.Actor.Health.Current).ToArray();
            var controller = Object.FindAnyObjectByType<PlacementController>(FindObjectsInactive.Include);
            controller.Select(wall);
            controller.UpdatePreview(center);
            Assert.AreEqual(PlacementIssue.None, controller.Issue);
            Assert.IsTrue(controller.Confirm());
            Assert.AreEqual(center.y + wall.Height, player.transform.position.y, 0.01f);
            foreach (var member in members) Assert.AreEqual(center.y + wall.Height, member.transform.position.y, 0.01f);
            yield return WaitForNavigation();
            Assert.IsTrue(motor.enabled);
            Assert.AreEqual(health, playerActor.Health.Current);
            for (int i = 0; i < members.Length; i++)
            {
                Assert.IsTrue(members[i].GetComponent<NavMeshAgent>().enabled);
                Assert.IsTrue(members[i].GetComponent<NavMeshAgent>().isOnNavMesh);
                Assert.AreEqual(unitHealth[i], members[i].Actor.Health.Current);
            }
            // 같은 자리의 다음 층 건설도 이미 올라간 유닛을 다시 옮긴다.
            Assert.IsTrue(world.TryPlace(wall, cell, 0, out _));
            Assert.AreEqual(center.y + wall.Height * 2f, player.transform.position.y, 0.01f);
            yield return WaitForNavigation();
            Assert.IsTrue(GameSession.Flow.StartBattle());
            Assert.IsTrue(player.enabled);
            Assert.IsTrue(motor.enabled);
            Assert.IsFalse(controller.enabled, "전투에서는 여전히 건설할 수 없다.");
        }

        [UnityTest]
        public IEnumerator 성문_위에_성벽을_설치하고_성문을_부수면_내려앉아_길이_갱신된다()
        {
            var player = Object.FindAnyObjectByType<ProjectNeo.Characters.PlayerInputController>();
            Assert.AreEqual(0f, world.LocalPoint(player.transform.position).y, 0.1f, "플레이어는 성문 위 대신 안뜰에서 시작한다.");
            PlacedStructure gate = world.Structures.Single(piece => piece.Placement.Definition.Kind == BuildPieceKind.Gate);
            Vector2Int cell = gate.Placement.Cell;
            var controller = Object.FindAnyObjectByType<PlacementController>(FindObjectsInactive.Include);
            controller.Select(wall);
            controller.UpdatePreview(world.transform.TransformPoint(world.Layout.Center(cell, Vector2Int.one)));
            Assert.AreEqual(PlacementIssue.None, controller.Issue);
            Assert.AreEqual(1, controller.PreviewLevel);
            Assert.AreEqual(gate.Placement.Definition.Height, controller.PreviewElevation, 0.001f);
            Assert.IsTrue(controller.Confirm());
            PlacedStructure upper = world.Structures.Single(piece => piece.Placement.Cell == cell && piece.Placement.Level == 1);
            float groundY = gate.transform.position.y;
            Assert.AreEqual(groundY + gate.Placement.Definition.Height, upper.transform.position.y, 0.001f);
            yield return WaitForNavigation();
            gate.ReceiveDamage(gate.Health.Current);
            Assert.AreEqual(0, upper.Placement.Level);
            Assert.AreEqual(groundY, upper.transform.position.y, 0.001f);
            Assert.AreSame(upper.Placement, world.Layout.At(cell));
            yield return WaitForNavigation();
            Vector3 top = upper.transform.position + Vector3.up * wall.Height;
            Assert.IsTrue(NavMesh.SamplePosition(top, out NavMeshHit hit, 0.5f, NavMesh.AllAreas));
            Assert.AreEqual(top.y, hit.position.y, 0.5f);
        }

        [UnityTest]
        public IEnumerator 이층_성벽과_이단_계단을_놓으면_안뜰에서_이층_위까지_길이_이어진다()
        {
            // 북쪽 성벽의 서쪽 모서리 (5,17)에 2층을 올리고, (6,17)~(7,17) 위에 서쪽으로 오르는 계단을 놓는다.
            // 땅의 계단은 (8,17) 윗면으로 올라오므로 그 옆에서 2단 계단이 시작된다.
            var corner = new Vector2Int(5, 17);
            var controller = Object.FindAnyObjectByType<PlacementController>(FindObjectsInactive.Include);
            controller.Select(wall);
            controller.UpdatePreview(world.WorldCenter(wall, corner, 0));
            Assert.AreEqual(PlacementIssue.None, controller.Issue);
            Assert.AreEqual(1, controller.PreviewLevel);
            Assert.AreEqual(wall.Height, controller.PreviewElevation, 0.001f);
            Assert.IsTrue(controller.Confirm());
            PlacedStructure upperWall = world.Structures.Single(piece => piece.Placement.Cell == corner && piece.Placement.Level == 1);
            Assert.AreEqual(Lower(corner).transform.position.y + wall.Height, upperWall.transform.position.y, 0.001f);

            Assert.IsTrue(world.TryPlace(stairs, new Vector2Int(6, 17), 3, out PlacedStructure upperStairs));
            Assert.AreEqual(1, upperStairs.Placement.Level);
            Assert.IsTrue(world.Layout.HasLanding(upperStairs.Placement));
            yield return WaitForNavigation();

            Vector3 start = Object.FindAnyObjectByType<SquadCommandCenter>().Squads[0].Members[0].transform.position;
            Vector3 secondFloor = upperWall.transform.position + Vector3.up * wall.Height;
            Assert.IsTrue(NavMesh.SamplePosition(start, out NavMeshHit from, 1f, NavMesh.AllAreas));
            Assert.IsTrue(NavMesh.SamplePosition(secondFloor, out NavMeshHit top, 0.5f, NavMesh.AllAreas), "2층 윗면이 걸을 수 있어야 한다.");
            Assert.AreEqual(secondFloor.y, top.position.y, 0.5f);
            var path = new NavMeshPath();
            NavMesh.CalculatePath(from.position, top.position, NavMesh.AllAreas, path);
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, "땅 계단과 2단 계단을 이어 2층에 닿아야 한다.");

            // 최대 3층까지만 쌓인다.
            Assert.IsTrue(world.TryPlace(wall, corner, 0, out _));
            Assert.AreEqual(PlacementIssue.Occupied, world.CheckPlacement(wall, corner, 0));
        }

        [UnityTest]
        public IEnumerator 아래_성벽이_부서지면_위_성벽과_그_위의_무기가_한_층_내려앉는다()
        {
            var cell = new Vector2Int(14, 17);
            PlacedStructure lower = Lower(cell);
            Assert.IsTrue(world.TryPlace(wall, cell, 0, out PlacedStructure upper));
            SiegeWeaponDefinition cannon = Object.FindAnyObjectByType<ConstructionHud>(FindObjectsInactive.Include).Weapons[0];
            Assert.AreEqual(MountIssue.Occupied, world.CheckMount(cannon, lower, "top"), "덮인 성벽에는 무기를 놓을 수 없다.");
            Assert.IsTrue(world.TryMount(cannon, upper, "top", out PlacedSiegeWeapon weapon));
            Assert.AreEqual(PlacementIssue.Occupied, world.CheckPlacement(wall, cell, 0), "무기가 놓인 성벽 위에는 쌓을 수 없다.");
            float groundY = lower.transform.position.y;
            float weaponHeight = weapon.transform.position.y - upper.transform.position.y;
            upper.ReceiveDamage(30f);
            float upperHealth = upper.Health.Current;
            yield return WaitForNavigation();

            lower.ReceiveDamage(lower.Health.Current);
            Assert.AreEqual(0, upper.Placement.Level);
            Assert.AreEqual(groundY, upper.transform.position.y, 0.001f);
            Assert.AreSame(upper.Placement, world.Layout.At(cell));
            Assert.AreEqual(upperHealth, upper.Health.Current, "내려앉아도 체력은 그대로다.");
            Assert.IsTrue(world.Weapons.Contains(weapon), "위에 놓인 무기는 조각과 함께 내려온다.");
            Assert.AreEqual(groundY + weaponHeight, weapon.transform.position.y, 0.001f);
            Assert.AreEqual(1, world.Structures.Count(piece => piece.Placement.Cell == cell));
            yield return WaitForNavigation();

            Vector3 newTop = upper.transform.position + Vector3.up * wall.Height;
            Assert.IsTrue(NavMesh.SamplePosition(newTop, out NavMeshHit hit, 0.5f, NavMesh.AllAreas));
            Assert.AreEqual(newTop.y, hit.position.y, 0.5f, "내려앉은 성벽 윗면이 다시 길이 된다.");
        }
    }
}
