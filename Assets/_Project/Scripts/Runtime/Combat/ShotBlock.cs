namespace ProjectNeo.Combat
{
    /// <summary>원거리 사선을 무엇이 막는지(COMBAT-12). 값은 저장하지 않지만 고정해 둔다.</summary>
    public enum ShotBlock
    {
        /// <summary>막히지 않았다.</summary>
        Clear = 0,
        /// <summary>대상이 아닌 다른 살아 있는 적만 막는다.</summary>
        LivingEnemy = 1,
        /// <summary>성벽·땅 같은 구조물이 막는다.</summary>
        Structure = 2,
    }
}
