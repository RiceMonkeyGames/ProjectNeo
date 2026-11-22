namespace ProjectNeo.Walls
{
    /// <summary>성 킷 조각 종류(SIEGE-14 시험). <see cref="CastleKitSettings"/>의 프리팹 배열 순서와 같다.</summary>
    public enum CastleKitPiece
    {
        Panel = 0,
        PanelAccent = 1,
        Pillar = 2,
        Floor = 3,
        Battlement = 4,
        BattlementCap = 5,
        /// <summary>성문 바깥 틀(문짝·내리닫이 쇠창살 포함).</summary>
        GateOuter = 6,
        /// <summary>성문 안쪽 틀(문 없음).</summary>
        GateInner = 7,
        /// <summary>성벽 위 한 층 차이를 잇는 계단(충돌체 있음).</summary>
        WallStairs = 8,
        /// <summary>성 안쪽 면 맨 윗층의 돋을새김 띠(Wall_Panel_Stone_02).</summary>
        InnerRelief = 9,
        /// <summary>무너진 칸을 향한 성벽 끝 단면: 맨 윗층(Rubble_Top).</summary>
        RubbleTop = 10,
        /// <summary>무너진 칸을 향한 성벽 끝 단면: 중간층(Rubble_Middle).</summary>
        RubbleMiddle = 11,
        /// <summary>무너진 칸을 향한 성벽 끝 단면: 땅층(Rubble_Bottom, 아래쪽은 돌무더기가 덮는다).</summary>
        RubbleBottom = 12,
        /// <summary>무너진 칸 바닥의 돌무더기(RubblePile, 외형 전용).</summary>
        RubblePile = 13,
        /// <summary>무너진 끝 구획 벽판의 성한 절반(Wall_Cut_01, 반 모듈).</summary>
        PanelHalf = 14,
        /// <summary>무너진 끝 구획 벽판의 부서진 절반: 맨 윗층(DestroyedWall_Top_01).</summary>
        BrokenTop = 15,
        /// <summary>무너진 끝 구획 벽판의 부서진 절반: 중간층(DestroyedWall_Middle_01).</summary>
        BrokenMiddle = 16,
        /// <summary>무너진 끝 구획 벽판의 부서진 절반: 땅층(DestroyedWall_Bottom_01).</summary>
        BrokenBottom = 17,
        /// <summary>무너진 끝 구획 흉벽의 성한 절반(Battlements_01_Half).</summary>
        BattlementHalf = 18,
        /// <summary>통로가 꺾이는 모서리 칸의 층참 꺾임 계단(Stairs_Wood_Corner_01, 충돌체 있음).</summary>
        CornerStairs = 19,
        /// <summary>무너진 끝 구획의 통로 바닥 성한 절반(Floor_Wood_03, 반 모듈 × 한 모듈).</summary>
        FloorHalf = 20,
    }
}
