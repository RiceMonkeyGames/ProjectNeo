using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 성가퀴 규칙(SIEGE-10). 씬을 알지 않는다.
    /// 성벽·성문 칸의 맨 위 조각에서, 성 바깥을 향한 면에만 세운다. 같은 높이 이상의 조각과 맞닿은 면(이어진 성벽, 계단 꼭대기)과
    /// 성 안쪽 면은 비운다. 성 바깥은 격자 밖에서 빈 칸을 따라 닿을 수 있는 칸이다.
    /// </summary>
    public static class BattlementRules
    {
        /// <summary>면 순서: 0 북(+z), 1 동(+x), 2 남(-z), 3 서(-x).</summary>
        public static readonly Vector2Int[] Directions =
            { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };

        const float SurfaceTolerance = 0.01f;

        public static int DirectionIndex(Vector2Int direction)
        {
            for (int i = 0; i < Directions.Length; i++) if (Directions[i] == direction) return i;
            return -1;
        }

        static bool InGrid(BuildGridDefinition grid, Vector2Int cell) =>
            cell.x >= 0 && cell.y >= 0 && cell.x < grid.Width && cell.y < grid.Depth;

        /// <summary>격자 가장자리에서 빈 칸(조각이 없는 칸)만 따라 닿을 수 있는 칸을 모은다.</summary>
        public static void CollectOutside(BuildLayout layout, BuildGridDefinition grid, HashSet<Vector2Int> outside)
        {
            outside.Clear();
            var open = new Queue<Vector2Int>();
            for (int x = 0; x < grid.Width; x++)
                for (int z = 0; z < grid.Depth; z++)
                {
                    if (x != 0 && z != 0 && x != grid.Width - 1 && z != grid.Depth - 1) continue;
                    var cell = new Vector2Int(x, z);
                    if (layout.ColumnHeight(cell) == 0 && outside.Add(cell)) open.Enqueue(cell);
                }
            while (open.Count > 0)
            {
                Vector2Int cell = open.Dequeue();
                foreach (Vector2Int direction in Directions)
                {
                    Vector2Int next = cell + direction;
                    if (InGrid(grid, next) && layout.ColumnHeight(next) == 0 && outside.Add(next)) open.Enqueue(next);
                }
            }
        }

        /// <summary>격자 밖이거나 바깥에 속한 칸인지.</summary>
        public static bool IsOutside(BuildGridDefinition grid, ICollection<Vector2Int> outside, Vector2Int cell) =>
            !InGrid(grid, cell) || outside.Contains(cell);

        /// <summary>
        /// 그 칸에서 성가퀴를 세울 면을 비트로 돌려준다(비트 i = <see cref="Directions"/>[i]).
        /// 맨 위 조각이 없거나 계단이면 0이다.
        /// </summary>
        public static int Faces(BuildLayout layout, BuildGridDefinition grid, ICollection<Vector2Int> outside, Vector2Int cell)
        {
            BuildPlacement top = layout.Top(cell);
            if (top == null || top.Definition.Kind == BuildPieceKind.Stairs) return 0;
            float surface = top.Elevation + top.Definition.Height;
            int mask = 0;
            for (int i = 0; i < Directions.Length; i++)
            {
                Vector2Int next = cell + Directions[i];
                BuildPlacement other = InGrid(grid, next) ? layout.Top(next) : null;
                // 같은 높이 이상과 맞닿으면 걸어서 이어지거나 벽면이 막아 주므로 비운다.
                if (other != null && other.Elevation + other.Definition.Height >= surface - SurfaceTolerance) continue;
                if (!IsOutside(grid, outside, next)) continue;
                mask |= 1 << i;
            }
            return mask;
        }

        /// <summary>
        /// 성벽 위 통로 폭(COMBAT-08 진형 줄 수 상한). <paramref name="cell"/>에서 <paramref name="axis"/> 방향 앞뒤로
        /// 같은 높이의 맨 위 조각(계단 제외)이 이어진 칸 수 × 칸 크기에서, 양 끝 면에 선 성가퀴 두께를 뺀다.
        /// 맨 위 조각이 없거나 계단이면 +∞(성벽 위가 아니다).
        /// </summary>
        public static float WalkwayWidth(BuildLayout layout, BuildGridDefinition grid, ICollection<Vector2Int> outside,
            Vector2Int cell, Vector2Int axis, float thickness)
        {
            BuildPlacement top = layout.Top(cell);
            int face = DirectionIndex(axis), back = DirectionIndex(-axis);
            if (top == null || top.Definition.Kind == BuildPieceKind.Stairs || face < 0) return float.PositiveInfinity;
            float surface = top.Elevation + top.Definition.Height;
            int cells = 1;
            Vector2Int high = cell, low = cell;
            while (SameSurface(layout, grid, high + axis, surface)) { high += axis; cells++; }
            while (SameSurface(layout, grid, low - axis, surface)) { low -= axis; cells++; }
            float width = cells * grid.CellSize;
            if ((Faces(layout, grid, outside, high) & (1 << face)) != 0) width -= thickness;
            if ((Faces(layout, grid, outside, low) & (1 << back)) != 0) width -= thickness;
            return Mathf.Max(0f, width);
        }

        static bool SameSurface(BuildLayout layout, BuildGridDefinition grid, Vector2Int cell, float surface)
        {
            BuildPlacement other = InGrid(grid, cell) ? layout.Top(cell) : null;
            return other != null && other.Definition.Kind != BuildPieceKind.Stairs
                && Mathf.Abs(other.Elevation + other.Definition.Height - surface) <= SurfaceTolerance;
        }

        /// <summary>
        /// 길이 <paramref name="edgeLength"/>인 면 하나에 놓을 덩어리를 채운다. 면 전체에 낮은 받침(난간)이 이어진다.
        /// 성가퀴는 그 위에 솟은 부분을 일정 간격으로 놓되, 면 가운데가 빈틈(크레넬)이 되게 한다. 사다리는 면 가운데에 걸리므로
        /// 늘 빈틈의 받침 위에 놓이고, 무늬는 사다리 자리에서도 끊기지 않는다. 칸 길이가 주기의 배수면 이웃 칸과 무늬가 이어진다.
        /// </summary>
        public static void Segments(BattlementStyle style, BattlementSettings settings, float edgeLength,
            List<BattlementSegment> result)
        {
            result.Clear();
            if (style == BattlementStyle.None || settings == null || edgeLength <= 0f) return;
            result.Add(new BattlementSegment(0f, edgeLength, settings.ParapetHeight));
            if (style != BattlementStyle.Crenellated) return;
            float period = settings.MerlonWidth + settings.CrenelWidth;
            float half = settings.MerlonWidth * 0.5f;
            float middle = edgeLength * 0.5f;
            // 가운데에서 반 주기씩 벗어난 자리가 솟은 부분의 가운데다.
            int reach = Mathf.CeilToInt(middle / period) + 1;
            for (int k = -reach; k < reach; k++)
            {
                float center = middle + period * (k + 0.5f);
                float start = Mathf.Max(0f, center - half), end = Mathf.Min(edgeLength, center + half);
                if (end > start + 1e-4f) result.Add(new BattlementSegment(start, end, settings.MerlonHeight));
            }
        }

        /// <summary>
        /// 성가퀴 면 하나에서 빈틈(크레넬) 가운데 자리(면 시작에서 잰 거리, COMBAT-10 사격 자리).
        /// 면 가운데가 늘 빈틈이고 한 주기마다 되풀이된다. 양 끝(0, 면 길이)의 반쪽 빈틈은 이웃 칸과 이어진 빈틈이라 포함한다.
        /// 성가퀴가 아니면(난간·없음) 비운다.
        /// </summary>
        public static void CrenelCenters(BattlementStyle style, BattlementSettings settings, float edgeLength, List<float> result)
        {
            result.Clear();
            if (style != BattlementStyle.Crenellated || settings == null || edgeLength <= 0f) return;
            float period = settings.MerlonWidth + settings.CrenelWidth;
            float middle = edgeLength * 0.5f;
            int reach = Mathf.CeilToInt(middle / period) + 1;
            for (int k = -reach; k <= reach; k++)
            {
                float center = middle + period * k;
                if (center >= -1e-4f && center <= edgeLength + 1e-4f) result.Add(Mathf.Clamp(center, 0f, edgeLength));
            }
        }
    }
}
