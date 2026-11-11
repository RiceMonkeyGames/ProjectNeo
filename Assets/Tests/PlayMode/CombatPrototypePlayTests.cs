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
        public IEnumerator Soldier_UsesItsOwnStats_AndAppliesStatChangesToAttacks()
        {
            var definition = SoldierDefinition.Create("test_soldier", new ProjectNeo.Economy.Cost(0),
                new StatValue(StatType.MaxHealth, 80f), new StatValue(StatType.Attack, 20f),
                new StatValue(StatType.AttackSpeed, 1f), new StatValue(StatType.AttackRange, 2.5f));
            var root = new GameObject("TestSoldier");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            var soldier = root.AddComponent<CharacterActor>();
            soldier.Configure(definition, player.Profile);
            try
            {
                root.transform.position = new Vector3(-10f, 0f, 0f);
                Place(dummy, new Vector3(-10f, 0f, 2f));
                root.SetActive(true);
                Assert.IsNull(soldier.Character, "잡졸을 영웅 데이터로 바꾸지 않는다.");
                Assert.AreEqual(80f, soldier.Health.Current);
                soldier.Stats.AddModifier(StatModifier.Flat(StatType.Attack, 10f), this);
                Physics.SyncTransforms();
                Assert.IsTrue(soldier.TryAttack());
                yield return new WaitForSeconds(soldier.Profile.ImpactDelay + 0.05f);
                Assert.AreEqual(70f, dummy.Health.Current);
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(definition);
            }
        }

        [UnityTest]
        public IEnumerator FirstPersonCamera_ClampsPitch_AndFollowsPlayerAtEyeHeight()
        {
            var view = scene.GetRootGameObjects().Select(root => root.GetComponent<FirstPersonCamera>())
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
            Assert.That(Vector3.Distance(view.transform.position, pivot), Is.LessThan(0.01f));
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly,
                player.transform.Find("Visual").GetComponentInChildren<Renderer>().shadowCastingMode);
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
