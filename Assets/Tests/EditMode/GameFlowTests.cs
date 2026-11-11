using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Flow;

namespace ProjectNeo.Tests
{
    public class GameFlowTests
    {
        [Test]
        public void 처음은_타이틀이고_런이_없다()
        {
            var flow = new GameFlow();
            Assert.AreEqual(GamePhase.Title, flow.Phase);
            Assert.IsNull(flow.Run);
            Assert.IsNull(flow.LastOutcome);
        }

        [Test]
        public void 새_런은_1스테이지_준비에서_시작한다()
        {
            var flow = new GameFlow();
            Assert.IsTrue(flow.StartRun());
            Assert.AreEqual(GamePhase.Preparation, flow.Phase);
            Assert.AreEqual(1, flow.Run.StageNumber);
        }

        [Test]
        public void 승리하면_보상을_거쳐_다음_스테이지_준비로_간다()
        {
            var flow = new GameFlow();
            flow.StartRun();
            Assert.IsTrue(flow.StartBattle());
            Assert.AreEqual(GamePhase.Battle, flow.Phase);
            Assert.IsTrue(flow.FinishBattle(BattleOutcome.Victory));
            Assert.AreEqual(GamePhase.Reward, flow.Phase);
            Assert.AreEqual(BattleOutcome.Victory, flow.LastOutcome);
            Assert.AreEqual(1, flow.Run.StageNumber, "보상 중에는 아직 끝낸 스테이지다.");
            Assert.IsTrue(flow.ContinueToNextStage());
            Assert.AreEqual(GamePhase.Preparation, flow.Phase);
            Assert.AreEqual(2, flow.Run.StageNumber);
        }

        [Test]
        public void 패배하면_런_결과를_거쳐_타이틀로_돌아가고_런을_버린다()
        {
            var flow = new GameFlow();
            flow.StartRun();
            flow.StartBattle();
            Assert.IsTrue(flow.FinishBattle(BattleOutcome.Defeat));
            Assert.AreEqual(GamePhase.RunResult, flow.Phase);
            Assert.IsNotNull(flow.Run, "결과 화면에서 도달 스테이지를 보여줄 수 있어야 한다.");
            Assert.IsTrue(flow.ReturnToTitle());
            Assert.AreEqual(GamePhase.Title, flow.Phase);
            Assert.IsNull(flow.Run);
            Assert.IsNull(flow.LastOutcome);
        }

        [Test]
        public void 다시_시작한_런은_이전_런의_진행을_이어받지_않는다()
        {
            var flow = new GameFlow();
            flow.StartRun();
            flow.StartBattle();
            flow.FinishBattle(BattleOutcome.Victory);
            flow.ContinueToNextStage();
            flow.StartBattle();
            flow.FinishBattle(BattleOutcome.Defeat);
            flow.ReturnToTitle();
            flow.StartRun();
            Assert.AreEqual(1, flow.Run.StageNumber);
            Assert.IsNull(flow.LastOutcome);
        }

        [Test]
        public void 허용되지_않은_전환은_거부하고_상태를_바꾸지_않는다()
        {
            var flow = new GameFlow();
            var changes = new List<GamePhase>();
            flow.PhaseChanged += changes.Add;
            Assert.IsFalse(flow.StartBattle());
            Assert.IsFalse(flow.FinishBattle(BattleOutcome.Victory));
            Assert.IsFalse(flow.ContinueToNextStage());
            Assert.IsFalse(flow.ReturnToTitle());
            Assert.AreEqual(GamePhase.Title, flow.Phase);
            flow.StartRun();
            Assert.IsFalse(flow.StartRun(), "런 중에는 새 런을 시작할 수 없다.");
            Assert.IsFalse(flow.FinishBattle(BattleOutcome.Defeat), "준비 중에는 전투 결과를 받지 않는다.");
            Assert.IsFalse(flow.ContinueToNextStage());
            Assert.AreEqual(GamePhase.Preparation, flow.Phase);
            Assert.AreEqual(1, flow.Run.StageNumber);
            CollectionAssert.AreEqual(new[] { GamePhase.Preparation }, changes);
        }

        [Test]
        public void 전투_결과는_한_번만_받는다()
        {
            var flow = new GameFlow();
            flow.StartRun();
            flow.StartBattle();
            Assert.IsTrue(flow.FinishBattle(BattleOutcome.Victory));
            Assert.IsFalse(flow.FinishBattle(BattleOutcome.Defeat));
            Assert.AreEqual(GamePhase.Reward, flow.Phase);
            Assert.AreEqual(BattleOutcome.Victory, flow.LastOutcome);
        }

        [Test]
        public void 단계가_바뀔_때마다_새_단계를_한_번씩_알린다()
        {
            var flow = new GameFlow();
            var changes = new List<GamePhase>();
            flow.PhaseChanged += changes.Add;
            flow.StartRun();
            flow.StartBattle();
            flow.FinishBattle(BattleOutcome.Victory);
            flow.ContinueToNextStage();
            flow.StartBattle();
            flow.FinishBattle(BattleOutcome.Defeat);
            flow.ReturnToTitle();
            CollectionAssert.AreEqual(new[]
            {
                GamePhase.Preparation, GamePhase.Battle, GamePhase.Reward, GamePhase.Preparation,
                GamePhase.Battle, GamePhase.RunResult, GamePhase.Title,
            }, changes);
        }
    }
}
