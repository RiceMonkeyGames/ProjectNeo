namespace ProjectNeo.UI
{
    /// <summary>키 안내의 한 줄: 키 이름, 하는 일, 길게 누르는 키인가(UI-06, UI-12).</summary>
    public readonly struct KeyHint
    {
        public readonly string Key;
        public readonly string Action;
        public readonly bool Hold;
        /// <summary>같은 줄의 두 번째 키(UI-12, 분대 페이지 "[" "]"). 키 가이드에서 따로 한 칸으로 그린다. 없으면 빈 글.</summary>
        public readonly string SecondKey;

        public KeyHint(string key, string action, bool hold = false, string secondKey = null)
        {
            Key = key ?? string.Empty;
            SecondKey = secondKey ?? string.Empty;
            Action = action ?? string.Empty;
            Hold = hold;
        }
    }
}
