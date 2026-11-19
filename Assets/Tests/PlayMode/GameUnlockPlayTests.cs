using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Flow;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>META-07: Game 씬의 새 런이 열린 시작 보너스를 받고, 시설이 해금 트랙을 읽는다.</summary>
    public class GameUnlockPlayTests
    {
        static int cleanup;

        IEnumerator LoadGame(params string[] openedEntries)
        {
            GameSession.Reset();
            GameSession.Meta.openedEntryIds.AddRange(openedEntries);
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameUnlockCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset();
        }

        static int Potions(RunState state) => state.Inventory.Items.Count(item => item.Definition.Id == "item_potion");
        static int StartingRecruits() => Object.FindObjectsByType<UnitCombatAI>().Count(unit => unit.name.Contains("_Recruit"));

        [UnityTest]
        public IEnumerator NewRun_GetsOpenedStartingBonuses()
        {
            yield return LoadGame("bonus_gold_50", "bonus_potions_2", "bonus_grunts_2");
            RunState state = GameSession.Flow.Run.State;

            Assert.AreEqual(150, state.Wallet.Gold, "RunStart 100 + 시작 골드 +50");
            Assert.AreEqual(2, Potions(state));
            Assert.AreEqual(2, StartingRecruits(), "시작 일반병 +2가 병영에서 나온다");
        }

        [UnityTest]
        public IEnumerator NewRun_WithoutBonuses_GetsOnlyRunStartFunds()
        {
            yield return LoadGame();
            RunState state = GameSession.Flow.Run.State;

            Assert.AreEqual(100, state.Wallet.Gold);
            Assert.AreEqual(0, Potions(state));
            Assert.AreEqual(0, StartingRecruits());
        }

        [UnityTest]
        public IEnumerator Facilities_UseTheRunStartUnlockCatalog()
        {
            yield return LoadGame();
            GameFacilities facilities = Object.FindAnyObjectByType<GameFacilities>();
            UnlockCatalog catalog = facilities.StartConfig.UnlockCatalog;

            Assert.IsNotNull(catalog, "RunStart.asset에 해금 트랙이 연결돼 있어야 한다.");
            Assert.IsEmpty(catalog.Validate());
            Assert.IsFalse(UnlockTracks.IsContentAvailable(GameSession.Meta, catalog, UnlockPayloadKind.Hero, "hero_knight"));
            Assert.IsTrue(UnlockTracks.IsContentAvailable(GameSession.Meta, catalog, UnlockPayloadKind.Hero, "hero_swordsman"));
        }
    }
}
