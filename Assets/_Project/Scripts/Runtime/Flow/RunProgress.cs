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
        /// 런이 소유하는 지갑·영웅·잡졸·부대·성 기록. 타이틀에서 시작하면 게임 씬이 씬 편성으로 만들어 붙이며,
        /// 붙기 전에는 null이다.
        /// </summary>
        public RunState State { get; private set; }

        internal RunProgress(RunState state = null, int stageNumber = 1)
        {
            State = state;
            StageNumber = stageNumber < 1 ? 1 : stageNumber;
        }

        internal void Attach(RunState state) => State = state;

        internal void AdvanceStage() => StageNumber++;
    }
}
