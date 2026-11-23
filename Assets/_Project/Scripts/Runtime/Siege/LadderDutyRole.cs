namespace ProjectNeo.Siege
{
    /// <summary>성벽 위 사다리 방어에서 수비병 한 명이 맡는 일(SIEGE-31).</summary>
    public enum LadderDutyRole
    {
        None = 0,       // 맡은 사다리가 없다. 평소처럼 자리를 지킨다.
        Push = 1,       // 위쪽 끝 옆 밀기 자리에서 사다리를 민다.
        Chokepoint = 2, // 출구 안쪽 반원에 서서 올라오는 적을 막는다.
    }
}
