namespace ProjectNeo.Stages
{
    /// <summary>보스 역할(Q-74). 저장·에셋에 남으므로 번호를 바꾸지 않고 새 값은 끝에 더한다.</summary>
    public enum BossRole
    {
        /// <summary>성문·성벽을 부순다. 구조물 피해 배율을 쓴다.</summary>
        Siege = 0,
        /// <summary>플레이어와 영웅을 먼저 노린다.</summary>
        Duelist = 1,
        /// <summary>주변 적을 강화하고, 죽으면 자기 웨이브가 약해진다.</summary>
        Commander = 2,
        /// <summary>먼 거리에서 성벽 위를 쏜다. 궁수 동작에 긴 사거리를 쓴다.</summary>
        Artillery = 3,
    }
}
