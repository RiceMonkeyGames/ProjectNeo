namespace ProjectNeo.Siege
{
    /// <summary>수비병 한 명의 사다리 임무: 어느 사다리(<see cref="Site"/> 번호)에서 무슨 일을 몇 번 자리에서 하는지(SIEGE-31).</summary>
    public readonly struct LadderDuty
    {
        public readonly int Site;
        public readonly LadderDutyRole Role;
        public readonly int Spot;

        public static readonly LadderDuty None = new LadderDuty(-1, LadderDutyRole.None, -1);

        public LadderDuty(int site, LadderDutyRole role, int spot)
        {
            Site = site;
            Role = role;
            Spot = spot;
        }

        public bool IsNone => Role == LadderDutyRole.None;

        public override string ToString() => IsNone ? "None" : $"{Role}@{Site}#{Spot}";
    }
}
