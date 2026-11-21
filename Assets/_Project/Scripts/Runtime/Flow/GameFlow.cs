using System;
using ProjectNeo.Run;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 타이틀 → 준비 → 전투 → (승리) 보상 → 다음 준비(마지막 스테이지면 엔딩), (패배) 런 결과 → 타이틀의 단계 전환 규칙.
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
        public bool StartRun(RunState state = null, int ascensionLevel = 0)
        {
            ReturnedFromRunEnd = false;
            return StartRunCore(state, ascensionLevel);
        }

        bool StartRunCore(RunState state, int ascensionLevel)
        {
            if (Phase != GamePhase.Title) return false;
            Run = new RunProgress(state, 1, ascensionLevel);
            LastOutcome = null;
            Enter(GamePhase.Preparation);
            return true;
        }

        /// <summary>
        /// 타이틀에서 저장된 런을 이어 한다(FLOW-02). 저장된 스테이지의 준비 단계로 들어간다.
        /// 런 데이터는 게임 씬이 저장 기록으로 되살려 붙인다.
        /// </summary>
        public bool ResumeRun(int stageNumber, int ascensionLevel = 0)
        {
            if (Phase != GamePhase.Title) return false;
            Run = new RunProgress(null, stageNumber, ascensionLevel);
            LastOutcome = null;
            Enter(GamePhase.Preparation);
            return true;
        }

        /// <summary>
        /// 이어 하려던 저장을 되살리지 못했을 때, 아직 런 데이터가 붙지 않은 런을 1스테이지의 새 런으로 바꾼다.
        /// 단계는 그대로(준비)이고 단계 알림은 보내지 않는다. 런 데이터가 이미 있으면 false.
        /// </summary>
        public bool RestartRunAtFirstStage()
        {
            if (Run == null || Run.State != null) return false;
            Run = new RunProgress(null, 1, Run.AscensionLevel);
            return true;
        }

        /// <summary>
        /// 런 데이터 없이 시작한 런에 데이터를 붙인다. 런이 없거나 이미 데이터가 있으면 false이고 바꾸지 않는다.
        /// 타이틀 씬은 게임 씬의 편성을 모르므로 게임 씬이 첫 준비 때 붙인다.
        /// </summary>
        public bool AttachRunState(RunState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (Run == null || Run.State != null) return false;
            Run.Attach(state);
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

        /// <summary>
        /// 런의 스테이지 수를 알려 준다(FLOW-03). 마지막 스테이지의 보상 뒤에는 다음 스테이지 대신 엔딩으로 간다.
        /// 런이 없으면 false. 0이면 마지막 스테이지가 없다.
        /// </summary>
        public bool SetStageCount(int count)
        {
            if (Run == null) return false;
            Run.SetStageCount(count);
            return true;
        }

        /// <summary>보상을 마치고 다음 스테이지 준비로 간다. 마지막 스테이지면 false이고 <see cref="FinishRun"/>을 쓴다.</summary>
        public bool ContinueToNextStage()
        {
            if (Phase != GamePhase.Reward || Run.IsFinalStage) return false;
            Run.AdvanceStage();
            Enter(GamePhase.Preparation);
            return true;
        }

        /// <summary>마지막 스테이지의 보상을 마치고 엔딩으로 간다(FLOW-03). 보상 단계의 마지막 스테이지가 아니면 false.</summary>
        public bool FinishRun()
        {
            if (Phase != GamePhase.Reward || !Run.IsFinalStage) return false;
            Enter(GamePhase.Ending);
            return true;
        }

        /// <summary>
        /// 마지막으로 타이틀에 돌아온 것이 런 결과(패배)나 엔딩 뒤였는지(UI-05). 타이틀은 이때 메인 화면 대신
        /// 같은 슬롯의 출정 준비 화면을 바로 연다. <see cref="TakeReturnedFromRunEnd"/>로 한 번만 읽는다.
        /// </summary>
        public bool ReturnedFromRunEnd { get; private set; }

        /// <summary>런 종료 뒤 복귀 표시를 읽고 지운다.</summary>
        public bool TakeReturnedFromRunEnd()
        {
            bool value = ReturnedFromRunEnd;
            ReturnedFromRunEnd = false;
            return value;
        }

        /// <summary>런을 끝내고 타이틀로 돌아간다. 런 진행 정보는 버린다.</summary>
        public bool ReturnToTitle()
        {
            if (Phase == GamePhase.Title) return false;
            ReturnedFromRunEnd = Phase == GamePhase.RunResult || Phase == GamePhase.Ending;
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
