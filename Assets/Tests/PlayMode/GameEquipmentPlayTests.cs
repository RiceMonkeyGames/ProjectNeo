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
                Assert.IsTrue(equipment.PaperDoll, "UI-07 B안 화면이 연결돼 있다.");
                CollectionAssert.AreEqual(new[] { sword.DisplayName }, equipment.StorageSlotNames, "보관함 격자에 장비 1개");
                Assert.AreEqual(5, equipment.WornSlotCount, "UI-07: 투구·방어구·장신구·보조무기·무기 칸(빈 칸 포함)");
                CollectionAssert.IsEmpty(equipment.WornSlotNames);
                StringAssert.Contains("장비 칸 0 / 5", equipment.StatusText);

                // UI-07: 보관함 장비를 미리 보면 왼쪽 스탯에 오를 공격력이 나오고, 상세 칸에 이름이 나온다.
                equipment.PreviewItem(false, 0);
                // 플레이어는 늘 최고 등급이라 등급 공격력 %가 장비 공격력에도 붙는다(UI-07): 오르는 값은 끼운 뒤에 잰다.
                string previewStats = equipment.StatsText;
                StringAssert.Contains(sword.DisplayName, equipment.DetailText);
                Assert.IsTrue(equipment.TooltipShowing, "마우스를 올린 장비는 툴팁으로 보여준다.");
                StringAssert.Contains("공격력 +4", equipment.DetailText);

                Assert.IsTrue(equipment.TryEquip(0));
                CollectionAssert.AreEqual(new[] { sword.DisplayName }, equipment.WornSlotNames);
                CollectionAssert.IsEmpty(equipment.StorageSlotNames);
                float gain = player.Stats.GetValue(StatType.Attack) - attack;
                Assert.GreaterOrEqual(gain, 4f - 0.001f, "칼의 공격력 +4가 (등급 %와 함께) 붙는다.");
                // 미리 보기는 바뀐 뒤의 최종 값을 색으로 보인다(UI-07, 사용자 2026-11-23): 오르면 초록, 내리면 빨강.
                StringAssert.Contains($"<color=#8FD16A>{attack + gain:0.##}</color>", previewStats, "미리 보기 값이 끼운 뒤의 값과 같다.");
                equipment.PreviewItem(true, 0);
                StringAssert.Contains($"<color=#E57A6A>{attack:0.##}</color>", equipment.StatsText, "착용 장비를 미리 보면 벗은 뒤의 값이 빨갛게 나온다.");
                Assert.AreEqual(1, player.Character.Equipment.Count);
                Assert.AreEqual(0, state.Inventory.CountOf(sword));
                StringAssert.Contains("장비 칸 1 / 5", equipment.StatusText);
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

                // UI-07: 연 키(I)를 다시 누르면 닫는다.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.I));
                yield return null;
                Assert.IsTrue(equipment.IsOpen, "I로 다시 연다.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.I));
                yield return null; yield return null;
                Assert.IsFalse(equipment.IsOpen, "I를 한 번 더 누르면 닫는다.");
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
