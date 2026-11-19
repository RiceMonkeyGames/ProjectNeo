namespace ProjectNeo.Meta
{
    /// <summary>해금 화면에서 항목을 누른 결과(META-06).</summary>
    public enum UnlockClickResult
    {
        /// <summary>살 수 있어 확인을 기다린다. 같은 항목을 한 번 더 누르면 산다.</summary>
        Armed = 0,
        /// <summary>샀다. 메타 진행이 바뀌었으니 저장해야 한다.</summary>
        Opened = 1,
        /// <summary>살 수 없다. 이유는 <see cref="UnlockScreenModel.Notice"/>.</summary>
        Refused = 2,
    }
}
