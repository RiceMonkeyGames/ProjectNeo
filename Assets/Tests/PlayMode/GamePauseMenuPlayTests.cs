using System.Collections;
using NUnit.Framework;
using ProjectNeo.Flow;
using ProjectNeo.Meta;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// FLOW-04: 준비 단계 Esc 메뉴. 다른 창이 없을 때 Esc로 열고 닫으며, '저장 후 타이틀로'는 저장한 뒤 타이틀로 가고
    /// 이어 하기로 같은 준비가 돌아온다. 저장하지 못하면 게임에 남는다. 전투·다른 창을 닫는 Esc로는 열리지 않는다.
    /// </summary>
    public class GamePauseMenuPlayTests
    {
        static int cleanup;
        string saveDirectory;
        GameBattleController battle;

        [SetUp]
        public void SetUp()
        {
            GameSession.Reset();
            saveDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ProjectNeoPauseMenuPlayTests_" + System.Guid.NewGuid().ToString("N"));
            GameSession.UseSaveStore(new MetaSaveStore(saveDirectory));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GamePauseMenuCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset();
            GameSession.UseSaveStore(null);
            GameSession.RunSavePolicy = new ProjectNeo.Run.RunSavePolicy();
            if (System.IO.Directory.Exists(saveDirectory)) System.IO.Directory.Delete(saveDirectory, true);
        }

        static IEnumerator WaitForActiveScene(string name)
        {
            for (int frame = 0; frame < 300 && SceneManager.GetActiveScene().name != name; frame++) yield return null;
            Assert.AreEqual(name, SceneManager.GetActiveScene().name);
            yield return null;
        }

        IEnumerator EnterGame(System.Action<TitleScreen> press)
        {
            press(Object.FindAnyObjectByType<TitleScreen>());
            yield return WaitForActiveScene("Game");
            battle = Object.FindAnyObjectByType<GameBattleController>();
            var world = Object.FindAnyObjectByType<ConstructionWorld>();
            yield return null;
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            yield return null;
        }

        IEnumerator NewGameFromTitle(int slot)
        {
            yield return SceneManager.LoadSceneAsync("Title", LoadSceneMode.Single);
            yield return null;
            yield return EnterGame(title => { title.SelectSlot(slot); title.StartGame(); if (title.IsExpeditionOpen) title.Ascension.StartSelected(); });
        }

        IEnumerator OpenGameWithoutSlot()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; yield return null;
            battle = Object.FindAnyObjectByType<GameBattleController>();
        }

        static IEnumerator Press(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator SaveAndReturn_GoesToTitle_AndContinueRestoresTheSamePreparation()
        {
            yield return NewGameFromTitle(0);
            GamePauseMenu menu = battle.PauseMenu;
            Assert.IsNotNull(menu);
            battle.RunLink.State.Wallet.AddGold(123);
            int gold = battle.RunLink.State.Wallet.Gold;
            int stage = GameSession.Flow.Run.StageNumber;

            Assert.IsTrue(menu.Open());
            Assert.IsTrue(menu.IsOpen);
            Assert.IsTrue(menu.SaveAndReturnToTitle());
            yield return WaitForActiveScene("Title");
            Assert.AreEqual(GamePhase.Title, GameSession.Flow.Phase);
            Assert.IsTrue(GameSession.HasRunSave(0), "the run is saved before leaving for Title");
            Assert.AreEqual(gold, GameSession.RunSaves.Inspect(0).Snapshot.gold, "the save holds what was done in this preparation");

            yield return EnterGame(title => { title.SelectSlot(0); title.ContinueGame(); });
            Assert.IsTrue(battle.RunLink.WasRestored);
            Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase);
            Assert.AreEqual(stage, GameSession.Flow.Run.StageNumber);
            Assert.AreEqual(gold, battle.RunLink.State.Wallet.Gold);
        }

        [UnityTest]
        public IEnumerator FailedSave_KeepsTheMenuOpen_AndStaysInTheGame()
        {
            yield return NewGameFromTitle(1);
            GamePauseMenu menu = battle.PauseMenu;
            GameSession.RunSavePolicy.onQuitDuringPreparation = false;
            Assert.IsTrue(menu.Open());
            LogAssert.Expect(LogType.Warning, "Save and return to Title refused: " + GamePauseMenu.SaveFailedText);
            Assert.IsFalse(menu.SaveAndReturnToTitle());
            yield return null; yield return null;
            Assert.IsTrue(menu.IsOpen);
            Assert.AreEqual(GamePauseMenu.SaveFailedText, menu.StatusText);
            Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase);
            Assert.AreEqual("Game", SceneManager.GetActiveScene().name);
            Assert.IsFalse(SceneSwitcher.IsLoading);
            menu.Close();
            Assert.IsFalse(menu.IsOpen);
        }

        [UnityTest]
        public IEnumerator WithoutASlot_SaveAndReturnIsRefused()
        {
            yield return OpenGameWithoutSlot();
            GamePauseMenu menu = battle.PauseMenu;
            Assert.IsTrue(menu.Open());
            LogAssert.Expect(LogType.Warning, "Save and return to Title refused: " + GamePauseMenu.NoSlotText);
            Assert.IsFalse(menu.SaveAndReturnToTitle());
            yield return null;
            Assert.AreEqual(GamePauseMenu.NoSlotText, menu.StatusText);
            Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase);
        }

        [UnityTest]
        public IEnumerator Escape_OpensAndClosesTheMenu_ButNotWhileAnotherWindowCloses_OrInBattle()
        {
            using var inputScope = new FocusIndependentInputScope();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return OpenGameWithoutSlot();
                GamePauseMenu menu = battle.PauseMenu;
                var phaseView = Object.FindAnyObjectByType<GamePhaseView>();
                yield return null;
                Assert.IsTrue(phaseView.IsExplorationView);

                yield return Press(keyboard, Key.Escape);
                Assert.IsTrue(menu.IsOpen, "Esc in first-person exploration opens the menu");
                Assert.IsTrue(phaseView.IsPanelView);
                Assert.AreEqual(CursorLockMode.None, Cursor.lockState);

                yield return Press(keyboard, Key.Escape);
                Assert.IsFalse(menu.IsOpen, "Esc again closes it");
                Assert.IsTrue(phaseView.IsExplorationView);

                // 다른 창을 닫는 Esc로는 메뉴가 열리지 않는다.
                Assert.IsTrue(battle.PerkWindow.Open());
                yield return null;
                yield return Press(keyboard, Key.Escape);
                Assert.IsFalse(battle.PerkWindow.IsOpen);
                Assert.IsFalse(menu.IsOpen, "the Esc that closed the perk window does not open the menu");
                Assert.IsTrue(phaseView.IsExplorationView);

                // 키 안내(H)에 메뉴 키가 있다.
                StringAssert.Contains("메뉴", PrepHudText.KeyList(Object.FindAnyObjectByType<KeyListHud>().Build()));

                Assert.IsTrue(GameSession.Flow.StartBattle());
                yield return null; yield return null;
                yield return Press(keyboard, Key.Escape);
                Assert.IsFalse(menu.IsOpen, "the menu does not open in battle");
                Assert.IsFalse(menu.Open());
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }
    }
}
