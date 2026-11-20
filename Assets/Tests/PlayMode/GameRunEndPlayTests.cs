using System.Collections;
using NUnit.Framework;
using ProjectNeo.Flow;
using ProjectNeo.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>META-08: 승리마다 해금 포인트를 주지 않고, 패배로 런이 끝날 때 클리어한 스테이지만큼 한 번 준다.</summary>
    public class GameRunEndPlayTests
    {
        static int cleanup;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameRunEndCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset();
        }

        static IEnumerator Fight(BattleOutcome outcome)
        {
            GameFlow flow = GameSession.Flow;
            Assert.IsTrue(flow.StartBattle());
            yield return null;
            Assert.IsTrue(flow.FinishBattle(outcome));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Defeat_GrantsOnePointPerStageClearedOnce()
        {
            GameFlow flow = GameSession.Flow;
            var rewards = Object.FindAnyObjectByType<GameRewardController>();
            for (int stage = 1; stage <= 2; stage++)
            {
                yield return Fight(BattleOutcome.Victory);
                Assert.AreEqual(0, GameSession.Meta.unlockPoints, "승리 보상에는 해금 포인트가 없다");
                Assert.IsTrue(flow.ContinueToNextStage());
                yield return null;
            }
            Assert.AreEqual(3, flow.Run.StageNumber);

            yield return Fight(BattleOutcome.Defeat);

            Assert.AreEqual(GamePhase.RunResult, flow.Phase);
            Assert.AreEqual(2, rewards.RunEndStagesCleared);
            Assert.AreEqual(2, rewards.RunEndUnlockPoints, "스테이지당 1점(StageRewards.asset)");
            Assert.AreEqual(2, GameSession.Meta.unlockPoints);
            StringAssert.StartsWith("클리어한 스테이지 2\n해금 포인트 +2 (합계 2)", rewards.DescribeRunEnd());
            // 런 점수(META-04): 한 번 계산되고 슬롯 최고 점수가 된다.
            Assert.IsNotNull(rewards.LastScore);
            Assert.Greater(rewards.LastScore.Total, 0, "남은 골드와 생존 잡졸만으로도 점수가 있다");
            Assert.IsTrue(rewards.IsNewBest);
            Assert.AreEqual(rewards.LastScore.Total, GameSession.Meta.bestRunScore);
            StringAssert.Contains($"런 점수 {ProjectNeo.Run.RunScore.Number(rewards.LastScore.Total)} - 최고 기록!", rewards.DescribeRunEnd());
            Assert.IsFalse(rewards.TryGrantRunEnd(), "한 런에 한 번만 준다");
            Assert.AreEqual(2, GameSession.Meta.unlockPoints);
            // 패배 화면(META-04 수동 확인 대체): HUD 전투 상태 칸에 실제로 점수와 최고 기록 표시가 보인다.
            var hud = Object.FindAnyObjectByType<GameFlowHud>();
            StringAssert.Contains($"런 점수 {ProjectNeo.Run.RunScore.Number(rewards.LastScore.Total)} - 최고 기록!", hud.StatusText);
            StringAssert.Contains(rewards.LastScore.Breakdown(), hud.StatusText, "점수 내역도 함께 보인다");
        }

        [UnityTest]
        public IEnumerator Defeat_BelowBest_KeepsAndShowsTheSavedBest()
        {
            const int best = 9999999;
            GameSession.Meta.bestRunScore = best;
            var rewards = Object.FindAnyObjectByType<GameRewardController>();
            yield return Fight(BattleOutcome.Victory);
            Assert.IsTrue(GameSession.Flow.ContinueToNextStage());
            yield return null;

            yield return Fight(BattleOutcome.Defeat);

            Assert.AreEqual(GamePhase.RunResult, GameSession.Flow.Phase);
            Assert.IsNotNull(rewards.LastScore);
            Assert.Less(rewards.LastScore.Total, best);
            Assert.IsFalse(rewards.IsNewBest);
            Assert.AreEqual(best, GameSession.Meta.bestRunScore, "더 낮은 점수는 최고 점수를 바꾸지 않는다");
            var hud = Object.FindAnyObjectByType<GameFlowHud>();
            StringAssert.Contains($"런 점수 {ProjectNeo.Run.RunScore.Number(rewards.LastScore.Total)} (최고 9,999,999)", hud.StatusText);
            StringAssert.DoesNotContain("최고 기록!", hud.StatusText);
        }

        [UnityTest]
        public IEnumerator DefeatOnFirstStage_GrantsNothing()
        {
            var rewards = Object.FindAnyObjectByType<GameRewardController>();

            yield return Fight(BattleOutcome.Defeat);

            Assert.AreEqual(0, rewards.RunEndUnlockPoints);
            Assert.AreEqual(0, GameSession.Meta.unlockPoints);
        }
    }
}
