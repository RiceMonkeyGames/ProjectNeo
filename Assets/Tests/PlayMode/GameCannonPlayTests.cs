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

        // 적이 오는 바깥쪽을 향하도록 가장 바깥 성벽 앞에 보루를 짓고 실제 대포를 단다(SIEGE-11: 무기는 보루에만).
        PlacedSiegeWeapon MountFacingOutward()
        {
            SiegeWeaponDefinition cannon = economy.Table.Weapons.Select(entry => entry.weapon)
                .First(definition => definition != null && definition.Id == "cannon");
            Vector3 outward = world.transform.TransformDirection(
                new Vector3(battle.Settings.Outward.x, 0f, battle.Settings.Outward.y)).normalized;
            PlacedStructure support = BastionTestSupport.PlaceInFrontOfOutermostWall(world,
                BastionTestSupport.Definition(SceneManager.GetActiveScene()), battle.Settings.Outward,
                wall => !world.Layout.IsCovered(wall.Placement));
            Assert.AreEqual(MountIssue.None, world.CheckMount(cannon, support, "top"));
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
            StringAssert.Contains("대포", hud.CannonHint());

            Assert.IsTrue(op.TryMan(cannon));
            yield return null;
            StringAssert.Contains("클릭", hud.CannonHint());

            // 조준선에 발사 가능·재장전 상태가 보인다(SIEGE-07).
            yield return null;
            CannonReticleView reticle = hud.CannonReticle;
            Assert.IsNotNull(reticle, "대포에 타면 조준선 표시가 생긴다.");
            Assert.IsTrue(reticle.IsShowing);
            Assert.AreEqual(1f, reticle.BarFill, 0.001f, "장전된 상태에서는 막대가 가득 차 있다.");
            Assert.AreEqual(op.Settings.ColorFor(op.AimState), reticle.ReticleColor);
            Assert.IsNotEmpty(reticle.LabelText);

            // 보루 대포(SIEGE-11)는 자동 ±90도, 직접 조준 ±135도다. 설치 방향 옆 110도로 시점을 돌리면
            // 자동 사격 범위 밖까지 시점과 포신이 따라가고, 160도로 돌리면 ±135도에서 멈춘다.
            Assert.AreEqual(90f, cannon.Settings.AimHalfAngle, "보루 대포 자동 사격 범위");
            Assert.AreEqual(135f, cannon.Settings.ManualAimHalfAngle, "보루 대포 직접 조준 범위");
            view.SetOrientation(yaw + 110f, 10f);
            op.ApplyLook(Vector2.zero);
            Assert.AreEqual(0f, Mathf.DeltaAngle(yaw + 110f, view.Yaw), 0.01f);
            for (int i = 0; i < 60 && cannon.Swivel < 109.9f; i++) yield return null;
            Assert.AreEqual(110f, cannon.Swivel, 0.1f);
            view.SetOrientation(yaw + 160f, 10f);
            op.ApplyLook(Vector2.zero);
            Assert.AreEqual(0f, Mathf.DeltaAngle(yaw + 135f, view.Yaw), 0.01f, "시점은 ±135도에서 멈춘다.");

            Assert.IsTrue(cannon.FireWithVelocity(cannon.BarrelFacing * 20f + Vector3.up * 5f));
            yield return null; yield return null;
            Assert.AreEqual(CannonAimState.Reloading, op.AimState);
            Assert.Less(reticle.BarFill, 0.5f, "쏜 직후에는 막대가 비어 있다가 찬다.");
            Assert.AreEqual(op.Settings.ReloadingColor, reticle.ReticleColor);
            StringAssert.Contains("재장전", reticle.LabelText);

            op.Leave();
            yield return null;
            Assert.IsFalse(reticle.IsShowing, "내리면 조준선 표시를 숨긴다.");
            Assert.IsFalse(cannon.IsManned);
        }
    }
}
