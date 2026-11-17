using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>D-4: Game 씬 병영 팝업에서 잡졸을 모집(비용·부대 선택)하고, 업그레이드한 훈련 보너스가 모든 잡졸에게 붙는다(Q-37).</summary>
    public class GameBarracksPlayTests
    {
        static int cleanup;
        GameBattleController battle;
        GameFacilities facilities;
        SquadCommandCenter center;
        ConstructionWorld world;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            battle = Object.FindAnyObjectByType<GameBattleController>();
            facilities = Object.FindAnyObjectByType<GameFacilities>();
            center = Object.FindAnyObjectByType<SquadCommandCenter>();
            world = Object.FindAnyObjectByType<ConstructionWorld>();
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (facilities != null && facilities.IsOpen) facilities.Close();
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameBarracksCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        [UnityTest]
        public IEnumerator 병영에서_비용을_내고_고른_부대로_잡졸을_모집한다()
        {
            RunState state = GameSession.Flow.Run.State;
            Assert.IsTrue(facilities.Open(FacilityType.TrainingGround));
            yield return null;
            Assert.AreEqual(2, state.RecruitableSoldiers.Count, "1레벨부터 근접·원거리 모두 모집한다.");
            Assert.AreEqual(2, facilities.OfferRows);
            StringAssert.StartsWith("Recruit", facilities.OfferHeaderText);
            Assert.AreEqual(-1, facilities.ChosenSquad, "처음에는 자동 배치다.");
            StringAssert.Contains("Training bonus", facilities.BodyText);
            Vector3 barracks = Object.FindObjectsByType<FacilityStation>()
                .First(station => station.Type == FacilityType.TrainingGround).transform.position;

            int gold = state.Wallet.Gold;
            int first = center.Squads[0].Members.Count;
            Assert.IsTrue(facilities.TryRecruit(0));
            Assert.AreEqual(gold - 15, state.Wallet.Gold, "근접 잡졸은 15골드.");
            Assert.AreEqual(first + 1, center.Squads[0].Members.Count, "자동 배치는 자리가 남은 첫 부대.");
            UnitCombatAI recruit = center.Squads[0].Members.Last();
            Assert.Less(Flat(recruit.transform.position, barracks), 12f, "병영 앞에 나타난다.");
            StringAssert.Contains("Squad 1", facilities.Message);

            Assert.IsTrue(facilities.ChooseSquad(1));
            Assert.AreEqual(1, facilities.ChosenSquad);
            int second = center.Squads[1].Members.Count;
            Assert.IsTrue(facilities.TryRecruit(1));
            Assert.AreEqual(gold - 35, state.Wallet.Gold, "원거리 잡졸은 20골드.");
            Assert.AreEqual(second + 1, center.Squads[1].Members.Count, "고른 부대에 들어간다.");
            Assert.AreSame(state.RecruitableSoldiers[1], center.Squads[1].Members.Last().Actor.SoldierDefinition);

            Assert.IsTrue(state.Wallet.TrySpend(new Cost(state.Wallet.Gold - 10)));
            int soldiers = state.Soldiers.Count;
            Assert.IsFalse(facilities.TryRecruit(0));
            StringAssert.Contains("Not enough funds", facilities.Message);
            Assert.AreEqual(10, state.Wallet.Gold);
            Assert.AreEqual(soldiers, state.Soldiers.Count, "돈이 모자라면 런·씬 모두 바뀌지 않는다.");

            facilities.Close();
            yield return null;
            Assert.IsTrue(battle.TryStartBattle());
            Assert.IsTrue(battle.RunLink.CurrentInput.Squads[1].Members.Any(member => member.Id == battle.RunLink.IdOf(center.Squads[1].Members.Last().Actor)),
                "모집한 잡졸이 다음 전투 입력에 들어간다.");
        }

        [UnityTest]
        public IEnumerator 병영_업그레이드_훈련_보너스는_기존과_새_잡졸_모두에게_바로_붙는다()
        {
            RunState state = GameSession.Flow.Run.State;
            CharacterActor veteran = center.Squads[0].Members[0].Actor;
            CharacterActor player = Object.FindObjectsByType<CharacterActor>().First(actor => battle.RunLink.IdOf(actor) == state.Player.Id);
            float baseHealth = veteran.Stats.GetBase(StatType.MaxHealth);
            float baseAttack = veteran.Stats.GetBase(StatType.Attack);
            float playerHealth = player.Stats.GetValue(StatType.MaxHealth);
            Assert.AreEqual(baseHealth, veteran.Health.Maximum, 0.001f, "1레벨은 보너스가 없다.");

            Assert.IsTrue(facilities.Open(FacilityType.TrainingGround));
            yield return null;
            Assert.IsTrue(facilities.TryUpgrade(), "시작 금화로 2레벨 업그레이드(60골드).");
            Assert.AreEqual(baseHealth * 1.1f, veteran.Health.Maximum, 0.01f, "기존 잡졸 체력 +10%.");
            Assert.AreEqual(veteran.Health.Maximum, veteran.Health.Current, 0.01f, "가득 찼던 체력은 늘어난 최대치까지 찬다.");
            Assert.AreEqual(baseAttack * 1.1f, veteran.Stats.GetValue(StatType.Attack), 0.01f, "기존 잡졸 공격력 +10%.");
            Assert.AreEqual(playerHealth, player.Stats.GetValue(StatType.MaxHealth), 0.001f, "플레이어에게는 붙지 않는다.");

            Assert.IsTrue(facilities.TryRecruit(0));
            CharacterActor recruit = center.Squads[0].Members.Last().Actor;
            Assert.AreNotSame(veteran, recruit);
            Assert.AreEqual(recruit.Stats.GetBase(StatType.MaxHealth) * 1.1f, recruit.Health.Maximum, 0.01f, "새 잡졸도 보너스를 받는다.");
            Assert.AreEqual(recruit.Health.Maximum, recruit.Health.Current, 0.01f);

            facilities.Close();
            yield return null;
            battle.RunLink.OnPreparation();
            Assert.AreEqual(baseHealth * 1.1f, veteran.Health.Maximum, 0.01f, "다시 적용해도 겹치지 않는다.");
        }
    }
}
