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

        /// <summary>이 런의 승천 단계(FLOW-03). 0이 기본 게임이고 런 동안 바뀌지 않는다.</summary>
        public int AscensionLevel { get; }

        /// <summary>
        /// 런의 스테이지 수. 게임 씬이 스테이지 목록을 보고 알려 준다. 0이면 모르는 것이고, 이때는 마지막 스테이지가 없다.
        /// </summary>
        public int StageCount { get; private set; }

        /// <summary>지금 스테이지가 런의 마지막 스테이지인지. 이 스테이지의 보상 뒤에는 엔딩으로 간다(FLOW-03).</summary>
        public bool IsFinalStage => StageCount > 0 && StageNumber >= StageCount;

        /// <summary>
        /// 런이 소유하는 지갑·영웅·잡졸·부대·성 기록. 타이틀에서 시작하면 게임 씬이 씬 편성으로 만들어 붙이며,
        /// 붙기 전에는 null이다.
        /// </summary>
        public RunState State { get; private set; }

        internal RunProgress(RunState state = null, int stageNumber = 1, int ascensionLevel = 0)
        {
            State = state;
            StageNumber = stageNumber < 1 ? 1 : stageNumber;
            AscensionLevel = ascensionLevel < 0 ? 0 : ascensionLevel;
        }

        internal void Attach(RunState state) => State = state;

        internal void AdvanceStage() => StageNumber++;

        internal void SetStageCount(int count) => StageCount = count < 0 ? 0 : count;
    }
}
