using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Siege;
using ProjectNeo.Stages;
using ProjectNeo.Stats;
using ProjectNeo.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// FLOW-03 승천 단계(Q-78)를 실제 Game 씬에서 본다: 고른 단계의 규칙이 시작 골드·준비 시계·적 편성·적 스탯·부대 사기에 붙고,
    /// 마지막 스테이지의 보상 뒤 'Finish run'으로 엔딩에 가면 다음 단계가 열린다.
    /// </summary>
    public class GameAscensionPlayTests
    {
        static int cleanup;
        GameBattleController battle;
        float capture;

        IEnumerator LoadGame(System.Action startFlow)
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            // 게임 씬을 열기 전에 흐름을 시작해 두면 GameDirector가 새 런(단계 0)을 다시 시작하지 않는다.
            startFlow();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            battle = Object.FindAnyObjectByType<GameBattleController>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SingleWaveEncounterFixture.UseSingleWave();
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameAscensionCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        [UnityTest]
        public IEnumerator 승천_10으로_시작하면_시작_골드_준비_시간_적_수_적_스탯_사기에_규칙이_붙는다()
        {
            GameBattleController.EncounterSourceOverride = null;
            yield return LoadGame(() => GameSession.Flow.StartRun(null, 10));
            RunState state = battle.RunLink.State;
            Assert.IsNotNull(state);
            Assert.AreEqual(10, state.AscensionLevel);
            AscensionEffects effects = state.Ascension;
            Assert.AreEqual(7, GameSession.Flow.Run.StageCount, "런 길이는 스테이지 목록에서 온다.");

            RunConfig start = battle.GetComponent<GameBuildEconomy>().StartConfig;
            Assert.AreEqual(effects.StartingGold(start.StartingGold), state.Wallet.Gold, "시작 골드 -25%");
            Assert.AreEqual(start.PrepTime.BudgetHours - 2f, state.Clock.Remaining, 0.0001f, "준비 시간 -2시간");

            EncounterPlan stageOne = EncounterPlan.FromStage(battle.Settings.Stages.StageFor(1), battle.Settings.SpawnCell);
            Assert.AreEqual(stageOne.WithCounts(effects.EnemyCount).TotalUnits, battle.Enemies.Count, "웨이브마다 적 수 +20%(올림)");
            Assert.Greater(battle.Enemies.Count, stageOne.TotalUnits);

            Assert.AreEqual(120f, battle.Morale.MoraleOf(0).Value, 0.0001f, "새 부대의 시작 사기 150 -> 120");

            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            SiegeAttacker released = battle.Enemies.First(enemy => enemy != null && enemy.gameObject.activeSelf);
            StatSheet stats = released.Actor.Stats;
            Assert.AreEqual(stats.GetBase(StatType.MaxHealth) * 1.15f, stats.GetValue(StatType.MaxHealth), 0.01f, "적 체력 +15%");
            Assert.AreEqual(stats.GetBase(StatType.Attack) * 1.15f, stats.GetValue(StatType.Attack), 0.01f, "적 공격력 +15%");
            Assert.AreEqual(stats.GetBase(StatType.MoveSpeed) * 1.15f, stats.GetValue(StatType.MoveSpeed), 0.01f, "적 이동 속도 +15%");
            Assert.AreEqual(released.Actor.Health.Maximum, released.Actor.Health.Current, 0.01f, "늘어난 체력까지 채워서 나온다.");
        }

        [UnityTest]
        public IEnumerator 마지막_스테이지_보상_뒤_Finish_run으로_엔딩에_가면_다음_승천_단계가_열린다()
        {
            yield return LoadGame(() => GameSession.Flow.ResumeRun(7));
            Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase);
            Assert.AreEqual(7, GameSession.Flow.Run.StageNumber);
            Assert.IsTrue(GameSession.Flow.Run.IsFinalStage);
            Assert.AreEqual(0, GameSession.Meta.ascensionUnlocked);

            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            Assert.IsTrue(GameSession.Flow.FinishBattle(BattleOutcome.Victory));
            yield return null;

            var hud = Object.FindAnyObjectByType<GameFlowHud>();
            Transform panel = hud.transform.Find("Panel");
            GameObject finish = panel.Find("Button_Finish run").gameObject;
            Assert.IsTrue(finish.activeSelf, "마지막 스테이지 보상에서는 Finish run 버튼이 보인다.");
            Assert.IsFalse(GameSession.Flow.ContinueToNextStage(), "다음 스테이지로는 갈 수 없다.");

            finish.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(GamePhase.Ending, GameSession.Flow.Phase);
            Assert.AreEqual(1, GameSession.Meta.ascensionUnlocked, "기본 게임을 깨면 승천 1이 열린다.");
            Assert.AreEqual(1, battle.AscensionUnlockedAtEnding);
            Assert.IsFalse(finish.activeSelf);
            StringAssert.Contains("Ascension 1 unlocked", hud.StatusText);
            // 엔딩도 런의 끝이라 런 종료 해금 포인트(META-08)를 준다: 마지막 스테이지까지 7개를 깬 것으로 센다.
            var rewards = battle.GetComponent<GameRewardController>();
            Assert.AreEqual(7, rewards.RunEndStagesCleared);
            Assert.AreEqual(rewards.Table.RunEndUnlockPoints(7, 0), rewards.RunEndUnlockPoints, "기본 게임은 배율 x1");
            Assert.AreEqual(rewards.RunEndUnlockPoints, GameSession.Meta.unlockPoints);
            StringAssert.Contains("Unlock points +", hud.StatusText);
            StringAssert.Contains("Run complete", hud.PhaseText);
        }
    }
}
