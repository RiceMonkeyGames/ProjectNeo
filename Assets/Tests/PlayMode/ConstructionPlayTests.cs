using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    public class ConstructionPlayTests
    {
        Scene scene;
        ConstructionWorld world;
        PlacementController controller;
        ConstructionSandbox sandbox;
        BuildPieceDefinition wall;
        BuildPieceDefinition stairs;
        float previousCapture;
        CursorLockMode previousLock;
        bool previousVisible;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousCapture = Time.captureDeltaTime;
            previousLock = Cursor.lockState; previousVisible = Cursor.visible;
            Time.captureDeltaTime = 1f / 30f;
            yield return SceneManager.LoadSceneAsync("ConstructionPrototype", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            var roots = scene.GetRootGameObjects();
            world = roots.Select(root => root.GetComponent<ConstructionWorld>()).Single(value => value != null);
            controller = roots.Select(root => root.GetComponent<PlacementController>()).Single(value => value != null);
            sandbox = roots.Select(root => root.GetComponent<ConstructionSandbox>()).Single(value => value != null);
            controller.enabled = false;
            wall = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall).Placement.Definition;
            stairs = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Stairs).Placement.Definition;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.UnloadSceneAsync(scene);
            Time.captureDeltaTime = previousCapture;
            Cursor.lockState = previousLock; Cursor.visible = previousVisible;
        }

        static IEnumerator WaitForNavigation(ConstructionWorld world)
        {
            int frames = 0;
            do { yield return null; frames++; }
            while ((world.IsRebuilding || world.RebuildPending) && frames < 600);
            Assert.IsFalse(world.IsRebuilding || world.RebuildPending, "길찾기 갱신이 끝나지 않았다.");
        }

        static IEnumerator WaitForCamera(ConstructionCameraController orbit)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (orbit.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(orbit.IsTransitioning, "실제 시간 기준 카메라 전환이 끝나지 않았다.");
        }

        [UnityTest]
        public IEnumerator Camera_OrbitsInQuarterTurns_WithoutChangingLayoutOrPickingCoordinates()
        {
            var orbit = scene.GetRootGameObjects().Select(root => root.GetComponent<ConstructionCameraController>())
                .Single(value => value != null);
            var camera = orbit.GetComponent<Camera>();
            Vector3 origin = world.transform.position;
            var cell = new Vector2Int(10, 4);
            Vector3 point = world.WorldCenter(wall, cell, 0);
            Vector3[] bodyPositions = world.Structures.Select(piece => piece.transform.position).ToArray();
            int revision = world.NavigationRevision;
            float initialYaw = orbit.Yaw;
            controller.Select(wall);
            for (int i = 0; i < 4; i++)
            {
                orbit.RotateRight();
                yield return WaitForCamera(orbit);
                Assert.That(Mathf.DeltaAngle(initialYaw - 90f * (i + 1), orbit.Yaw), Is.EqualTo(0f).Within(0.01f));
                Assert.That(Mathf.DeltaAngle(orbit.Yaw, orbit.RenderedYaw), Is.EqualTo(0f).Within(0.01f));
                Assert.IsTrue(camera.orthographic);
                Assert.That(orbit.transform.eulerAngles.x, Is.EqualTo(30f).Within(0.01f));
                var plane = new Plane(world.transform.up, origin);
                Ray ray = camera.ScreenPointToRay(camera.WorldToScreenPoint(point));
                Assert.IsTrue(plane.Raycast(ray, out float distance));
                controller.UpdatePreview(ray.GetPoint(distance));
                Assert.AreEqual(cell, controller.PreviewCell);
                Assert.AreEqual(origin, world.transform.position);
                CollectionAssert.AreEqual(bodyPositions, world.Structures.Select(piece => piece.transform.position).ToArray());
            }
            orbit.RotateLeft();
            Assert.That(Mathf.DeltaAngle(initialYaw + 90f, orbit.Yaw), Is.EqualTo(0f).Within(0.01f));
            Assert.AreEqual(revision, world.NavigationRevision);
        }

        [UnityTest]
        public IEnumerator Camera_TransitionsSmoothly_AndRetargetsWithoutJumping()
        {
            var orbit = scene.GetRootGameObjects().Select(root => root.GetComponent<ConstructionCameraController>())
                .Single(value => value != null);
            float initial = orbit.RenderedYaw;
            Vector3 originalPosition = orbit.transform.position;
            orbit.RotateRight();
            Assert.AreEqual(originalPosition, orbit.transform.position, "입력 즉시 카메라가 점프하면 안 된다.");
            Assert.IsTrue(orbit.IsTransitioning);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Mathf.Abs(Mathf.DeltaAngle(initial, orbit.RenderedYaw)) < 5f
                && Time.realtimeSinceStartup < deadline) yield return null;
            float moved = Mathf.DeltaAngle(initial, orbit.RenderedYaw);
            Assert.That(moved, Is.LessThan(0f).And.GreaterThan(-90f));
            Vector3 beforeRetarget = orbit.transform.position;
            orbit.RotateRight();
            Assert.AreEqual(beforeRetarget, orbit.transform.position);
            orbit.RotateLeft();
            Assert.AreEqual(beforeRetarget, orbit.transform.position);
            yield return WaitForCamera(orbit);
            Assert.That(Mathf.DeltaAngle(initial - 90f, orbit.RenderedYaw), Is.EqualTo(0f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Preview_DoesNotOccupyCellsOrChangeNavigation_AndCancelCleansUp()
        {
            int count = world.Layout.Placements.Count;
            int revision = world.NavigationRevision;
            controller.Select(wall);
            controller.UpdatePreview(world.WorldCenter(wall, new Vector2Int(1, 1), 0));
            Assert.IsTrue(controller.IsPreviewVisible);
            Assert.AreEqual(PlacementIssue.None, controller.Issue);
            Assert.AreEqual(count, world.Layout.Placements.Count);
            Assert.IsNull(world.Layout.At(new Vector2Int(1, 1)));
            var ghost = world.transform.Find("PlacementPreview");
            Assert.IsTrue(ghost.GetComponentsInChildren<Collider>().All(collider => !collider.enabled));
            Assert.Less(ghost.GetComponentInChildren<Renderer>().sharedMaterial.GetColor("_BaseColor").a, 1f);
            controller.Cancel();
            yield return null;
            Assert.IsFalse(controller.IsPreviewVisible);
            Assert.AreEqual(revision, world.NavigationRevision);
        }

        [UnityTest]
        public IEnumerator Confirm_RotatesFootprint_RejectsOverlap_AndUpdatesNavigation()
        {
            int revision = world.NavigationRevision;
            controller.Select(stairs);
            controller.Rotate();
            Vector2Int cell = new Vector2Int(1, 1);
            controller.UpdatePreview(world.transform.TransformPoint(new Vector3(1.2f, 0f, 1.2f) * world.Grid.CellSize));
            Assert.IsTrue(controller.StairNeedsConnection);
            Assert.IsTrue(controller.Confirm());
            Assert.AreEqual(new Vector2Int(2, 1), world.Layout.At(cell).Size);
            Assert.AreSame(world.Layout.At(cell), world.Layout.At(cell + Vector2Int.right));
            Assert.IsFalse(controller.Confirm());
            Assert.AreEqual(PlacementIssue.Occupied, controller.Issue);
            yield return WaitForNavigation(world);
            Assert.Greater(world.NavigationRevision, revision);
        }

        [UnityTest]
        public IEnumerator UnitOverlap_IsRejected_AndGateCannotBeCovered()
        {
            Vector2Int unitCell = world.Layout.WorldToCell(world.LocalPoint(sandbox.Unit.transform.position));
            Assert.AreEqual(PlacementIssue.UnitInTheWay, world.CheckPlacement(wall, unitCell, 0));
            Assert.IsFalse(world.TryPlace(wall, unitCell, 0, out _));
            var gate = world.Structures.Single(piece => piece.Placement.Definition.Kind == BuildPieceKind.Gate);
            Vector2Int gateCell = gate.Placement.Cell;
            Assert.IsFalse(world.Layout.Remove(gate.Placement.Id));
            gate.ReceiveDamage(gate.Health.Current);
            Assert.AreEqual(PlacementIssue.ReservedGate, world.CheckPlacement(wall, gateCell, 0));
            yield return WaitForNavigation(world);
        }

        [UnityTest]
        public IEnumerator Unit_WalksUpManualStairs_ThenFallsTakesDamageAndResumesOnGround()
        {
            sandbox.WalkToWall();
            int frames = 0;
            while (sandbox.Unit.transform.position.y < wall.Height - 0.1f && frames++ < 900) yield return null;
            Assert.GreaterOrEqual(sandbox.Unit.transform.position.y, wall.Height - 0.1f, sandbox.Message);
            var agent = sandbox.Unit.GetComponent<NavMeshAgent>();
            agent.isStopped = true;
            // 실제로 유닛이 서 있는 조각을 시험 버튼이 찾아 파괴한다.
            float before = sandbox.Unit.Health.Current;
            sandbox.CollapseUnderUnit();
            var fall = sandbox.Unit.GetComponent<UnitFallController>();
            Assert.IsTrue(fall.IsFalling, sandbox.Message);
            Assert.IsFalse(agent.enabled);
            frames = 0;
            while (fall.IsFalling && frames++ < 900) yield return null;
            Assert.IsFalse(fall.IsFalling);
            Assert.That(sandbox.Unit.transform.position.y, Is.LessThan(0.3f));
            Assert.Greater(fall.LastFallDamage, 0f);
            Assert.That(before - sandbox.Unit.Health.Current, Is.EqualTo(fall.LastFallDamage).Within(0.01f));
            Assert.IsTrue(agent.enabled && agent.isOnNavMesh);
        }

        [UnityTest]
        public IEnumerator CharacterControllerBody_FallsAndReenablesItsColliderAfterLanding()
        {
            var support = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall);
            var root = new GameObject("FallingPlayerBody");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            var actor = root.AddComponent<CharacterActor>();
            actor.Configure(sandbox.Unit.Character.Definition, sandbox.Unit.Profile);
            var motor = root.AddComponent<CharacterController>();
            motor.height = 2f; motor.radius = 0.4f; motor.center = Vector3.up;
            var settings = FallSettings.Create();
            var fall = root.AddComponent<UnitFallController>();
            fall.Configure(world, settings);
            root.transform.position = support.transform.position + Vector3.up * (wall.Height + 0.08f);
            root.SetActive(true);
            try
            {
                Physics.SyncTransforms();
                support.ReceiveDamage(support.Health.Current);
                Assert.IsTrue(fall.IsFalling);
                Assert.IsFalse(motor.enabled);
                int frames = 0;
                while (fall.IsFalling && frames++ < 600) yield return null;
                Assert.IsFalse(fall.IsFalling);
                Assert.IsTrue(motor.enabled);
                Assert.Less(root.transform.position.y, 0.3f);
                Assert.Less(actor.Health.Current, actor.Health.Maximum);
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(settings);
            }
        }

        [UnityTest]
        public IEnumerator ConsecutiveChanges_AreCoalesced_AndRemovedWallTopDisappears()
        {
            var target = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall);
            Vector3 top = target.transform.position + Vector3.up * target.Placement.Definition.Height;
            int revision = world.NavigationRevision;
            target.ReceiveDamage(target.Health.Current);
            Assert.IsTrue(world.TryPlace(wall, new Vector2Int(1, 1), 0, out _));
            yield return null;
            Assert.IsTrue(world.TryPlace(wall, new Vector2Int(2, 1), 0, out _));
            yield return WaitForNavigation(world);
            Assert.Greater(world.NavigationRevision, revision);
            Assert.IsFalse(NavMesh.SamplePosition(top, out var hit, 0.25f, NavMesh.AllAreas)
                && Mathf.Abs(hit.position.y - top.y) < 0.25f);
        }
    }
}
