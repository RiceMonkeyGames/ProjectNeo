using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 성 킷 조각 하나의 자리(SIEGE-14 시험). <see cref="Position"/>은 성 좌표 m의 프리팹 기준점, <see cref="Yaw"/>는 도(°)다.
    /// <see cref="MirrorZ"/>면 로컬 z를 뒤집어 놓고(꺾임 계단의 좌우 대칭본), <see cref="ScaleY"/>만큼 세로로 늘린다(여러 층 높이의 무너진 V자).
    /// 조각은 킷 모듈(5m) 크기 그대로이고, 칸이 모듈보다 넓을 때의 늘림은 <see cref="StretchX"/>·<see cref="StretchZ"/>에 담긴다(SIEGE-35).
    /// <see cref="Cell"/>은 묶어 다시 만들 칸이다.
    /// </summary>
    public readonly struct CastleKitPlacement
    {
        public readonly CastleKitPiece Piece;
        public readonly Vector2Int Cell;
        public readonly Vector3 Position;
        public readonly float Yaw;
        public readonly bool MirrorZ;
        public readonly float ScaleY;
        /// <summary>로컬 x·z 배율(SIEGE-27: 계단 방의 반 층 계단, 구멍 둘레 바닥 조각). 기본 1.</summary>
        public readonly float StretchX, StretchZ;
        /// <summary>요(yaw) 다음에 로컬 z축으로 기울이는 각도(°). <see cref="CastleKitPiece.RoomCollider"/> 경사에만 쓴다.</summary>
        public readonly float Tilt;

        public CastleKitPlacement(CastleKitPiece piece, Vector2Int cell, Vector3 position, float yaw, bool mirrorZ = false, float scaleY = 1f,
            float stretchX = 1f, float stretchZ = 1f, float tilt = 0f)
        {
            Piece = piece;
            Cell = cell;
            Position = position;
            Yaw = yaw;
            MirrorZ = mirrorZ;
            ScaleY = scaleY;
            StretchX = stretchX;
            StretchZ = stretchZ;
            Tilt = tilt;
        }
    }
}
