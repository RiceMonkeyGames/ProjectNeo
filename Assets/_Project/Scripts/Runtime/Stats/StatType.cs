namespace ProjectNeo.Stats
{
    /// <summary>
    /// 스탯 종류. 에셋에 숫자로 저장되므로 기존 값의 숫자는 바꾸지 말고 새 값은 끝에 추가한다.
    /// </summary>
    public enum StatType
    {
        MaxHealth = 0,
        Attack = 1,
        Defense = 2,
        AttackSpeed = 3,
        MoveSpeed = 4,
        AttackRange = 5,
    }
}
