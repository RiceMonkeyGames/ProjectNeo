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
    /// <summary>게임 씬에서 층 쌓기를 확인한다. 땅 계단과 자동 계단으로 이어지는 2·3층 길, 미리보기 높이, 아래가 부서질 때 내려앉기.</summary>
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

        // 기본 성(GameFlowSetup.DefaultSeeds): 7.5m 칸, 성벽 고리 x 10~22, z 11~23, 성문 (16, 23), 사다리 줄 x 12·14·18·20.
        static readonly Vector2Int NorthWestCorner = new Vector2Int(10, 23);

        PlacedStructure Lower(Vector2Int cell) => world.Structures.Single(piece => piece.Placement.Cell == cell && piece.Placement.Level == 0);

        static readonly int[] StairRotations = { 0, 1, 2, 3 };

        // 성 밖 북서쪽 들판의 빈 칸(성벽 고리 x 10~22, z 11~23 바깥, 사다리 줄과 떨어진 곳).
        static readonly Vector2Int OpenFieldCell = new Vector2Int(5, 31);

        [UnityTest]
        public IEnumerator OneCellGroundStairs_PlayerAndUnitAscendAndDescend(
            [ValueSource(nameof(StairRotations))] int rotation)
        {
            Assert.AreEqual(Vector2Int.one, stairs.Footprint);
            Assert.AreEqual(wall.Height, stairs.Height, "계단 한 단이 성벽 한 층(5m)을 오른다.");
            var cell = OpenFieldCell;
            Vector2Int direction = BuildLayout.Landings(Vector2Int.one, cell, rotation).Single() - cell;
            Assert.IsTrue(world.TryPlace(stairs, cell, rotation, out var groundStair));
            Assert.IsTrue(world.TryPlace(wall, cell + direction, 0, out var upperWall));
            Assert.IsTrue(world.Layout.HasLanding(groundStair.Placement));
            Assert.AreEqual(PlacementIssue.Occupied, world.CheckPlacement(wall, cell, 0), "계단 위에는 쌓지 않는다.");
            Assert.AreEqual(PlacementIssue.StairsGroundOnly, world.CheckPlacement(stairs, cell + direction, rotation),
                "Game 격자에서 계단은 땅 위에만 놓는다(SIEGE-27).");
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
            var cell = NorthWestCorner;
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
        public IEnumerator 성문_위_성벽은_성문을_부숴도_아치와_함께_유지되고_아래_통로가_열린다()
        {
            var player = Object.FindAnyObjectByType<ProjectNeo.Characters.PlayerInputController>();
            Assert.AreEqual(0f, world.LocalPoint(player.transform.position).y, 0.1f, "플레이어는 성문 위 대신 안뜰에서 시작한다.");
            PlacedStructure gate = world.Structures.Single(piece => piece.Placement.Definition.Kind == BuildPieceKind.Gate);
            Vector2Int cell = gate.Placement.Cell;
            // 기본 성의 성문은 고정 성벽 한 층을 얹은 2층 문루다(SIEGE-18).
            PlacedStructure upper = world.Structures.Single(piece => piece.Placement.Cell == cell && piece.Placement.Level == 1);
            Assert.AreEqual(BuildPieceKind.Wall, upper.Placement.Definition.Kind);
            Assert.IsTrue(upper.Placement.IsFixed);
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
        public IEnumerator 성벽을_2층과_3층으로_올리면_안뜰에서_자동_계단으로_맨_위까지_길이_이어진다()
        {
            // 서쪽 성벽 가운데 칸을 올린다. 땅 계단은 북쪽 성벽 (13,22)에만 있으므로, 1층 통로를 따라 와서
            // 1↔2층은 자동 바깥 계단으로, 3층은 계단실로 오른다(SIEGE-27).
            var cell = new Vector2Int(10, 17);
            var controller = Object.FindAnyObjectByType<PlacementController>(FindObjectsInactive.Include);
            controller.Select(wall);
            controller.UpdatePreview(world.WorldCenter(wall, cell, 0));
            Assert.AreEqual(PlacementIssue.None, controller.Issue);
            Assert.AreEqual(1, controller.PreviewLevel);
            Assert.AreEqual(wall.Height, controller.PreviewElevation, 0.001f);
            Assert.IsTrue(controller.Confirm());
            Assert.IsTrue(GameSession.Flow.Run.State.TryWait(), "놓은 조각을 완성해 위에 쌓을 수 있게 한다(SIEGE-08).");
            PlacedStructure second = world.Structures.Single(piece => piece.Placement.Cell == cell && piece.Placement.Level == 1);
            Assert.AreEqual(Lower(cell).transform.position.y + wall.Height, second.transform.position.y, 0.001f);
            yield return WaitForNavigation();

            Vector3 start = Object.FindAnyObjectByType<SquadCommandCenter>().Squads[0].Members[0].transform.position;
            Assert.IsTrue(NavMesh.SamplePosition(start, out NavMeshHit from, 1f, NavMesh.AllAreas));
            AssertReachable(from.position, second, "2층");

            Assert.IsTrue(world.TryPlace(wall, cell, 0, out PlacedStructure third));
            Assert.AreEqual(2, third.Placement.Level);
            yield return WaitForNavigation();
            AssertReachable(from.position, third, "3층");

            // 성벽은 3층(15m)까지만 쌓인다(Q-59).
            Assert.AreEqual(PlacementIssue.StoreyCap, world.CheckPlacement(wall, cell, 0));
        }

        void AssertReachable(Vector3 from, PlacedStructure piece, string label)
        {
            Vector3 floor = piece.transform.position + Vector3.up * piece.Placement.Definition.Height;
            Assert.IsTrue(NavMesh.SamplePosition(floor, out NavMeshHit top, 1f, NavMesh.AllAreas), label + " 윗면이 걸을 수 있어야 한다.");
            Assert.AreEqual(floor.y, top.position.y, 0.5f, label);
            var path = new NavMeshPath();
            NavMesh.CalculatePath(from, top.position, NavMesh.AllAreas, path);
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, "안뜰에서 땅 계단과 자동 계단을 이어 " + label + "에 닿아야 한다.");
        }

        [UnityTest]
        public IEnumerator 아래_보루가_부서지면_위_보루와_그_위의_무기가_함께_무너진다()
        {
            // 2층 성벽 앞에 2층 보루를 쌓는다(SIEGE-11: 쌓은 층도 그 층의 성벽과 맞닿아야 하고, 무기는 보루에만 놓는다).
            // 사다리 줄(x 12·14·18·20)과 성문을 피한 북쪽 성벽 칸.
            var wallCell = new Vector2Int(11, 23);
            Lower(wallCell);
            Assert.IsTrue(world.TryPlace(wall, wallCell, 0, out PlacedStructure upperWall), "2층 성벽: " + world.CheckPlacement(wall, wallCell, 0));
            BuildPieceDefinition bastion = BastionTestSupport.Definition(SceneManager.GetActiveScene());
            Vector2Int cell = wallCell + Vector2Int.up;
            Assert.AreEqual(PlacementIssue.None, world.CheckPlacement(bastion, cell, 0), "1층 보루");
            Assert.IsTrue(world.TryPlace(bastion, cell, 0, out PlacedStructure lower));
            Assert.AreEqual(PlacementIssue.None, world.CheckPlacement(bastion, cell, 0), "2층 보루");
            Assert.IsTrue(world.TryPlace(bastion, cell, 0, out PlacedStructure upper));
            Assert.AreEqual(1, upper.Placement.Level);
            SiegeWeaponDefinition cannon = Object.FindAnyObjectByType<ConstructionHud>(FindObjectsInactive.Include).Weapons[0];
            Assert.AreEqual(MountIssue.Occupied, world.CheckMount(cannon, lower, "top"), "덮인 보루에는 무기를 놓을 수 없다.");
            Assert.IsTrue(world.TryMount(cannon, upper, "top", out PlacedSiegeWeapon weapon), "위 보루 무기: " + world.CheckMount(cannon, upper, "top"));
            Assert.AreEqual(PlacementIssue.Occupied, world.CheckPlacement(bastion, cell, 0), "무기가 놓인 보루 위에는 쌓을 수 없다.");
            yield return WaitForNavigation();

            // Game 격자는 골짜기를 막으므로(SIEGE-14) 위층이 한 층 내려앉지 않고 그 칸의 위층이 함께 무너진다.
            lower.ReceiveDamage(lower.Health.Current);
            Assert.IsFalse(world.Structures.Contains(upper), "위 보루도 함께 무너진다.");
            Assert.IsFalse(world.Weapons.Contains(weapon), "위에 놓인 무기도 함께 없어진다.");
            Assert.IsNull(world.Layout.Top(cell));
            Assert.AreEqual(0, world.Structures.Count(piece => piece.Placement.Cell == cell));
            Assert.AreEqual(1, upperWall.Placement.Level, "옆 2층 성벽은 그대로다.");
            yield return WaitForNavigation();
            Assert.AreEqual(PlacementIssue.None, world.CheckPlacement(bastion, cell, 0), "무너진 보루 자리에 다시 지을 수 있다.");
        }
    }
}
