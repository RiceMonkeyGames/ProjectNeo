using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Squads;
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
        public IEnumerator UnitOverlap_LiftsUnitOntoNewWall_AndEmptyGateSiteRemainsReserved()
        {
            Vector2Int unitCell = world.Layout.WorldToCell(world.LocalPoint(sandbox.Unit.transform.position));
            float health = sandbox.Unit.GetComponent<CharacterActor>().Health.Current;
            Assert.AreEqual(PlacementIssue.None, world.CheckPlacement(wall, unitCell, 0));
            Assert.IsTrue(world.TryPlace(wall, unitCell, 0, out PlacedStructure built));
            Assert.AreEqual(built.transform.position.y + wall.Height, sandbox.Unit.transform.position.y, 0.01f);
            yield return WaitForNavigation(world);
            yield return null;
            Assert.IsTrue(sandbox.Unit.GetComponent<NavMeshAgent>().isOnNavMesh);
            Assert.AreEqual(health, sandbox.Unit.GetComponent<CharacterActor>().Health.Current);
            var gate = world.Structures.Single(piece => piece.Placement.Definition.Kind == BuildPieceKind.Gate);
            Vector2Int gateCell = gate.Placement.Cell;
            Assert.IsFalse(world.Layout.Remove(gate.Placement.Id));
            gate.ReceiveDamage(gate.Health.Current);
            Assert.AreEqual(PlacementIssue.ReservedGate, world.CheckPlacement(wall, gateCell, 0));
            yield return WaitForNavigation(world);
        }

        [UnityTest]
        public IEnumerator CommandAim_DistinguishesWallTopGroundAndVerticalFaces()
        {
            var root = new GameObject("AimTest");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.SetActive(false);
            var config = SquadConfig.Create(1, 1);
            var settings = SquadCommandSettings.Create();
            try
            {
                var center = root.AddComponent<SquadCommandCenter>();
                center.Configure(config, settings, new SquadRoster[0]);
                var cameraRoot = new GameObject("AimCamera");
                cameraRoot.transform.SetParent(root.transform);
                var camera = cameraRoot.AddComponent<Camera>();
                camera.enabled = false;
                var commandInput = root.AddComponent<SquadCommandInput>();
                commandInput.Configure(center, camera);
                root.SetActive(true);
                yield return null;
                var support = world.Structures.Single(piece => piece.Placement.Cell == new Vector2Int(4, 8));
                Vector3 top = support.transform.position + Vector3.up * wall.Height;
                camera.transform.position = top + new Vector3(0f, 10f, -10f);
                camera.transform.LookAt(top);
                Assert.IsTrue(commandInput.TryGetAimPoint(out Vector3 chosen));
                Assert.That(chosen.y, Is.EqualTo(top.y).Within(0.2f), "성벽 위를 가리키면 지면을 고르면 안 된다.");

                Vector3 side = support.transform.position + Vector3.up * (wall.Height * 0.5f);
                camera.transform.position = side + Vector3.back * 6f;
                camera.transform.LookAt(side);
                Assert.IsFalse(commandInput.TryGetAimPoint(out _), "벽 옆면을 가리켜 윗면이나 뒤쪽 땅을 고르면 안 된다.");

                Vector3 ground = new Vector3(12f, 0f, -10f);
                camera.transform.position = ground + Vector3.up * 10f;
                camera.transform.LookAt(ground);
                Assert.IsTrue(commandInput.TryGetAimPoint(out chosen));
                Assert.That(chosen.y, Is.EqualTo(0f).Within(0.2f));
            }
            finally { Object.Destroy(root); Object.Destroy(config); Object.Destroy(settings); }
        }

        [UnityTest]
        public IEnumerator MoveDirective_ClimbsWall_ThenStopsAtPartialPathWhenStairsCollapse()
        {
            var ai = sandbox.Unit.gameObject.AddComponent<UnitCombatAI>();
            var settings = UnitAISettings.Create();
            ai.Configure(settings);
            try
            {
                var landing = world.Structures.Single(piece => piece.Placement.Cell == new Vector2Int(5, 8));
                Vector3 top = landing.transform.position + Vector3.up * wall.Height;
                ai.SetDirective(UnitDirective.Move, top);
                int frames = 0;
                while (ai.Directive != UnitDirective.Hold && frames++ < 900) yield return null;
                Assert.AreEqual(UnitDirective.Hold, ai.Directive);
                Assert.That(Vector3.Distance(top, ai.transform.position), Is.LessThan(0.5f));

                // 성벽 위에서 지상으로 향하는 길이 도중에 사라진다.
                ai.SetDirective(UnitDirective.Move, new Vector3(-7.5f, 0f, -8f));
                var staircase = world.Structures.Single(piece => piece.Placement.Definition.Kind == BuildPieceKind.Stairs);
                staircase.ReceiveDamage(staircase.Health.Current);
                yield return WaitForNavigation(world);
                frames = 0;
                while (ai.Directive != UnitDirective.Hold && frames++ < 900) yield return null;
                Assert.AreEqual(UnitDirective.Hold, ai.Directive, "부분 경로의 끝에서 이동을 끝내야 한다.");
                Assert.That(ai.transform.position.y, Is.GreaterThan(wall.Height - 0.2f));
                Assert.IsFalse(sandbox.Unit.GetComponent<UnitFallController>().IsFalling);
            }
            finally { Object.Destroy(settings); }
        }

        [UnityTest]
        public IEnumerator MoveDirective_ToDisconnectedWall_StopsAtClosestReachableGround()
        {
            var ai = sandbox.Unit.gameObject.AddComponent<UnitCombatAI>();
            var settings = UnitAISettings.Create();
            ai.Configure(settings);
            try
            {
                var staircase = world.Structures.Single(piece => piece.Placement.Definition.Kind == BuildPieceKind.Stairs);
                staircase.ReceiveDamage(staircase.Health.Current);
                yield return WaitForNavigation(world);
                var isolated = world.Structures.Single(piece => piece.Placement.Cell == new Vector2Int(10, 8));
                Vector3 target = isolated.transform.position + Vector3.up * wall.Height;
                var agent = sandbox.Unit.GetComponent<NavMeshAgent>();
                var expected = new NavMeshPath();
                Assert.IsTrue(agent.CalculatePath(target, expected));
                Assert.AreEqual(NavMeshPathStatus.PathPartial, expected.status);
                Vector3 end = expected.corners.Last();
                ai.SetDirective(UnitDirective.Move, target);
                int frames = 0;
                while (ai.Directive != UnitDirective.Hold && frames++ < 900) yield return null;
                Assert.AreEqual(UnitDirective.Hold, ai.Directive);
                Assert.That(Vector3.Distance(end, ai.transform.position), Is.LessThan(0.6f));
                Assert.That(ai.transform.position.y, Is.LessThan(0.3f));
            }
            finally { Object.Destroy(settings); }
        }

        [UnityTest]
        public IEnumerator MoveDirective_ReplansWhenDestinationWallDisappears()
        {
            var ai = sandbox.Unit.gameObject.AddComponent<UnitCombatAI>();
            var settings = UnitAISettings.Create();
            ai.Configure(settings);
            try
            {
                var target = world.Structures.Single(piece => piece.Placement.Cell == new Vector2Int(5, 8));
                Vector3 destination = target.transform.position + Vector3.up * wall.Height;
                Vector3 start = ai.transform.position;
                ai.SetDirective(UnitDirective.Move, destination);
                yield return null;
                target.ReceiveDamage(target.Health.Current);
                yield return WaitForNavigation(world);
                int frames = 0;
                while (ai.Directive != UnitDirective.Hold && frames++ < 900) yield return null;
                Assert.AreEqual(UnitDirective.Hold, ai.Directive);
                Assert.That(Vector3.Distance(start, ai.transform.position), Is.GreaterThan(1f),
                    "목적지가 무너져도 도달 가능한 가까운 지점으로 이동해야 한다.");
                Assert.IsTrue(ai.GetComponent<NavMeshAgent>().isOnNavMesh);
            }
            finally { Object.Destroy(settings); }
        }

        [UnityTest]
        public IEnumerator MoveDirective_SurvivesSupportCollapse_AndReachesGroundDestination()
        {
            var ai = sandbox.Unit.gameObject.AddComponent<UnitCombatAI>();
            var settings = UnitAISettings.Create();
            ai.Configure(settings);
            try
            {
                var landing = world.Structures.Single(piece => piece.Placement.Cell == new Vector2Int(5, 8));
                ai.SetDirective(UnitDirective.Move, landing.transform.position + Vector3.up * wall.Height);
                int frames = 0;
                while (ai.Directive != UnitDirective.Hold && frames++ < 900) yield return null;
                Assert.That(ai.transform.position.y, Is.GreaterThan(wall.Height - 0.2f));
                Vector3 target = new Vector3(-7.5f, 0f, -8f);
                ai.SetDirective(UnitDirective.Move, target);
                sandbox.CollapseUnderUnit();
                var fall = ai.GetComponent<UnitFallController>();
                Assert.IsTrue(fall.IsFalling);
                frames = 0;
                while ((fall.IsFalling || ai.Directive != UnitDirective.Hold) && frames++ < 900) yield return null;
                Assert.IsFalse(fall.IsFalling);
                Assert.Greater(fall.LastFallDamage, 0f);
                Assert.AreEqual(UnitDirective.Hold, ai.Directive);
                Assert.That(Vector3.Distance(target, ai.transform.position), Is.LessThan(0.6f));
            }
            finally { Object.Destroy(settings); }
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
        public IEnumerator SiegeWeapon_MountsOnWallTop_WithoutChangingFloorOrNavigation_AndRejectsBadSpots()
        {
            var hud = scene.GetRootGameObjects().Select(root => root.GetComponent<ProjectNeo.UI.ConstructionHud>())
                .Single(value => value != null);
            SiegeWeaponDefinition cannon = hud.Weapons[0];
            var support = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall);
            var ramp = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Stairs);
            Assert.IsTrue(support.TryGetMountPose("top", out Pose top));
            int pieces = world.Layout.Placements.Count;
            int revision = world.NavigationRevision;

            controller.SelectWeapon(cannon);
            controller.UpdateWeaponPreview(null, world.WorldCenter(wall, new Vector2Int(1, 1), 0));
            Assert.AreEqual(MountIssue.NoSupport, controller.MountIssue);
            Assert.IsFalse(controller.Confirm());
            controller.UpdateWeaponPreview(ramp, ramp.transform.position);
            Assert.AreEqual(MountIssue.NoMountPoint, controller.MountIssue);
            Assert.IsFalse(controller.Confirm());
            Assert.AreEqual(0, world.Weapons.Count);

            controller.UpdateWeaponPreview(support, support.transform.position);
            Assert.AreEqual(MountIssue.None, controller.MountIssue);
            Assert.AreEqual(0, world.Weapons.Count, "미리보기만으로 설치되면 안 된다.");
            Assert.IsTrue(controller.Confirm());
            PlacedSiegeWeapon weapon = world.Weapons.Single();
            Assert.AreSame(cannon, weapon.Definition);
            Assert.AreSame(support, weapon.Support);
            Assert.That(Vector3.Distance(top.position, weapon.transform.position), Is.LessThan(0.001f));
            Assert.AreEqual(cannon.Size, weapon.transform.localScale);
            Assert.IsFalse(weapon.transform.IsChildOf(support.transform.parent), "무기는 길찾기 수집 대상 밖에 둔다.");
            Assert.IsFalse(controller.Confirm());
            Assert.AreEqual(MountIssue.Occupied, controller.MountIssue);
            Assert.AreEqual(1, world.Weapons.Count);

            Assert.AreEqual(pieces, world.Layout.Placements.Count);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsFalse(world.IsRebuilding || world.RebuildPending);
            Assert.AreEqual(revision, world.NavigationRevision);
        }

        [UnityTest]
        public IEnumerator SupportCollapse_DestroysMountedWeapon_AndNotifiesItsModulesOnce()
        {
            var template = new GameObject("TestWeaponTemplate");
            SceneManager.MoveGameObjectToScene(template, scene);
            template.AddComponent<PlacedSiegeWeapon>();
            template.AddComponent<RecordingSiegeWeaponModule>();
            SiegeWeaponDefinition definition = SiegeWeaponDefinition.Create("test", "test", Vector3.one, template);
            try
            {
                PlacedStructure[] walls = world.Structures
                    .Where(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall).Take(2).ToArray();
                Assert.IsTrue(world.TryMount(definition, walls[0], "top", out var doomed));
                Assert.IsTrue(world.TryMount(definition, walls[1], "top", out var kept));
                var doomedModule = doomed.GetComponent<RecordingSiegeWeaponModule>();
                var keptModule = kept.GetComponent<RecordingSiegeWeaponModule>();
                Assert.AreEqual(1, doomedModule.Installed);
                int worldEvents = 0;
                world.WeaponRemoved += (_, _) => worldEvents++;
                int supportId = walls[0].Placement.Id;

                walls[0].ReceiveDamage(walls[0].Health.Current);
                Assert.IsTrue(doomed.IsRemoved);
                Assert.AreEqual(1, doomedModule.Removed);
                Assert.AreEqual(SiegeWeaponRemoval.SupportDestroyed, doomedModule.LastReason);
                Assert.AreSame(kept, world.Weapons.Single());
                Assert.IsNull(world.Mounts.At(supportId, "top"));
                Assert.AreEqual(0, keptModule.Removed);
                yield return null;
                Assert.IsTrue(doomed == null, "받치던 성벽과 함께 무기 오브젝트도 사라져야 한다.");
                Assert.IsFalse(world.TryMount(definition, walls[0], "top", out _), "무너진 조각에는 설치할 수 없다.");

                Assert.IsTrue(world.RemoveWeapon(kept));
                Assert.IsFalse(world.RemoveWeapon(kept));
                Assert.AreEqual(SiegeWeaponRemoval.Dismantled, keptModule.LastReason);
                Assert.AreEqual(1, keptModule.Removed);
                Assert.AreEqual(2, worldEvents);
                Assert.AreEqual(0, world.Weapons.Count);
                Assert.AreEqual(MountIssue.None, world.CheckMount(definition, walls[1], "top"));
                yield return WaitForNavigation(world);
            }
            finally
            {
                Object.Destroy(template);
                Object.Destroy(definition);
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
