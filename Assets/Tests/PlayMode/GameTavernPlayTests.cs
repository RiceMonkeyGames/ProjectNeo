using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>D-3: Game 씬 주점에서 영웅을 대장·대기로 고용하고, 대장은 부대 지시를 따르며 대기 영웅은 전투에서 빠진다.</summary>
    public class GameTavernPlayTests
    {
        static int cleanup;
        GameFacilities facilities;
        GameEquipment equipment;
        SquadCommandCenter center;
        GameBattleController battle;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; yield return null;
            facilities = Object.FindAnyObjectByType<GameFacilities>();
            equipment = Object.FindAnyObjectByType<GameEquipment>();
            center = Object.FindAnyObjectByType<SquadCommandCenter>();
            battle = Object.FindAnyObjectByType<GameBattleController>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameTavernCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        FacilityStation Station(FacilityType type) =>
            Object.FindObjectsByType<FacilityStation>().Single(station => station.Type == type);

        [UnityTest]
        public IEnumerator 주점에서_대장과_대기_영웅을_고용하고_대기_영웅은_전투에서_빠진다()
        {
            RunState state = GameSession.Flow.Run.State;
            state.Wallet.AddGold(200);
            Assert.IsTrue(Station(FacilityType.Tavern).Interact());
            Assert.AreEqual(2, state.TavernOffers.Count, "주점 1레벨 영웅 풀(검사·궁수)");
            Assert.AreEqual(2, facilities.OfferRows);
            StringAssert.Contains("고용", facilities.OfferHeaderText);

            int gold = state.Wallet.Gold;
            var captainHero = state.TavernOffers[0];
            Assert.IsTrue(facilities.ChooseCaptainSquad(0));
            Assert.IsTrue(facilities.TryHire(0));
            Assert.AreEqual(gold - captainHero.HireCost.gold, state.Wallet.Gold);
            RunHero captain = state.Heroes.Single(hero => hero.Definition == captainHero);
            Assert.AreSame(captain, state.Army.Squads[0].Leader);
            UnitCombatAI captainUnit = center.Squads[0].Leader;
            Assert.IsNotNull(captainUnit, "씬 부대장도 바뀐다.");
            Assert.AreSame(captain.Character, captainUnit.GetComponent<CharacterActor>().Character, "런과 씬이 같은 캐릭터를 쓴다.");
            StringAssert.Contains("대장", facilities.BodyText);

            var reserveHero = state.TavernOffers[0];
            Assert.IsTrue(facilities.ChooseCaptainSquad(-1));
            Assert.IsTrue(facilities.TryHire(0));
            RunHero reserve = state.Heroes.Single(hero => hero.Definition == reserveHero);
            CollectionAssert.AreEqual(new[] { reserve }, state.ReserveHeroes);
            CharacterActor reserveActor = Object.FindObjectsByType<CharacterActor>()
                .Single(actor => actor.Character == reserve.Character);
            Assert.AreEqual(1, facilities.OfferRows, "매물이 비면 안내 한 줄");
            facilities.Close();
            yield return null;

            Assert.IsTrue(equipment.Open());
            Assert.AreSame(state.Player, equipment.SelectedHero);
            Assert.IsTrue(equipment.NextHero());
            Assert.AreSame(captain, equipment.SelectedHero, "장비 화면에서 고용 영웅을 고른다.");
            equipment.Close();
            yield return null;

            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            Assert.IsFalse(reserveActor.gameObject.activeSelf, "대기 영웅은 전투에 나가지 않는다.");
            Assert.IsTrue(captainUnit.gameObject.activeSelf);
            Assert.AreSame(captain, battle.RunLink.CurrentInput.Squads[0].Leader);
        }
    }
}
