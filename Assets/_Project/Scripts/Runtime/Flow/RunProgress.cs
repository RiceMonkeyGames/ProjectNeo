using ProjectNeo.Run;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 런 하나가 살아 있는 동안만 유지되는 진행 정보. 새 런에서 새로 만들고 타이틀로 돌아가면 버린다.
    /// </summary>
    public sealed class RunProgress
    {
        /// <summary>지금 준비하거나 싸우는 스테이지. 1부터 센다.</summary>
        public int StageNumber { get; private set; } = 1;

        /// <summary>
        /// 런이 소유하는 지갑·영웅·잡졸·부대·성 기록. 게임 씬에 아직 연결하지 않아 지금은 null일 수 있다(C-1~2에서 연결).
        /// </summary>
        public RunState State { get; }

        internal RunProgress(RunState state = null) => State = state;

        internal void AdvanceStage() => StageNumber++;
    }
}
