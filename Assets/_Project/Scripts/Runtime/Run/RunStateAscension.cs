namespace ProjectNeo.Run
{
    /// <summary>이 런의 승천 단계(FLOW-03, Q-78). 런 동안 바뀌지 않고 런 저장에 함께 남는다.</summary>
    public sealed partial class RunState
    {
        /// <summary>이 런을 시작한 승천 단계(0 = 기본 게임).</summary>
        public int AscensionLevel { get; private set; }
        /// <summary>단계까지 쌓인 규칙. 설정 전이나 단계 0이면 아무것도 바꾸지 않는다.</summary>
        public AscensionEffects Ascension { get; private set; } = AscensionEffects.None;

        /// <summary>
        /// 승천 단계와 표를 정한다. 단계는 표 범위로 맞춘다(표가 없으면 0). 준비 시작 때 시작 골드·준비 시계보다 먼저 부른다.
        /// 같은 값으로 여러 번 불러도 결과가 같다.
        /// </summary>
        public void SetupAscension(AscensionTable table, int level)
        {
            Ascension = AscensionEffects.For(table, level);
            AscensionLevel = Ascension.Level;
        }
    }
}
