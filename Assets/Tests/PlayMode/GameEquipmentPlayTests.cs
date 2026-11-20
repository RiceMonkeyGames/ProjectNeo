using System.Collections;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Economy;
using ProjectNeo.Flow;
using ProjectNeo.Stats;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>D-7: 준비 중 I로 장비 화면을 열고 런 플레이어에게 장비를 끼우면 씬 플레이어 스탯이 바뀐다.</summary>
    public class GameEquipmentPlayTests
    {
        static int cleanup;
        GameEquipment equipment;
        GamePhaseView view;
        CharacterActor player;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; yield return null;
            equipment = Object.FindAnyObjectByType<GameEquipment>();
            view = Object.FindAnyObjectByType<GamePhaseView>();
            player = Object.FindAnyObjectByType<PlayerInputController>().GetComponent<CharacterActor>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameEquipmentCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        static ItemDefinition Sword() =>
            ItemDefinition.Create("test_sword", ItemKind.Equipment, new Cost(40), true, StatModifier.Flat(StatType.Attack, 4f));

        [UnityTest]
        public IEnumerator 런_플레이어는_씬_플레이어와_같은_캐릭터다()
        {
            Assert.IsNotNull(equipment, "Game 씬 전투 오브젝트에 장비 화면 컴포넌트가 있어야 한다.");
            Assert.AreSame(player.Character, GameSession.Flow.Run.State.Player.Character);
            yield break;
        }

        [UnityTest]
        public IEnumerator I로_장비_화면을_열고_끼우면_씬_스탯이_오르며_벗으면_돌아온다()
        {
            var state = GameSession.Flow.Run.State;
            ItemDefinition sword = Sword();
            state.Inventory.Add(sword);
            float attack = player.Stats.GetValue(StatType.Attack);

            using var inputScope = new FocusIndependentInputScope();
            Keyboard originalKeyboard = Keyboard.current;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Cursor.lockState = CursorLockMode.Locked;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.I));
                yield return null;
                Assert.IsTrue(equipment.IsOpen && view.IsPanelView, "I로 장비 화면을 연다.");
                Assert.IsFalse(Object.FindAnyObjectByType<PlayerInputController>().enabled);
                Assert.AreEqual(1, equipment.InventoryRows);
                StringAssert.Contains("장비 칸 0 / 3", equipment.StatusText);

                Assert.IsTrue(equipment.TryEquip(0));
                Assert.AreEqual(attack + 4f, player.Stats.GetValue(StatType.Attack), 0.001f);
                Assert.AreEqual(1, player.Character.Equipment.Count);
                Assert.AreEqual(0, state.Inventory.CountOf(sword));
                StringAssert.Contains("장비 칸 1 / 3", equipment.StatusText);
                StringAssert.Contains("장착", equipment.BodyText);

                Assert.IsTrue(equipment.TryUnequip(0));
                Assert.AreEqual(attack, player.Stats.GetValue(StatType.Attack), 0.001f);
                Assert.AreEqual(1, state.Inventory.CountOf(sword));

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null; yield return null;
                Assert.IsFalse(equipment.IsOpen, "Esc로 닫는다.");
                Assert.IsTrue(view.IsExplorationView);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
            }
        }

        [UnityTest]
        public IEnumerator 전투_중에는_장비_화면이_열리지_않는다()
        {
            Assert.IsTrue(equipment.Open());
            Assert.IsTrue(GameSession.Flow.StartBattle());
            yield return null;
            Assert.IsFalse(equipment.IsOpen, "전투가 시작되면 닫힌다.");
            Assert.IsFalse(equipment.Open());
        }
    }
}
