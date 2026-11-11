using System;
using ProjectNeo.Run;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 타이틀 → 준비 → 전투 → (승리) 보상 → 다음 준비, (패배) 런 결과 → 타이틀의 단계 전환 규칙.
    /// 씬과 화면을 알지 않는다. 허용되지 않은 전환은 false를 돌려주고 상태를 바꾸지 않는다.
    /// </summary>
    public sealed class GameFlow
    {
        public GamePhase Phase { get; private set; } = GamePhase.Title;

        /// <summary>진행 중인 런. 타이틀에서는 null이다.</summary>
        public RunProgress Run { get; private set; }

        /// <summary>이번 런에서 마지막으로 끝난 전투의 결과. 아직 없으면 null이다.</summary>
        public BattleOutcome? LastOutcome { get; private set; }

        /// <summary>단계가 바뀐 뒤 새 단계와 함께 한 번 알린다.</summary>
        public event Action<GamePhase> PhaseChanged;

        /// <summary>타이틀에서 새 런을 시작해 1스테이지 준비로 간다. <paramref name="state"/>는 이 런이 소유할 데이터다.</summary>
        public bool StartRun(RunState state = null)
        {
            if (Phase != GamePhase.Title) return false;
            Run = new RunProgress(state);
            LastOutcome = null;
            Enter(GamePhase.Preparation);
            return true;
        }

        /// <summary>준비를 끝내고 전투를 시작한다.</summary>
        public bool StartBattle()
        {
            if (Phase != GamePhase.Preparation) return false;
            Enter(GamePhase.Battle);
            return true;
        }

        /// <summary>전투 결과를 받는다. 승리는 보상, 패배는 런 결과로 간다. 전투 중이 아니면 무시한다.</summary>
        public bool FinishBattle(BattleOutcome outcome)
        {
            if (Phase != GamePhase.Battle) return false;
            LastOutcome = outcome;
            Enter(outcome == BattleOutcome.Victory ? GamePhase.Reward : GamePhase.RunResult);
            return true;
        }

        /// <summary>
        /// 상세 전투 결과를 런 데이터에 반영하고 단계를 넘긴다. 전투 중이 아니거나 런 데이터가 결과를 받지 못하면
        /// (다른 전투의 결과, 이미 반영한 결과, 모르는 유닛) 아무것도 바꾸지 않고 false다.
        /// </summary>
        public bool FinishBattle(BattleResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (Phase != GamePhase.Battle) return false;
            if (Run.State != null && Run.State.Apply(result) != BattleResultIssue.None) return false;
            return FinishBattle(result.Outcome);
        }

        /// <summary>보상을 마치고 다음 스테이지 준비로 간다.</summary>
        public bool ContinueToNextStage()
        {
            if (Phase != GamePhase.Reward) return false;
            Run.AdvanceStage();
            Enter(GamePhase.Preparation);
            return true;
        }

        /// <summary>런을 끝내고 타이틀로 돌아간다. 런 진행 정보는 버린다.</summary>
        public bool ReturnToTitle()
        {
            if (Phase == GamePhase.Title) return false;
            Run = null;
            LastOutcome = null;
            Enter(GamePhase.Title);
            return true;
        }

        void Enter(GamePhase next)
        {
            Phase = next;
            PhaseChanged?.Invoke(next);
        }
    }
}
