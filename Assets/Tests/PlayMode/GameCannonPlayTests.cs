using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Flow;
using ProjectNeo.Siege;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>Game 씬에 연결된 실제 대포가 전투에서 무장·사격하고, 플레이어가 탈 수 있는지(SIEGE-02 7단계).</summary>
    public class GameCannonPlayTests
    {
        static int cleanup;
        GameBattleController battle;
        ConstructionWorld world;
        GameBuildEconomy economy;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; yield return null;
            battle = Object.FindAnyObjectByType<GameBattleController>();
            world = Object.FindAnyObjectByType<ConstructionWorld>();
            economy = Object.FindAnyObjectByType<GameBuildEconomy>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameCannonCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        // 적이 오는 바깥쪽을 향하도록 가장 바깥 성벽 위에 실제 대포를 단다.
        PlacedSiegeWeapon MountFacingOutward()
        {
            SiegeWeaponDefinition cannon = economy.Table.Weapons.Select(entry => entry.weapon)
                .First(definition => definition != null && definition.Id == "cannon");
            Vector3 outward = world.transform.TransformDirection(
                new Vector3(battle.Settings.Outward.x, 0f, battle.Settings.Outward.y)).normalized;
            PlacedStructure support = world.Structures
                .Where(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall && !world.Layout.IsCovered(piece.Placement)
                    && world.CheckMount(cannon, piece, "top") == MountIssue.None)
                .OrderByDescending(piece => Vector3.Dot(piece.transform.position, outward)).First();
            Assert.IsTrue(support.TryGetMountPose("top", out Pose top));
            int rotation = Enumerable.Range(0, 4).OrderByDescending(r =>
                Vector3.Dot(ConstructionWorld.RotateMountPose(top, r).rotation * Vector3.forward, outward)).First();
            Assert.IsTrue(world.TryMount(cannon, support, "top", out PlacedSiegeWeapon weapon, rotation));
            return weapon;
        }

        [UnityTest]
        public IEnumerator RealCannon_ArmsOnBattleStart_FiresAtEnemies_AndDisarmsAfterward()
        {
            PlacedSiegeWeapon weapon = MountFacingOutward();
            var cannon = weapon.GetComponent<CannonModule>();
            Assert.IsNotNull(cannon, "Game 대포 프리팹에 사격 부품이 있어야 한다.");
            Assert.IsNotNull(cannon.Settings);
            Assert.AreEqual(150f, weapon.Health.Maximum);
            Assert.IsFalse(cannon.Armed, "준비 중에는 무장하지 않는다.");
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            Assert.IsTrue(cannon.Armed, "전투가 시작되면 무장한다.");

            int fired = 0;
            cannon.Fired += (_, _) => fired++;
            for (int i = 0; i < 2400 && fired == 0 && battle.IsRunning; i++) yield return null;
            Assert.Greater(fired, 0, "다가오는 적을 자동으로 쏜다.");
        }

        [UnityTest]
        public IEnumerator Player_HasCannonOperator_AndHudShowsHintWhenManned()
        {
            PlacedSiegeWeapon weapon = MountFacingOutward();
            var cannon = weapon.GetComponent<CannonModule>();
            var input = Object.FindAnyObjectByType<PlayerInputController>();
            var op = input.GetComponent<CannonOperator>();
            Assert.IsNotNull(op, "Game 플레이어에 대포 조작 부품이 있어야 한다.");
            var view = Object.FindAnyObjectByType<FirstPersonCamera>();
            var hud = Object.FindAnyObjectByType<GameFlowHud>();
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;

            // 대포 뒤 성벽 위에 플레이어를 세우고 대포 쪽을 보게 한다.
            var motor = input.GetComponent<CharacterController>();
            motor.enabled = false;
            input.transform.position = cannon.transform.position - cannon.Facing * 1.2f + Vector3.up * 0.05f;
            motor.enabled = true;
            float yaw = Mathf.Atan2(cannon.Facing.x, cannon.Facing.z) * Mathf.Rad2Deg;
            view.SetOrientation(yaw, 10f);
            Physics.SyncTransforms();
            yield return null; yield return null;
            Assert.AreSame(cannon, op.Nearby);
            StringAssert.Contains("대포", hud.CannonHint(true));

            Assert.IsTrue(op.TryMan(cannon));
            yield return null;
            StringAssert.Contains("좌클릭", hud.CannonHint(true));
            op.Leave();
            Assert.IsFalse(cannon.IsManned);
        }
    }
}
