using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Stats;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    public class CombatPrototypePlayTests
    {
        const string SceneName = "CombatPrototype";
        Scene scene;
        CharacterActor player;
        CharacterActor dummy;
        PlayerInputController input;
        CursorLockMode cursorLock;
        bool cursorVisible;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cursorLock = Cursor.lockState;
            cursorVisible = Cursor.visible;
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            var actors = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CharacterActor>()).ToArray();
            player = actors.Single(actor => actor.name == "Player");
            dummy = actors.Single(actor => actor.name == "TrainingDummy_1");
            input = player.GetComponent<PlayerInputController>();
            input.enabled = false;
            Place(player, Vector3.zero);
            Place(dummy, new Vector3(0f, 0f, 2f));
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.UnloadSceneAsync(scene);
            Cursor.lockState = cursorLock;
            Cursor.visible = cursorVisible;
        }

        static void Place(CharacterActor actor, Vector3 position)
        {
            var motor = actor.GetComponent<CharacterController>();
            if (motor != null) motor.enabled = false;
            actor.transform.position = position;
            if (motor != null) motor.enabled = true;
        }

        [UnityTest]
        public IEnumerator Attack_HitsAfterDelay_RejectsSpam_AndKillsDummy()
        {
            Assert.IsTrue(player.TryAttack());
            Assert.IsFalse(player.TryAttack());
            Assert.AreEqual(100f, dummy.Health.Current, "선딜레이 전에 피해가 들어가면 안 된다.");
            yield return new WaitForSeconds(player.Profile.ImpactDelay + 0.05f);
            Assert.AreEqual(75f, dummy.Health.Current);
            while (!dummy.Health.IsDead)
            {
                yield return new WaitForSeconds(0.75f);
                Assert.IsTrue(player.TryAttack());
                yield return new WaitForSeconds(player.Profile.ImpactDelay + 0.05f);
            }
            Assert.AreEqual(0f, dummy.Health.Current);
            Assert.IsFalse(dummy.GetComponent<Collider>().enabled);
        }

        [UnityTest]
        public IEnumerator Attack_RejectsTargetsBehindOutOfRangeAndBehindWall()
        {
            Vector3[] positions = { new Vector3(0f, 0f, -2f), new Vector3(0f, 0f, 5f), new Vector3(0f, 0f, 2f) };
            for (int i = 0; i < positions.Length; i++)
            {
                Place(dummy, positions[i]);
                GameObject wall = null;
                if (i == 2)
                {
                    wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    SceneManager.MoveGameObjectToScene(wall, scene);
                    wall.transform.position = new Vector3(0f, 1f, 1f);
                    wall.transform.localScale = new Vector3(2f, 2f, 0.2f);
                }
                Physics.SyncTransforms();
                Assert.IsTrue(player.TryAttack());
                yield return new WaitForSeconds(0.75f);
                Assert.AreEqual(100f, dummy.Health.Current);
                if (wall != null) Object.Destroy(wall);
            }
        }

        [UnityTest]
        public IEnumerator Death_CancelsPendingAttackAndMovement()
        {
            player.TryAttack();
            player.ReceiveDamage(100f);
            Vector3 start = player.transform.position;
            input.Move(Vector2.up, 0f, 0.1f);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(100f, dummy.Health.Current);
            Assert.AreEqual(start, player.transform.position);
            Assert.IsFalse(player.TryAttack());
        }

        [UnityTest]
        public IEnumerator Camera_ClampsPitch_AndPullsInBeforeWall()
        {
            var view = scene.GetRootGameObjects().Select(root => root.GetComponent<ThirdPersonCamera>())
                .Single(camera => camera != null);
            var settings = Resources.FindObjectsOfTypeAll<PlayerControlSettings>().First();
            view.Look(new Vector2(0f, 10000f));
            yield return null;
            Assert.That(Mathf.DeltaAngle(0f, view.transform.eulerAngles.x), Is.EqualTo(settings.MinimumPitch).Within(0.1f));
            view.Look(new Vector2(0f, -10000f));
            yield return null;
            Assert.That(Mathf.DeltaAngle(0f, view.transform.eulerAngles.x), Is.EqualTo(settings.MaximumPitch).Within(0.1f));
            view.Look(new Vector2(0f, (settings.MaximumPitch - settings.InitialPitch) / settings.MouseSensitivity));
            Place(player, new Vector3(0f, 0f, -13f));
            Physics.SyncTransforms();
            yield return null;
            Vector3 pivot = player.transform.position + Vector3.up * settings.CameraHeight;
            Assert.Less(Vector3.Distance(view.transform.position, pivot), 2.5f);
        }

        [UnityTest]
        public IEnumerator Movement_UsesStatsCameraYawAndNormalizedDiagonal_AndStopsAtWall()
        {
            Place(dummy, new Vector3(10f, 0f, 10f));
            Physics.SyncTransforms();
            float speed = player.Character.Stats.GetValue(StatType.MoveSpeed);
            input.Move(Vector2.one, 90f, 0.1f);
            Vector3 displacement = player.transform.position;
            Assert.That(new Vector2(displacement.x, displacement.z).magnitude, Is.EqualTo(speed * 0.1f).Within(0.02f));
            Assert.Greater(displacement.x, 0f);
            Assert.Less(displacement.z, 0f);
            Place(player, new Vector3(0f, 0f, 23f));
            for (int i = 0; i < 20; i++) input.Move(Vector2.up, 0f, 0.05f);
            Assert.Less(player.transform.position.z, 24.5f);
            yield return null;
        }
    }
}
