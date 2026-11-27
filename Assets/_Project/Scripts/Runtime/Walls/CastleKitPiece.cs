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
        /// <summary>망루 맨윗층 방의 벽판(Wall_01, 충돌체 있음. 맨윗층은 조각 상자 대신 킷이 벽을 맡는다).</summary>
        TowerPanel = 21,
        /// <summary>망루 맨윗층 바깥 면의 화살 구멍 벽판(Wall_Arrowslit_01, 충돌체 있음).</summary>
        TowerArrowslit = 22,
        /// <summary>망루 맨윗층에서 통로로 난 문 벽판(Wall_Door_01, 뚫린 문, 충돌체 있음).</summary>
        TowerDoor = 23,
        /// <summary>망루 지붕 둘레의 내민 받침(Wall_Corbels_01).</summary>
        TowerCorbel = 24,
        /// <summary>망루 지붕 모서리의 내민 받침(Wall_Corbels_Corner_01).</summary>
        TowerCorbelCorner = 25,
        /// <summary>망루 지붕 모서리의 높은 흉벽 기둥(Battlements_Pillar_01, 1.2배).</summary>
        TowerCap = 26,
        /// <summary>3층 성벽 계단 방의 반 층 계단 한 줄(Stairs_Wood_Straight_01, 외형 전용. 충돌은 <see cref="RoomCollider"/>, SIEGE-27).</summary>
        RoomStairs = 27,
        /// <summary>
        /// 프리팹 없는 상자 충돌체(SIEGE-27 계단 방의 바닥·층참·경사). <see cref="CastleKitPlacement.Position"/>이 상자 가운데,
        /// <see cref="CastleKitPlacement.StretchX"/> × <see cref="CastleKitPlacement.ScaleY"/> × <see cref="CastleKitPlacement.StretchZ"/>가 크기(m),
        /// <see cref="CastleKitPlacement.Tilt"/>가 로컬 z축 기울기(°)다.
        /// </summary>
        RoomCollider = 28,
        /// <summary>
        /// 프리팹 없는 양방향 길찾기 연결(SIEGE-27 계단 방의 층참·발치 띠를 가로지른다). <see cref="CastleKitPlacement.Position"/>이 가운데,
        /// 로컬 +x(<see cref="CastleKitPlacement.Yaw"/>)로 <see cref="CastleKitPlacement.StretchX"/> 길이, <see cref="CastleKitPlacement.StretchZ"/> 폭(m).
        /// </summary>
        RoomLink = 29,
        /// <summary>
        /// 계단 방 둘째 계단의 기둥을 바닥까지 잇는 나무 기둥(SM_Bld_House_WoodBeam_01: 기준점 바닥 가운데, 높이 2.5m, 외형 전용).
        /// 계단 모델 기둥은 그 계단의 아래 끝 높이(층참)까지만 내려와, 그 아래를 이어 준다.
        /// </summary>
        RoomPost = 30,
        /// <summary>
        /// 옆으로 올라타는 꺾임 계단의 외형(<see cref="CornerStairs"/>와 같은 모델, 충돌체 없음). 계단 방 문으로만 들어오는 모서리에 쓰며,
        /// 충돌은 옆판 없는 경사 판(<see cref="RoomCollider"/>)이 맡는다(SIEGE-27).
        /// </summary>
        CornerStairsOpen = 31,
        /// <summary>
        /// 충돌체가 있는 반 모듈 벽판(Wall_Cut_01). 칸이 모듈보다 넓을 때 짜맞춤(<see cref="CastleKitLook.Compose"/>)으로
        /// 계단 방·망루 방의 벽과 문 양옆을 채운다(SIEGE-35).
        /// </summary>
        RoomPanelHalf = 32,
        /// <summary>성 바깥 모서리 윗부분의 내닫이 망루(Wall_Tower_M_01, 외형 전용, SIEGE-19).</summary>
        CornerTurret = 33,
        /// <summary>
        /// 성가퀴(Lv2) 면의 흉벽(Battlements_03: 바깥으로 내민 솟은 돌과 받침돌). Lv1 난간 면은 <see cref="Battlement"/>을
        /// 성벽에 묻어 낮은 돌만 남긴다(SIEGE-20, 사용자 결정 2026-11-27).
        /// </summary>
        Crenellation = 34,
        /// <summary>성가퀴(Lv2) 면의 반쪽 흉벽(Battlements_03_Half).</summary>
        CrenellationHalf = 35,
    }
}
