namespace ProjectNeo.Meta
{
    /// <summary>트랙에서 무작위 해금을 할 수 있는지(UI-06, 사용자 결정 2026-11-21).</summary>
    public enum UnlockDrawCheck
    {
        Ok = 0,
        /// <summary>선행 조건을 채운 미해금 항목이 없다(모두 열었거나 선행이 막혔다).</summary>
        NothingLeft = 1,
        NotEnoughPoints = 2,
        UnknownTrack = 3,
    }
}
