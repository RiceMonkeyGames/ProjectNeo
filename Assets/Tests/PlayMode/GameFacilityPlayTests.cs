using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>D-0: 안뜰 시설 건물에서 F로 팝업을 열고 Esc로 닫는다.</summary>
    public class GameFacilityPlayTests
    {
        static int cleanup;
        GameFacilities facilities;
        GamePhaseView view;
        PlayerInputController player;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; yield return null;
            facilities = Object.FindAnyObjectByType<GameFacilities>();
            view = Object.FindAnyObjectByType<GamePhaseView>();
            player = Object.FindAnyObjectByType<PlayerInputController>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameFacilityCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        FacilityStation Station(FacilityType type) =>
            Object.FindObjectsByType<FacilityStation>().Single(station => station.Type == type);

        [UnityTest]
        public IEnumerator 런_데이터에_시설_다섯_곳이_시작_레벨로_붙는다()
        {
            Assert.IsNotNull(facilities, "Game 씬 전투 오브젝트에 시설 컴포넌트가 있어야 한다.");
            foreach (FacilityType type in new[] { FacilityType.Tavern, FacilityType.Shop, FacilityType.TrainingGround,
                         FacilityType.Smithy, FacilityType.Defense })
            {
                FacilityState state = facilities.Facility(type);
                Assert.IsNotNull(state, type.ToString());
                Assert.AreEqual(0, state.Level);
            }
            Assert.AreEqual(5, GameSession.Flow.Run.State.Facilities.Count);
            yield break;
        }

        [UnityTest]
        public IEnumerator 건물_네_곳에서_각자의_팝업이_열리고_닫힌다()
        {
            foreach (FacilityType type in new[] { FacilityType.Tavern, FacilityType.Shop, FacilityType.TrainingGround, FacilityType.Smithy })
            {
                FacilityStation station = Station(type);
                Assert.IsTrue(station.Interact(), type.ToString());
                Assert.IsTrue(view.IsPanelView && !view.IsExplorationView);
                Assert.AreEqual(type, facilities.OpenFacility);
                Assert.IsFalse(player.enabled, "팝업 중에는 이동·시점을 멈춘다.");
                Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
                Assert.AreEqual(facilities.NameOf(type), facilities.TitleText);
                StringAssert.Contains("레벨 1 / 3", facilities.StatusText);
                StringAssert.Contains("골드 100", facilities.StatusText);
                Assert.IsFalse(view.EnterConstruction(), "팝업 중에는 건설 모드에 들어가지 않는다.");
                Assert.IsFalse(station.Interact(), "이미 열려 있으면 다시 열지 않는다.");
                facilities.Close();
                yield return null;
                Assert.IsFalse(facilities.IsOpen);
                Assert.IsTrue(view.IsExplorationView && player.enabled);
                Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState);
            }
        }

        [UnityTest]
        public IEnumerator 상점을_바라보고_F로_열고_Esc로_닫으며_전투_중에는_열리지_않는다()
        {
            var interaction = Object.FindAnyObjectByType<PreparationInteraction>();
            var camera = Object.FindAnyObjectByType<FirstPersonCamera>();
            var hud = Object.FindAnyObjectByType<GameFlowHud>();
            var motor = player.GetComponent<CharacterController>();
            FacilityStation shop = Station(FacilityType.Shop);
            Keyboard originalKeyboard = Keyboard.current;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Bounds bounds = shop.GetComponent<BoxCollider>().bounds;
                Vector3 near = new Vector3(bounds.center.x, 0f, bounds.min.z - 2f);
                motor.enabled = false; player.transform.position = near; motor.enabled = true;
                yield return null;
                Cursor.lockState = CursorLockMode.Locked;
                camera.transform.rotation = Quaternion.LookRotation(Vector3.forward);
                Physics.SyncTransforms();
                interaction.RefreshTarget();
                Assert.AreSame(shop, interaction.Target);
                Assert.AreEqual("상점 이용", shop.Prompt);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                yield return null;
                Assert.IsTrue(facilities.IsOpen && view.IsPanelView);
                Assert.AreEqual(FacilityType.Shop, facilities.OpenFacility);
                yield return null; // 안내 글자는 LateUpdate에서 갱신한다.
                StringAssert.Contains("닫기", hud.InteractionHint);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null; yield return null;
                Assert.IsFalse(facilities.IsOpen, "Esc로 닫는다.");
                Assert.IsTrue(view.IsExplorationView && player.enabled);
                Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState, "닫는 Esc가 이동 커서를 다시 풀지 않아야 한다.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;

                Assert.IsTrue(shop.Interact());
                Assert.IsTrue(GameSession.Flow.StartBattle());
                yield return null;
                Assert.IsFalse(facilities.IsOpen, "전투가 시작되면 팝업을 닫는다.");
                Assert.IsFalse(shop.Interact(), "전투 중에는 열리지 않는다.");
                Assert.IsFalse(view.IsPanelView);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
            }
        }
    }
}
