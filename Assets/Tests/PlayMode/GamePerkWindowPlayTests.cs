using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Flow;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using ProjectNeo.Stats;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>META-09: Game 씬의 퍽 창. 준비 단계에 열려 트리 3개의 퍽을 두 번 눌러 찍고, 보상 화면에는 여는 버튼이 보인다.</summary>
    public class GamePerkWindowPlayTests
    {
        static int cleanup;
        GameBattleController battle;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; yield return null;
            battle = Object.FindAnyObjectByType<GameBattleController>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GamePerkWindowCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset();
        }

        [UnityTest]
        public IEnumerator Window_OpensInPreparation_AndTakesAPerkWithTwoClicks()
        {
            GamePerkWindow window = battle.PerkWindow;
            Assert.IsNotNull(window, "RunStart.asset에 퍽 트리가 연결돼 있어야 한다.");
            Assert.IsEmpty(window.Trees.Validate());
            Assert.AreEqual(3, window.Trees.Trees.Count);
            RunState state = battle.RunLink.State;
            state.Perks.BonusPoints = 1;
            PerkDefinition first = window.Trees.Trees[0].Perks[0];
            CharacterActor player = Object.FindObjectsByType<CharacterActor>()
                .First(actor => state.Find(battle.RunLink.IdOf(actor)) is RunHero hero && hero.IsPlayer);
            float attack = player.Stats.GetValue(StatType.Attack);

            Assert.IsTrue(window.Open());
            Assert.IsTrue(window.IsOpen);
            Assert.AreEqual(15, window.RowCount, "트리 3개 x 퍽 5개");
            Assert.AreEqual(UnlockClickResult.Armed, window.Click(first));
            Assert.AreEqual(UnlockClickResult.Opened, window.Click(first));
            StringAssert.Contains("Taken", window.RowText(first));
            Assert.Greater(player.Stats.GetValue(StatType.Attack), attack, "첫 전투 퍽(공격 +15%)이 바로 붙는다");
            Assert.AreEqual(UnlockClickResult.Refused, window.Click(window.Trees.Trees[0].Perks[4]));
            StringAssert.StartsWith("Take ", window.StatusText);

            window.Close();
            Assert.IsFalse(window.IsOpen);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RewardScreen_ShowsTheOpenButton_AndBattleKeepsTheWindowClosed()
        {
            GamePerkWindow window = battle.PerkWindow;
            GameFlow flow = GameSession.Flow;
            Assert.IsFalse(window.IsRewardButtonVisible);

            Assert.IsTrue(flow.StartBattle());
            yield return null;
            Assert.IsFalse(window.Open(), "전투 중에는 열리지 않는다(Q-06)");
            Assert.IsTrue(flow.FinishBattle(BattleOutcome.Victory));
            yield return null; yield return null;

            Assert.IsTrue(window.IsRewardButtonVisible);
            Assert.IsTrue(window.Open());
            yield return null;
            Assert.IsTrue(window.IsOpen);
            Assert.IsFalse(window.IsRewardButtonVisible, "창이 열려 있는 동안 여는 버튼은 숨는다");
            window.Close();
            yield return null;
            Assert.IsTrue(window.IsRewardButtonVisible);
        }
    }
}
