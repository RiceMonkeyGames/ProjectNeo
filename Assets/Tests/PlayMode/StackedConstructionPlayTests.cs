using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
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
        float previousCapture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 30f;
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
            Time.captureDeltaTime = previousCapture;
        }

        IEnumerator WaitForNavigation()
        {
            yield return null;
            for (int frame = 0; frame < 600 && (world.IsRebuilding || world.RebuildPending); frame++) yield return null;
            Assert.IsFalse(world.IsRebuilding || world.RebuildPending);
            yield return null;
        }

        PlacedStructure Lower(Vector2Int cell) => world.Structures.Single(piece => piece.Placement.Cell == cell && piece.Placement.Level == 0);

        static readonly int[] StairRotations = { 0, 1, 2, 3 };

        [UnityTest]
        public IEnumerator OneCellStairs_PlayerAndUnitAscendAndDescendTwoTiers(
            [ValueSource(nameof(StairRotations))] int rotation)
        {
            Assert.AreEqual(Vector2Int.one, stairs.Footprint);
            Assert.AreEqual(3f, stairs.Height);
            var cell = new Vector2Int(32, 45);
            Vector2Int direction = BuildLayout.Landings(Vector2Int.one, cell, rotation).Single() - cell;
            Assert.IsTrue(world.TryPlace(stairs, cell, rotation, out var groundStair));
            Assert.IsTrue(world.TryPlace(wall, cell + direction, 0, out _));
            Assert.IsTrue(world.TryPlace(wall, cell + direction * 2, 0, out _));
            Assert.IsTrue(world.TryPlace(stairs, cell + direction * 2, rotation, out var upperStair));
            Assert.IsTrue(world.TryPlace(wall, cell + direction * 3, 0, out _));
            Assert.IsTrue(world.TryPlace(wall, cell + direction * 3, 0, out var upperWall));
            Assert.IsTrue(world.Layout.HasLanding(upperStair.Placement));
            Assert.AreEqual(PlacementIssue.Occupied, world.CheckPlacement(wall, cell, 0));
            yield return WaitForNavigation();

            Vector3 ground = world.WorldCenter(wall, cell - direction, 0);
            Vector3 top = upperWall.transform.position + Vector3.up * wall.Height;
            var unit = Object.FindAnyObjectByType<SquadCommandCenter>().Squads[0].Members[0];
            var agent = unit.GetComponent<NavMeshAgent>();
            Vector3 original = unit.transform.position;
            Assert.IsTrue(agent.Warp(ground));
            foreach (Vector3 target in new[] { top, ground })
            {
                unit.SetDirective(UnitDirective.Move, target);
                int frame = 0;
                while (frame++ < 900 && unit.Directive != UnitDirective.Hold) yield return null;
                Assert.AreEqual(UnitDirective.Hold, unit.Directive);
                Assert.Less(Vector3.Distance(target, unit.transform.position), 0.6f,
                    $"Unit rotation={rotation}, target={target}, position={unit.transform.position}");
            }
            Assert.IsTrue(agent.Warp(original));

            var player = Object.FindAnyObjectByType<ProjectNeo.Characters.PlayerInputController>();
            player.enabled = false;
            var motor = player.GetComponent<CharacterController>();
            var actor = player.GetComponent<ProjectNeo.Combat.CharacterActor>();
            float health = actor.Health.Current;
            motor.enabled = false;
            player.transform.position = ground + Vector3.up * 0.05f;
            motor.enabled = true;
            Physics.SyncTransforms();
            foreach (Vector3 target in new[] { top, ground })
            {
                int frame = 0;
                while (frame++ < 900)
                {
                    Vector3 delta = target - player.transform.position;
                    delta.y = 0f;
                    if (delta.magnitude < 0.15f && Mathf.Abs(target.y - player.transform.position.y) < 0.2f) break;
                    float yaw = Quaternion.LookRotation(delta.normalized).eulerAngles.y;
                    player.Move(delta.magnitude > 0.1f ? Vector2.up : Vector2.zero, yaw, 1f / 30f);
                    yield return null;
                }
                Assert.Less(Vector3.Distance(target, player.transform.position), 0.3f,
                    $"Player rotation={rotation}, target={target}, position={player.transform.position}");
            }
            Assert.AreEqual(health, actor.Health.Current, "Walking stairs must not cause fall damage.");
            groundStair.ReceiveDamage(groundStair.Health.Current);
            yield return WaitForNavigation();
            var disconnected = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(ground, top, NavMesh.AllAreas, disconnected));
            Assert.AreEqual(NavMeshPathStatus.PathPartial, disconnected.status,
                "Removing the only ground stair must leave vertical wall faces impassable.");
        }

        [UnityTest]
        public IEnumerator 적층_성벽은_맨_위부터_철거하고_점유와_길찾기를_갱신한다()
        {
            var cell = new Vector2Int(24, 59);
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
            // 건설 화면에서 놓은 조각은 작업 주문이다(SIEGE-08). 위에 쌓으려면 한 시간 기다려 완성한다.
            Assert.IsTrue(GameSession.Flow.Run.State.TryWait());
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
        public IEnumerator 성문_위에_성벽을_설치하고_성문을_부수면_아치와_위_성벽이_유지되고_아래_통로가_열린다()
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

            // Intact gate blocks ground path through the gate cell.
            Vector3 outside = gate.transform.position + Vector3.forward * 6f;
            Vector3 inside = gate.transform.position - Vector3.forward * 6f;
            var blocked = new NavMeshPath();
            NavMesh.CalculatePath(outside, inside, NavMesh.AllAreas, blocked);
            Assert.AreNotEqual(NavMeshPathStatus.PathComplete, blocked.status, "파괴 전에는 성문 아래로 지상 경로가 열리지 않는다.");

            gate.ReceiveDamage(gate.Health.Current);
            Assert.IsTrue(gate.Health.IsDead);
            Assert.AreEqual(1, upper.Placement.Level, "성문이 부서져도 아치가 유지되어 위층 성벽이 내려앉지 않는다.");
            Assert.AreEqual(groundY + gate.Placement.Definition.Height, upper.transform.position.y, 0.001f);
            Assert.AreSame(gate.Placement, world.Layout.At(cell));
            Assert.IsTrue(gate.transform.Find("Visual/StoneArch").gameObject.activeSelf);
            Assert.IsFalse(gate.transform.Find("Visual/Door").gameObject.activeSelf);

            yield return WaitForNavigation();
            Vector3 top = upper.transform.position + Vector3.up * wall.Height;
            Assert.IsTrue(NavMesh.SamplePosition(top, out NavMeshHit hit, 0.5f, NavMesh.AllAreas));
            Assert.AreEqual(top.y, hit.position.y, 0.5f);

            var open = new NavMeshPath();
            NavMesh.CalculatePath(outside, inside, NavMesh.AllAreas, open);
            Assert.AreEqual(NavMeshPathStatus.PathComplete, open.status, "성문 파괴 후 문 아래 통로로 지상 이동 경로가 열려야 한다.");
        }

        [UnityTest]
        public IEnumerator 이층_성벽과_이단_계단을_놓으면_안뜰에서_이층_위까지_길이_이어진다()
        {
            // 북쪽 성벽의 서쪽 모서리 (24,59)에 2층을 올리고, (25,59) 위에 서쪽으로 오르는 1칸 계단을 놓는다.
            // 땅의 계단은 (27,59) 윗면으로 올라오고, (26,59)의 평평한 성벽을 지나 2단 계단에 닿는다.
            var corner = new Vector2Int(24, 59);
            var controller = Object.FindAnyObjectByType<PlacementController>(FindObjectsInactive.Include);
            controller.Select(wall);
            controller.UpdatePreview(world.WorldCenter(wall, corner, 0));
            Assert.AreEqual(PlacementIssue.None, controller.Issue);
            Assert.AreEqual(1, controller.PreviewLevel);
            Assert.AreEqual(wall.Height, controller.PreviewElevation, 0.001f);
            Assert.IsTrue(controller.Confirm());
            Assert.IsTrue(GameSession.Flow.Run.State.TryWait(), "놓은 조각을 완성해 위에 쌓을 수 있게 한다(SIEGE-08).");
            PlacedStructure upperWall = world.Structures.Single(piece => piece.Placement.Cell == corner && piece.Placement.Level == 1);
            Assert.AreEqual(Lower(corner).transform.position.y + wall.Height, upperWall.transform.position.y, 0.001f);

            Assert.IsTrue(world.TryPlace(stairs, new Vector2Int(25, 59), 3, out PlacedStructure upperStairs));
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
            var cell = new Vector2Int(45, 59);
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
