namespace ProjectNeo.Combat
{
    /// <summary>
    /// 지금 하는 일로 정해지는 위협 종류를 알려 준다(COMBAT-14). 공성 적의 판단(사다리, 성문 부수기 등)이 구현하고
    /// <see cref="CharacterActor.ThreatSource"/>에 스스로를 등록한다.
    /// </summary>
    public interface ITargetThreatSource
    {
        TargetThreat Threat { get; }
    }
}
