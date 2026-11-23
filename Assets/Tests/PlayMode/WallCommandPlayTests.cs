using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    public class WallCommandPlayTests
    {
        Scene scene;
        WallCommandSandbox mode;
        ConstructionWorld world;
        SquadCommandCenter center;
        ConstructionSandbox construction;
        float previousCapture;
        CursorLockMode previousLock;
        bool previousVisible;
        T Find<T>() where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousCapture = Time.captureDeltaTime;
            previousLock = Cursor.lockState; previousVisible = Cursor.visible;
            Time.captureDeltaTime = 1f / 30f;
            yield return SceneManager.LoadSceneAsync("WallCommandPrototype", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            mode = Find<WallCommandSandbox>(); world = Find<ConstructionWorld>();
            center = Find<SquadCommandCenter>(); construction = Find<ConstructionSandbox>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.UnloadSceneAsync(scene);
            Time.captureDeltaTime = previousCapture;
            Cursor.lockState = previousLock; Cursor.visible = previousVisible;
        }

        IEnumerator WaitForHold()
        {
            int frames = 0;
            while (center.Commander.OrderOf(0).Kind != SquadOrderKind.Hold && frames++ < 1200) yield return null;
            Assert.AreEqual(SquadOrderKind.Hold, center.Commander.OrderOf(0).Kind,
                string.Join("; ", center.Squads[0].Members.Select(unit => unit.name + " " + unit.Directive + " " + unit.transform.position)));
        }

        [UnityTest]
        public IEnumerator Views_SwitchCameraInputAndBodyVisibility_WithoutClearingOrders()
        {
            var first = Find<FirstPersonCamera>();
            var player = Find<PlayerInputController>();
            var placement = Find<PlacementController>();
            var input = Find<SquadCommandInput>();
            var orbit = Find<ConstructionCameraController>();
            Assert.IsTrue(mode.IsConstructionView);
            Assert.IsTrue(placement.enabled && orbit.gameObject.activeInHierarchy);
            Assert.IsFalse(player.enabled || input.enabled || first.gameObject.activeInHierarchy);
            center.Commander.Select(0);
            var destination = new Vector3(-7.5f, 0f, -5f);
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Move, destination));
            mode.SetConstructionView(false);
            yield return null;
            Assert.IsTrue(player.enabled && input.enabled && first.gameObject.activeInHierarchy);
            Assert.IsFalse(placement.enabled || orbit.gameObject.activeInHierarchy);
            Assert.IsFalse(first.GetComponent<Camera>().orthographic);
            Assert.That(Vector3.Distance(first.transform.position,
                player.transform.position + Vector3.up * 1.6f), Is.LessThan(0.01f));
            var renderer = player.transform.Find("Visual").GetComponentInChildren<Renderer>();
            Assert.AreEqual(ShadowCastingMode.ShadowsOnly, renderer.shadowCastingMode);
            Vector3 before = first.transform.forward;
            first.Look(new Vector2(90f, -50f));
            yield return null;
            Assert.Greater(Vector3.Distance(before, first.transform.forward), 0.01f);
            mode.SetConstructionView(true);
            Assert.IsTrue(placement.enabled);
            Assert.IsFalse(player.enabled || input.enabled);
            Assert.AreNotEqual(ShadowCastingMode.ShadowsOnly, renderer.shadowCastingMode);
            Assert.AreEqual(destination, center.Commander.OrderOf(0).Destination);
        }

        [UnityTest]
        public IEnumerator Squad_ClimbsStairsAndHoldsFormationOnWall_WithoutMovingOtherSquads()
        {
            Vector3[] others = center.Squads[1].Members.Select(unit => unit.transform.position).ToArray();
            construction.WalkToWall();
            Assert.AreEqual(SquadOrderKind.Move, center.Commander.OrderOf(0).Kind, construction.Message);
            mode.SetConstructionView(false);
            yield return WaitForHold();
            // SIEGE-18: 1층 성벽 윗면은 5m.
            float wallTop = Object.FindAnyObjectByType<ConstructionWorld>().Structures
                .First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall).Placement.Definition.Height;
            foreach (UnitCombatAI member in center.Squads[0].Members)
                Assert.That(member.transform.position.y, Is.EqualTo(wallTop).Within(0.2f));
            for (int i = 0; i < others.Length; i++)
                Assert.That(Vector3.Distance(others[i], center.Squads[1].Members[i].transform.position), Is.LessThan(0.1f));
        }

        [UnityTest]
        public IEnumerator Formation_OnWallTop_ClampsRanksAcrossTheWalkway()
        {
            // COMBAT-08: 한 줄 1명(폭 0)으로 8명을 세우면 8줄이 되지만, 성벽 위 통로를 가로지르는 방향으로는 들어가는 줄 수만 선다.
            construction.WalkToWall();
            Vector3 anchor = center.Commander.OrderOf(0).Destination;
            yield return null;
            var slots = new System.Collections.Generic.List<Vector3>();
            int best = int.MaxValue;
            foreach (float facing in new[] { 0f, 90f })
            {
                center.PlanSlots(FormationKind.Line, 0f, anchor, facing, false, 8, slots);
                Vector3 forward = Quaternion.Euler(0f, facing, 0f) * Vector3.forward;
                int ranks = slots.Select(slot => Mathf.RoundToInt(Vector3.Dot(slot - anchor, forward) / 0.3f)).Distinct().Count();
                if (ranks >= best) continue;
                best = ranks;
                foreach (Vector3 slot in slots) Assert.That(slot.y, Is.EqualTo(anchor.y).Within(0.2f), "every slot stays on the wall top");
            }
            // SIEGE-18: 7.5m 칸 통로에는 줄 간격 1.1m로 네 줄까지 선다(한 줄이면 8줄).
            Assert.LessOrEqual(best, 4, "across the 7.5m wall top at most four ranks fit");
        }

        [UnityTest]
        public IEnumerator Squad_StopsOnWallWhenStairsDisappear()
        {
            construction.WalkToWall();
            yield return WaitForHold();
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Move, new Vector3(-7.5f, 0f, -8f)));
            var stairs = world.Structures.Single(piece => piece.Placement.Definition.Kind == BuildPieceKind.Stairs);
            stairs.ReceiveDamage(stairs.Health.Current);
            yield return WaitForHold();
            Assert.IsTrue(center.Squads[0].Members.All(unit => unit.transform.position.y > 2.8f));
        }

        [UnityTest]
        public IEnumerator Squad_FallsWhenSupportCollapses_ThenResumesGroundMove()
        {
            construction.WalkToWall();
            yield return WaitForHold();
            construction.CollapseUnderUnit();
            Assert.IsTrue(construction.Unit.GetComponent<UnitFallController>().IsFalling, construction.Message);
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Move, new Vector3(-7.5f, 0f, -8f)));
            yield return WaitForHold();
            foreach (UnitCombatAI unit in center.Squads[0].Members)
            {
                Assert.IsFalse(unit.GetComponent<UnitFallController>().IsFalling);
                Assert.Greater(unit.GetComponent<UnitFallController>().LastFallDamage, 0f);
                Assert.That(unit.transform.position.y, Is.LessThan(0.3f));
            }
        }
    }
}
