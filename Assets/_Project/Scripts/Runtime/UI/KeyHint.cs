namespace ProjectNeo.UI
{
    /// <summary>전체 키 목록(H, UI-06)의 한 줄: 키 이름과 하는 일.</summary>
    public readonly struct KeyHint
    {
        public readonly string Key;
        public readonly string Action;

        public KeyHint(string key, string action)
        {
            Key = key ?? string.Empty;
            Action = action ?? string.Empty;
        }
    }
}
