using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 운반한 사다리를 걸 수 있는 자리 하나(SIEGE-37): 성벽 칸, 바깥 면 방향, 면 가운데에서 잰 빈틈 거리와
    /// 그 자리에 섰을 때의 사다리 밑(길찾기 위)·위쪽 끝 자리.
    /// </summary>
    public readonly struct LadderSlot
    {
        public Vector2Int Cell { get; }
        public Vector2Int Outward { get; }
        public float Offset { get; }
        /// <summary>사다리를 세울 땅 위 자리(길찾기 메시 위로 맞춘 값).</summary>
        public Vector3 BasePoint { get; }
        /// <summary>다 오른 유닛이 올라서는 성벽 위 자리.</summary>
        public Vector3 TopPoint { get; }

        public LadderSlot(Vector2Int cell, Vector2Int outward, float offset, Vector3 basePoint, Vector3 topPoint)
        {
            Cell = cell; Outward = outward; Offset = offset; BasePoint = basePoint; TopPoint = topPoint;
        }

        public bool SameAs(LadderSlot other) => Cell == other.Cell && Outward == other.Outward
            && Mathf.Abs(Offset - other.Offset) < 0.01f;
    }
}
