namespace ProjectNeo.Flow
{
    /// <summary>
    /// 런 하나가 살아 있는 동안만 유지되는 진행 정보. 새 런에서 새로 만들고 타이틀로 돌아가면 버린다.
    /// 골드·영웅·성 배치 같은 런 데이터는 C-1에서 이 수명에 맞춰 붙인다.
    /// </summary>
    public sealed class RunProgress
    {
        /// <summary>지금 준비하거나 싸우는 스테이지. 1부터 센다.</summary>
        public int StageNumber { get; private set; } = 1;

        internal void AdvanceStage() => StageNumber++;
    }
}
