namespace ProjectNeo.Combat
{
    /// <summary>활 든 모델의 상체 동작 단계(ART-04). 값은 저장되지 않지만 순서를 고정한다.</summary>
    public enum BowDrawPhase
    {
        Lowered = 0,
        Drawing = 1,
        Holding = 2,
        Releasing = 3,
    }
}
