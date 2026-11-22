using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 성 킷 조각 하나의 자리(SIEGE-14 시험). <see cref="Position"/>은 성 좌표 m의 프리팹 기준점, <see cref="Yaw"/>는 도(°)다.
    /// <see cref="MirrorZ"/>면 로컬 z를 뒤집어 놓고(꺾임 계단의 좌우 대칭본), <see cref="ScaleY"/>만큼 세로로 늘린다(여러 층 높이의 무너진 V자).
    /// 킷 모듈(5m)과 칸 크기가 다르면 쓰는 쪽이 칸 크기 ÷ 모듈 크기로 고르게 늘리거나 줄인다. <see cref="Cell"/>은 묶어 다시 만들 칸이다.
    /// </summary>
    public readonly struct CastleKitPlacement
    {
        public readonly CastleKitPiece Piece;
        public readonly Vector2Int Cell;
        public readonly Vector3 Position;
        public readonly float Yaw;
        public readonly bool MirrorZ;
        public readonly float ScaleY;

        public CastleKitPlacement(CastleKitPiece piece, Vector2Int cell, Vector3 position, float yaw, bool mirrorZ = false, float scaleY = 1f)
        {
            Piece = piece;
            Cell = cell;
            Position = position;
            Yaw = yaw;
            MirrorZ = mirrorZ;
            ScaleY = scaleY;
        }
    }
}
