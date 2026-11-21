namespace ProjectNeo.Squads
{
    /// <summary>부대 진형(COMBAT-08). 값은 저장·인스펙터에 쓰이므로 바꾸지 않는다.</summary>
    public enum FormationKind
    {
        /// <summary>횡대. 기본 진형.</summary>
        Line = 0,
        /// <summary>밀집. 간격이 좁다.</summary>
        Close = 1,
        /// <summary>산개. 간격이 넓다.</summary>
        Loose = 2,
        /// <summary>종대. 폭이 정해져 있고 드래그 폭을 쓰지 않는다.</summary>
        Column = 3,
    }
}
