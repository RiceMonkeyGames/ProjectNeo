namespace ProjectNeo.Meta
{
    /// <summary>해금 화면에서 항목 카드가 보이는 상태(UI-06).</summary>
    public enum UnlockEntryState
    {
        /// <summary>열렸다(기본으로 열린 것 포함).</summary>
        Opened = 0,
        /// <summary>선행을 채워 다음 무작위 해금에서 나올 수 있다.</summary>
        Candidate = 1,
        /// <summary>선행 항목이 아직 안 열려 지금은 나오지 않는다.</summary>
        Locked = 2,
    }
}
