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
        public static void CollectOutside(BuildLayout layout, BuildGridDefinition grid, HashSet<Vector2Int> outside) =>
            CollectOutside(layout, grid, outside, null);

        /// <summary>
        /// 격자 가장자리에서 빈 칸만 따라 닿을 수 있는 칸을 모은다. <paramref name="alwaysInside"/>의 칸은 비어 있어도
        /// 바깥이 되지 않고 지나갈 수도 없다(BUG-battlement-inner-faces: 기본 성 안뜰은 성벽에 틈이 나도 늘 안쪽).
        /// </summary>
        public static void CollectOutside(BuildLayout layout, BuildGridDefinition grid, HashSet<Vector2Int> outside,
            ICollection<Vector2Int> alwaysInside)
        {
            outside.Clear();
            var open = new Queue<Vector2Int>();
            for (int x = 0; x < grid.Width; x++)
                for (int z = 0; z < grid.Depth; z++)
                {
                    if (x != 0 && z != 0 && x != grid.Width - 1 && z != grid.Depth - 1) continue;
                    // 성이 남쪽 가장자리 너머에 있으면(SIEGE-40) 남쪽 가장자리만 맞닿은 칸에서는 바깥이 시작되지 않는다.
                    if (grid.CastleBehindSouthEdge && z == 0 && x != 0 && x != grid.Width - 1) continue;
                    var cell = new Vector2Int(x, z);
                    if (IsOpen(layout, alwaysInside, cell) && outside.Add(cell)) open.Enqueue(cell);
                }
            while (open.Count > 0)
            {
                Vector2Int cell = open.Dequeue();
                foreach (Vector2Int direction in Directions)
                {
                    Vector2Int next = cell + direction;
                    if (InGrid(grid, next) && IsOpen(layout, alwaysInside, next) && outside.Add(next)) open.Enqueue(next);
                }
            }
        }

        static bool IsOpen(BuildLayout layout, ICollection<Vector2Int> alwaysInside, Vector2Int cell) =>
            layout.ColumnHeight(cell) == 0 && (alwaysInside == null || !alwaysInside.Contains(cell));

        /// <summary>
        /// 기본 성(런 시작 배치) <paramref name="startLayout"/>의 바닥 전체를 모은다. 바깥에서 닿지 않는 칸, 곧 성벽 고리 칸과
        /// 그 안뜰이다. 고리 칸이 무너져 비어도 바깥이 아니므로, 틈으로 안뜰까지 바깥이 번지지 않고
        /// 틈 양옆 성벽의 끝 면(틈을 향한 면)에도 성가퀴가 서지 않는다.
        /// </summary>
        public static void CollectEnclosed(BuildLayout startLayout, BuildGridDefinition grid, HashSet<Vector2Int> inside)
        {
            inside.Clear();
            var reached = new HashSet<Vector2Int>();
            CollectOutside(startLayout, grid, reached);
            for (int x = 0; x < grid.Width; x++)
                for (int z = 0; z < grid.Depth; z++)
                {
                    var cell = new Vector2Int(x, z);
                    if (!reached.Contains(cell)) inside.Add(cell);
                }
        }

        /// <summary>격자 밖이거나 바깥에 속한 칸인지. 성이 남쪽 가장자리 너머에 있으면 격자 남쪽 밖은 성 안이다(SIEGE-40).</summary>
        public static bool IsOutside(BuildGridDefinition grid, ICollection<Vector2Int> outside, Vector2Int cell) =>
            InGrid(grid, cell) ? outside.Contains(cell) : !IsBehindSouthEdge(grid, cell);

        /// <summary>성이 남쪽 가장자리 너머에 있는 격자에서 격자 남쪽 밖(성 안) 칸인지.</summary>
        public static bool IsBehindSouthEdge(BuildGridDefinition grid, Vector2Int cell) =>
            grid != null && grid.CastleBehindSouthEdge && cell.y < 0;

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
        /// 성가퀴는 그 위에 솟은 부분을 일정 간격으로 놓되, 면 가운데가 빈틈(크레넬)이 되게 한다. 킷 무늬
        /// (<see cref="BattlementSettings.MerlonAtMiddle"/>)는 면 가운데가 솟은 부분이다(SIEGE-20). 사다리는 늘 빈틈의 받침 위에
        /// 걸리고(<see cref="LadderCrenelOffset"/>), 무늬는 사다리 자리에서도 끊기지 않는다. 칸 길이가 주기의 배수면 이웃 칸과 무늬가 이어진다.
        /// 난간에 빈틈 받침(<see cref="BattlementSettings.ParapetGapSill"/>)이 있으면 난간도 같은 무늬의 낮은 돌(난간 높이)이다.
        /// </summary>
        public static void Segments(BattlementStyle style, BattlementSettings settings, float edgeLength,
            List<BattlementSegment> result)
        {
            result.Clear();
            if (style == BattlementStyle.None || settings == null || edgeLength <= 0f) return;
            float sill = SillHeight(style, settings);
            result.Add(new BattlementSegment(0f, edgeLength, sill));
            float top = style == BattlementStyle.Crenellated ? settings.MerlonHeight : settings.ParapetHeight;
            if (top <= sill + 1e-4f) return;
            float period = settings.MerlonWidth + settings.CrenelWidth;
            float half = settings.MerlonWidth * 0.5f;
            float middle = edgeLength * 0.5f;
            // 빈틈 가운데에서 반 주기 벗어난 자리가 솟은 부분의 가운데다.
            float phase = CrenelPhase(settings) + 0.5f;
            int reach = Mathf.CeilToInt(middle / period) + 1;
            for (int k = -reach; k <= reach; k++)
            {
                float center = middle + period * (k + phase);
                float start = Mathf.Max(0f, center - half), end = Mathf.Min(edgeLength, center + half);
                if (end > start + 1e-4f) result.Add(new BattlementSegment(start, end, top));
            }
        }

        /// <summary>
        /// 그 모양의 빈틈 받침 높이(사다리 꼭대기가 걸리는 높이). 성가퀴는 난간 높이, 난간은 낮은 돌이면 빈틈 받침, 아니면 난간 높이다.
        /// </summary>
        public static float SillHeight(BattlementStyle style, BattlementSettings settings)
        {
            if (style == BattlementStyle.None || settings == null) return 0f;
            if (style == BattlementStyle.Parapet && settings.ParapetGapSill > 0f) return settings.ParapetGapSill;
            return settings.ParapetHeight;
        }

        /// <summary>그 모양의 덩어리를 칸 가장자리에서 바깥으로 내미는 거리(m). 성가퀴만 내민다(킷 Battlements_03, SIEGE-20).</summary>
        public static float Outset(BattlementStyle style, BattlementSettings settings) =>
            style == BattlementStyle.Crenellated && settings != null ? settings.CrenellatedOutset : 0f;

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
            float phase = CrenelPhase(settings);
            int reach = Mathf.CeilToInt(middle / period) + 1;
            for (int k = -reach; k <= reach; k++)
            {
                float center = middle + period * (k + phase);
                if (center >= -1e-4f && center <= edgeLength + 1e-4f) result.Add(Mathf.Clamp(center, 0f, edgeLength));
            }
        }

        // 면 가운데에서 잰 빈틈 가운데의 위상(주기 단위). 킷 무늬는 가운데가 솟은 부분이라 반 주기 밀린다.
        static float CrenelPhase(BattlementSettings settings) => settings.MerlonAtMiddle ? 0.5f : 0f;

        /// <summary>
        /// 사다리가 걸리는 빈틈이 면 가운데에서 떨어진 거리(m, 면 시작 → 끝 방향이 +). 사다리는 면 가운데에 가장 가까운 빈틈에 걸리고,
        /// 둘이 같으면 면 시작 쪽을 고른다(SIEGE-20). 면 가운데가 빈틈인 무늬나 설정이 없으면 0이다.
        /// 성가퀴 모양(난간·포구)과 상관없이 무늬로만 정해, 준비 중 단계가 바뀌어도 사다리 자리가 움직이지 않는다.
        /// </summary>
        public static float LadderCrenelOffset(BattlementSettings settings, float edgeLength)
        {
            if (settings == null || !settings.MerlonAtMiddle || edgeLength <= 0f) return 0f;
            float period = settings.MerlonWidth + settings.CrenelWidth;
            // 가운데 양옆 빈틈 중 면 안에 온전히 들어가는 것. 면이 한 주기보다 짧으면 가운데에 건다.
            float offset = period * 0.5f;
            if (edgeLength * 0.5f - offset < settings.CrenelWidth * 0.5f - 1e-4f) return 0f;
            return -offset;
        }
    }
}
