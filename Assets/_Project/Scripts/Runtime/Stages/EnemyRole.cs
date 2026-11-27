namespace ProjectNeo.Stages
{
    /// <summary>적 한 종류의 역할(CONTENT-04). 미리보기·보상·이후 콘텐츠가 분류에 쓴다. 저장 값이 바뀌지 않게 번호를 고정한다.</summary>
    public enum EnemyRole
    {
        /// <summary>사다리를 걸고 성벽을 오르는 보병.</summary>
        GroundLadder = 0,
        /// <summary>성문을 부수는 적.</summary>
        GateBreaker = 1,
        /// <summary>성벽을 부수는 적.</summary>
        WallBreaker = 2,
        /// <summary>땅에서 쏘는 원거리 적.</summary>
        Ranged = 3,
        /// <summary>성벽 위로 날아드는 적.</summary>
        Flyer = 4,
        /// <summary>높이 날며 쏘는 원거리 비행 적.</summary>
        FlyingRanged = 5,
    }
}
