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
        /// <summary>두 키 사이에 키캡 없이 쓰는 글(UI-07, 범위 "F1" "~" "F10"). 없으면 빈 글이고 두 키는 그냥 나란히 놓는다.</summary>
        public readonly string Joiner;

        public KeyHint(string key, string action, bool hold = false, string secondKey = null, string joiner = null)
        {
            Key = key ?? string.Empty;
            SecondKey = secondKey ?? string.Empty;
            Joiner = joiner ?? string.Empty;
            Action = action ?? string.Empty;
            Hold = hold;
        }
    }
}
