using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Squads
{
    /// <summary>
    /// 준비 단계 부대 배치 구역(PREP-05). 성벽·성문 조각이 차지하는 사각형 범위(성 좌표의 XZ) 안을 성 안으로 본다.
    /// 성벽 위도 이 범위에 든다. 씬은 알지 않는다.
    /// </summary>
    public static class CastleInteriorRules
    {
        /// <summary>조각 중심점들을 감싸는 사각형. 점이 없으면 false.</summary>
        public static bool TryGetBounds(IEnumerable<Vector2> piecePoints, float pieceHalfSize, out Rect bounds)
        {
            bounds = default;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            bool any = false;
            foreach (Vector2 point in piecePoints)
            {
                any = true;
                minX = Mathf.Min(minX, point.x); maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y); maxY = Mathf.Max(maxY, point.y);
            }
            if (!any) return false;
            float half = Mathf.Max(0f, pieceHalfSize);
            bounds = Rect.MinMaxRect(minX - half, minY - half, maxX + half, maxY + half);
            return true;
        }

        /// <summary>경계선 위도 안으로 본다.</summary>
        public static bool Contains(Rect bounds, Vector2 point)
            => point.x >= bounds.xMin && point.x <= bounds.xMax && point.y >= bounds.yMin && point.y <= bounds.yMax;
    }
}
