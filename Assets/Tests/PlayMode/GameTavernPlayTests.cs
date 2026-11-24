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
    /// <summary>D-3·UI-13: Game 씬 주점에서 고용한 영웅은 모두 대기 영웅이 되고, 편성 창에서 대장으로 정한 영웅만 전투에 나간다.</summary>
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
        public IEnumerator 주점에서_고용한_영웅은_대기로_들어가고_편성에서_대장이_되며_대기_영웅은_전투에서_빠진다()
        {
            RunState state = GameSession.Flow.Run.State;
            state.Wallet.AddGold(200);
            Assert.IsTrue(Station(FacilityType.Tavern).Interact());
            Assert.AreEqual(2, state.TavernOffers.Count, "주점 1레벨 영웅 풀(검사·궁수)");
            Assert.AreEqual(2, facilities.OfferRows);
            StringAssert.Contains("고용", facilities.OfferHeaderText);

            // 타일을 고르면 상세에 보이기만 하고, 상세의 고용 단추가 고용한다(UI-13 C안).
            int gold = state.Wallet.Gold;
            var captainHero = state.TavernOffers[0];
            Assert.IsTrue(facilities.Select(0));
            Assert.AreEqual(captainHero.DisplayName, facilities.DetailTitleText);
            Assert.AreEqual(gold, state.Wallet.Gold, "고르기만 해서는 고용하지 않는다.");
            Assert.AreEqual("고용", facilities.ActionText);
            Assert.IsTrue(facilities.ActionButton.gameObject.activeSelf);
            facilities.ActionButton.onClick.Invoke();
            Assert.AreEqual(gold - captainHero.HireCost.gold, state.Wallet.Gold);
            RunHero captain = state.Heroes.Single(hero => hero.Definition == captainHero);
            Assert.IsNull(state.Army.Squads[0].Leader, "주점에서는 대장을 정하지 않는다.");
            CollectionAssert.Contains(state.ReserveHeroes, captain);
            StringAssert.Contains("예비", facilities.BodyText);
            Assert.AreEqual(-1, facilities.SelectedIndex, "고용한 영웅은 목록에서 빠지고 선택이 풀린다.");

            var reserveHero = state.TavernOffers[0];
            Assert.IsTrue(facilities.TryHire(0));
            RunHero reserve = state.Heroes.Single(hero => hero.Definition == reserveHero);
            CollectionAssert.AreEquivalent(new[] { captain, reserve }, state.ReserveHeroes);
            CharacterActor reserveActor = Object.FindObjectsByType<CharacterActor>()
                .Single(actor => actor.Character == reserve.Character);
            Assert.AreEqual(0, facilities.OfferRows, "매물이 비면 타일이 없다.");
            facilities.Close();
            yield return null;

            // 대장은 편성 창(O)에서 정한다.
            var organization = Object.FindAnyObjectByType<GameSquadOrganization>();
            Assert.IsTrue(organization.Open());
            Assert.IsTrue(organization.SelectSquad(0));
            Assert.IsTrue(organization.Assign(captain));
            organization.Close();
            yield return null;
            Assert.AreSame(captain, state.Army.Squads[0].Leader);
            CollectionAssert.AreEqual(new[] { reserve }, state.ReserveHeroes);
            UnitCombatAI captainUnit = center.Squads[0].Leader;
            Assert.IsNotNull(captainUnit, "씬 부대장도 바뀐다.");
            Assert.AreSame(captain.Character, captainUnit.GetComponent<CharacterActor>().Character, "런과 씬이 같은 캐릭터를 쓴다.");

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
