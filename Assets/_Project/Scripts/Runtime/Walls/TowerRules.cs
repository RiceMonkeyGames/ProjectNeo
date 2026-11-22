using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 망루 규칙(SIEGE-14, 사용자 결정 2026-11-23). 씬을 알지 않는다.
    /// 망루는 보루처럼 성 바깥 빈 칸에 성벽과 맞닿게 짓고(2×2), 놓으면 맞닿은 가장 높은 성벽보다 한 층 높게 층을 한 번에 쌓는다.
    /// 망루 위에는 망루 층만, 망루 층은 망루 위에만 쌓는다. 맨 윗층은 통로 높이의 방이고(문·내부 계단), 지붕에 아군 원거리 유닛이 선다.
    /// </summary>
    public static class TowerRules
    {
        const float HeightTolerance = 0.01f;

        /// <summary><see cref="BuildLayout.Check"/>와 보루 규칙을 통과한 뒤에 부른다.</summary>
        public static PlacementIssue Check(BuildLayout layout, BuildGridDefinition grid, ICollection<Vector2Int> outside,
            ICollection<Vector2Int> ladderLanes, BuildPieceDefinition definition, Vector2Int cell, int rotation,
            int level, float elevation)
        {
            if (layout == null || definition == null) return PlacementIssue.None;
            Vector2Int size = BuildLayout.RotatedSize(definition.Footprint, rotation);
            bool tower = definition.Kind == BuildPieceKind.Tower;
            for (int x = 0; x < size.x; x++)
                for (int z = 0; z < size.y; z++)
                {
                    Vector2Int key = cell + new Vector2Int(x, z);
                    BuildPlacement below = level > 0 ? layout.At(key, level - 1) : null;
                    bool onTower = below != null && below.Definition.Kind == BuildPieceKind.Tower;
                    if (level > 0 && tower != onTower) return PlacementIssue.TowerStack;
                    if (!tower) continue;
                    if (ladderLanes != null && ladderLanes.Contains(key)) return PlacementIssue.LadderLane;
                    if (level == 0 && (outside == null || !outside.Contains(key))) return PlacementIssue.TowerNeedsWall;
                }
            if (!tower || level > 0) return PlacementIssue.None;
            return BastionRules.TouchesWall(layout, grid, cell, size, level, elevation) ? PlacementIssue.None : PlacementIssue.TowerNeedsWall;
        }

        /// <summary>
        /// 망루를 그 자리에 놓을 때 한 번에 쌓을 층 수. 맞닿은 성벽·성문 칸 가운데 가장 높은 칸보다 한 층 높게(격자 층 한도까지).
        /// 망루가 아니면 1. 이미 그 높이 이상이면 1(한 층씩 더 올린다).
        /// </summary>
        public static int Tiers(BuildLayout layout, BuildGridDefinition grid, BuildPieceDefinition definition, Vector2Int cell,
            int rotation, int level)
        {
            if (layout == null || definition == null || definition.Kind != BuildPieceKind.Tower) return 1;
            Vector2Int size = BuildLayout.RotatedSize(definition.Footprint, rotation);
            int tallest = 0;
            ForEachNeighbour(grid, cell, size, (inside, next, face) =>
            {
                BuildPlacement top = layout.Top(next);
                if (top != null && (top.Definition.Kind == BuildPieceKind.Wall || top.Definition.Kind == BuildPieceKind.Gate))
                    tallest = Mathf.Max(tallest, layout.ColumnHeight(next));
            });
            int target = Mathf.Min(tallest + 1, grid != null ? grid.MaxLevels : tallest + 1);
            return Mathf.Max(1, target - level);
        }

        /// <summary>망루 맨 윗층 방의 짜임. <see cref="HasRoom"/>이 거짓이면 방 없이 꽉 찬 층이다(통로와 이어지는 면이 없다).</summary>
        public readonly struct Interior
        {
            public readonly bool HasRoom;
            /// <summary>통로 쪽 면(0 북, 1 동, 2 남, 3 서). 문과 계단이 이 면에 붙는다.</summary>
            public readonly int InnerFace;
            public readonly Vector2Int DoorCell;
            public readonly Vector2Int StairCell;
            /// <summary>계단이 올라가는 쪽(계단 칸에서 문 칸의 반대쪽 면).</summary>
            public readonly int RiseFace;

            public Interior(bool hasRoom, int innerFace, Vector2Int doorCell, Vector2Int stairCell, int riseFace)
            { HasRoom = hasRoom; InnerFace = innerFace; DoorCell = doorCell; StairCell = stairCell; RiseFace = riseFace; }
        }

        /// <summary>
        /// 망루 맨 윗층 방: 바닥 높이가 이웃 성벽 통로와 같은 면을 문 쪽(안쪽)으로 고른다(가장 많이 닿은 쪽, 같으면 방향 순서).
        /// 안쪽 줄 두 칸 가운데 통로와 닿은 첫 칸이 문 칸, 다른 칸이 계단 칸이고 계단은 문 칸의 반대쪽으로 오른다.
        /// </summary>
        public static Interior RoomOf(BuildLayout layout, BuildGridDefinition grid, BuildPlacement top)
        {
            if (layout == null || top == null || top.Definition.Kind != BuildPieceKind.Tower) return default;
            Vector2Int size = top.Size;
            var counts = new int[4];
            float floor = top.Elevation;
            ForEachNeighbour(grid, top.Cell, size, (inside, next, face) =>
            {
                if (Walkway(layout, next, floor)) counts[face]++;
            });
            int inner = -1;
            for (int face = 0; face < 4; face++) if (counts[face] > 0 && (inner < 0 || counts[face] > counts[inner])) inner = face;
            if (inner < 0) return default;
            Vector2Int direction = BattlementRules.Directions[inner];
            var row = new List<Vector2Int>();
            for (int x = 0; x < size.x; x++)
                for (int z = 0; z < size.y; z++)
                {
                    var key = top.Cell + new Vector2Int(x, z);
                    var outward = key + direction;
                    if (outward.x < top.Cell.x || outward.y < top.Cell.y || outward.x >= top.Cell.x + size.x || outward.y >= top.Cell.y + size.y)
                        row.Add(key);
                }
            if (row.Count < 2) return default;
            Vector2Int door = row[0], stair = row[1];
            if (!Walkway(layout, door + direction, floor)) { door = row[1]; stair = row[0]; }
            Vector2Int rise = stair - door;
            int riseFace = 0;
            for (int face = 0; face < 4; face++) if (BattlementRules.Directions[face] == rise) riseFace = face;
            return new Interior(true, inner, door, stair, riseFace);
        }

        /// <summary>
        /// 그 자리(성 좌표, 발 높이)의 맨 위 조각이 윗면 효과(<see cref="BuildPieceDefinition.OccupantModifiers"/>)를 가진 조각이고
        /// 발이 그 윗면 위에 있는지.
        /// </summary>
        public static bool StandingOn(BuildLayout layout, Vector3 localFeet, float tolerance, out BuildPlacement top)
        {
            top = null;
            if (layout == null) return false;
            BuildPlacement candidate = layout.Top(layout.WorldToCell(localFeet));
            if (candidate == null || candidate.Definition.OccupantModifiers.Count == 0) return false;
            float surface = candidate.Elevation + candidate.Definition.Height;
            if (Mathf.Abs(localFeet.y - surface) > tolerance) return false;
            top = candidate;
            return true;
        }

        // 그 칸 맨 위가 성벽·성문이고 윗면이 floor 높이인지(망루 방과 같은 높이의 통로).
        static bool Walkway(BuildLayout layout, Vector2Int cell, float floor)
        {
            BuildPlacement top = layout.Top(cell);
            return top != null && (top.Definition.Kind == BuildPieceKind.Wall || top.Definition.Kind == BuildPieceKind.Gate)
                && Mathf.Abs(top.Elevation + top.Definition.Height - floor) <= HeightTolerance;
        }

        // 조각 둘레 바깥의 이웃 칸마다(격자 안만) 부른다. 인자: 조각 안 칸, 이웃 칸, 면.
        static void ForEachNeighbour(BuildGridDefinition grid, Vector2Int cell, Vector2Int size, System.Action<Vector2Int, Vector2Int, int> visit)
        {
            for (int x = 0; x < size.x; x++)
                for (int z = 0; z < size.y; z++)
                {
                    Vector2Int key = cell + new Vector2Int(x, z);
                    for (int face = 0; face < 4; face++)
                    {
                        Vector2Int next = key + BattlementRules.Directions[face];
                        if (next.x >= cell.x && next.x < cell.x + size.x && next.y >= cell.y && next.y < cell.y + size.y) continue;
                        if (grid != null && (next.x < 0 || next.y < 0 || next.x >= grid.Width || next.y >= grid.Depth)) continue;
                        visit(key, next, face);
                    }
                }
        }
    }
}
