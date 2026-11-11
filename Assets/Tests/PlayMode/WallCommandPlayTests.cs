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
            foreach (UnitCombatAI member in center.Squads[0].Members)
                Assert.That(member.transform.position.y, Is.EqualTo(3f).Within(0.2f));
            for (int i = 0; i < others.Length; i++)
                Assert.That(Vector3.Distance(others[i], center.Squads[1].Members[i].transform.position), Is.LessThan(0.1f));
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
