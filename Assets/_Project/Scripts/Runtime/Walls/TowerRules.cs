using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 망루 규칙(SIEGE-14, 사용자 결정 2026-11-23; 성벽 줄 안 배치 SIEGE-24, 사용자 결정 2026-11-27). 씬을 알지 않는다.
    /// 망루(2×2)는 성벽 줄 안에 짓는다: 한쪽 줄 두 칸은 지금 성벽 칸(모든 층이 성벽)이고 놓을 때 그 성벽을 통째로 바꿔 끼우며,
    /// 다른 줄 두 칸은 성 바깥 빈 칸으로 바깥쪽에 내민다(<see cref="CheckInLine"/>). 새로 놓으면 정의의 처음 층 수(망루 2층)를
    /// 한 번에 쌓고, 그 위는 한 층씩 더 쌓는다(격자 층 한도까지). 망루 위에는 망루 층만, 망루 층은 망루 위에만 쌓는다.
    /// 통로와 닿은 층부터 방이고(문·내부 계단), 지붕에 아군 원거리 유닛이 선다.
    /// </summary>
    public static class TowerRules
    {
        const float HeightTolerance = 0.01f;

        /// <summary>
        /// <see cref="BuildLayout.Check"/>와 보루 규칙을 통과한 뒤에 부른다(쌓기 규칙). 망루를 새로 놓는 자리(땅, 0층)는
        /// <see cref="CheckInLine"/>이 맡으므로 여기서는 늘 <see cref="PlacementIssue.TowerNeedsWall"/>이다.
        /// </summary>
        public static PlacementIssue Check(BuildLayout layout, BuildGridDefinition grid, ICollection<Vector2Int> outside,
            BuildPieceDefinition definition, Vector2Int cell, int rotation,
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
                }
            return tower && level == 0 ? PlacementIssue.TowerNeedsWall : PlacementIssue.None;
        }

        /// <summary>그 자리(앵커 칸·크기)가 이미 놓인 망루 하나의 맨 윗면 전체인지(그 위에 한 층 더 쌓는 자리).</summary>
        public static bool IsStackOnTower(BuildLayout layout, Vector2Int cell, Vector2Int size)
        {
            if (layout == null) return false;
            BuildPlacement first = layout.Top(cell);
            if (first == null || first.Definition.Kind != BuildPieceKind.Tower || first.Cell != cell || first.Size != size) return false;
            for (int x = 0; x < size.x; x++)
                for (int z = 0; z < size.y; z++)
                    if (layout.Top(cell + new Vector2Int(x, z)) != first) return false;
            return true;
        }

        /// <summary>
        /// 망루를 새로 놓는 자리 검사(SIEGE-24). 네 방향 가운데 한 방향을 바깥으로 보고, 그 반대쪽 줄(안쪽 줄)의 칸이 모두
        /// 고정되지 않은 성벽만 쌓인 칸이고, 바깥쪽 줄의 칸이 모두 성 바깥 빈 칸이며, 안쪽 줄의 안쪽 이웃이 성 바깥이 아니면
        /// (성벽 줄이 안뜰을 두르고 있으면) 놓을 수 있다. <paramref name="replaced"/>가 있으면 바꿔 끼울 성벽 조각을 위층부터 담는다.
        /// 땅 계단이 안쪽 줄 성벽에 기대어 있으면 놓지 않는다(계단이 오를 곳을 잃는다).
        /// </summary>
        public static PlacementIssue CheckInLine(BuildLayout layout, BuildGridDefinition grid, ICollection<Vector2Int> outside,
            BuildPieceDefinition definition, Vector2Int cell, int rotation,
            List<BuildPlacement> replaced = null)
        {
            replaced?.Clear();
            if (layout == null || grid == null || definition == null) return PlacementIssue.NoPiece;
            Vector2Int size = BuildLayout.RotatedSize(definition.Footprint, rotation);
            if (cell.x < 0 || cell.y < 0 || cell.x + size.x > grid.Width || cell.y + size.y > grid.Depth) return PlacementIssue.OutsideGrid;
            if (!grid.IsBuildable(cell) || !grid.IsBuildable(cell + size - Vector2Int.one)) return PlacementIssue.OutsideGrid;
            if (outside == null) return PlacementIssue.TowerNeedsWall;
            PlacementIssue best = PlacementIssue.TowerNeedsWall;
            // 미리보기마다 여러 번 부르므로 목록을 다시 쓴다(메인 스레드 전용).
            List<Vector2Int> inner = innerScratch;
            for (int face = 0; face < 4; face++)
            {
                Vector2Int outward = BattlementRules.Directions[face];
                // 바깥 방향으로 두 줄 이상이어야 안쪽 줄과 바깥쪽 줄로 나뉜다.
                if ((outward.x != 0 ? size.x : size.y) < 2) continue;
                inner.Clear();
                bool fits = true;
                for (int x = 0; x < size.x && fits; x++)
                    for (int z = 0; z < size.y && fits; z++)
                    {
                        Vector2Int key = cell + new Vector2Int(x, z);
                        if (Inside(cell, size, key + outward))
                        {
                            // 바깥쪽 줄이 아닌 칸(안쪽 줄 이외의 가운데 줄 포함)은 바깥 쪽 줄 판정에서 뺀다.
                            if (!Inside(cell, size, key - outward)) { inner.Add(key); fits = WallColumn(layout, key); }
                            else fits = false;
                            continue;
                        }
                        // 바깥쪽 줄: 성 바깥 빈 칸.
                        if (layout.ColumnHeight(key) != 0 || layout.IsGateCell(key) || outside == null || !outside.Contains(key)) fits = false;
                    }
                if (!fits || inner.Count == 0) continue;
                // 안쪽 줄의 안쪽 이웃이 성 바깥이면 성벽 줄이 아니다(혼자 선 성벽).
                foreach (Vector2Int key in inner)
                {
                    Vector2Int behind = key - outward;
                    if (behind.x < 0 || behind.y < 0 || behind.x >= grid.Width || behind.y >= grid.Depth || outside.Contains(behind)) fits = false;
                }
                if (!fits) continue;
                if (StairLeansOn(layout, inner)) { best = PlacementIssue.Occupied; continue; }
                if (replaced != null)
                {
                    foreach (Vector2Int key in inner)
                        for (int level = layout.ColumnHeight(key) - 1; level >= 0; level--) replaced.Add(layout.At(key, level));
                    replaced.Sort((a, b) => b.Level.CompareTo(a.Level));
                }
                return PlacementIssue.None;
            }
            return best;
        }

        static readonly List<Vector2Int> innerScratch = new List<Vector2Int>();

        static bool Inside(Vector2Int cell, Vector2Int size, Vector2Int key) =>
            key.x >= cell.x && key.x < cell.x + size.x && key.y >= cell.y && key.y < cell.y + size.y;

        // 그 칸이 한 층 이상이고 모든 층이 고정되지 않은 성벽인지(바꿔 끼울 수 있는 성벽 칸).
        static bool WallColumn(BuildLayout layout, Vector2Int cell)
        {
            int height = layout.ColumnHeight(cell);
            if (height == 0 || layout.IsGateCell(cell)) return false;
            for (int level = 0; level < height; level++)
            {
                BuildPlacement piece = layout.At(cell, level);
                if (piece == null || piece.Definition.Kind != BuildPieceKind.Wall || piece.IsFixed) return false;
            }
            return true;
        }

        // 땅 계단의 높은 쪽이 그 칸들 가운데 하나에 기대어 있는지.
        static bool StairLeansOn(BuildLayout layout, List<Vector2Int> cells)
        {
            foreach (BuildPlacement placement in layout.Placements)
            {
                if (placement.Definition.Kind != BuildPieceKind.Stairs) continue;
                foreach (Vector2Int landing in BuildLayout.Landings(placement.Definition.Footprint, placement.Cell, placement.Rotation))
                    if (cells.Contains(landing)) return true;
            }
            return false;
        }

        /// <summary>
        /// 그 자리에 놓을 때 한 번에 쌓을 층 수. 망루를 새로 놓으면(0층) 정의의 처음 층 수(망루 2층, SIEGE-24)를 격자 층 한도까지,
        /// 망루 위에 쌓으면 한 층. 망루가 아니면 1. 성벽 등급으로 망루 높이를 정하는 규칙(SIEGE-28)은 여기를 바꾼다.
        /// </summary>
        public static int Tiers(BuildGridDefinition grid, BuildPieceDefinition definition, int level)
        {
            if (definition == null || definition.Kind != BuildPieceKind.Tower || level > 0) return 1;
            int limit = grid != null ? grid.MaxLevels : definition.InitialTiers;
            if (definition.MaxStorey > 0) limit = Mathf.Min(limit, definition.MaxStorey);
            return Mathf.Clamp(definition.InitialTiers, 1, Mathf.Max(1, limit - level));
        }

        /// <summary>망루 한 층 방의 짜임(SIEGE-29). <see cref="HasRoom"/>이 거짓이면 방 없이 꽉 찬 층이다(통로와 이어지는 면이 없다).</summary>
        public readonly struct Interior
        {
            public readonly bool HasRoom;
            /// <summary>통로 쪽 면(0 북, 1 동, 2 남, 3 서). 문과 계단이 이 면에 붙는다.</summary>
            public readonly int InnerFace;
            public readonly Vector2Int DoorCell;
            public readonly Vector2Int StairCell;
            /// <summary>계단이 올라가는 쪽(계단 칸에서 문 칸 쪽 면). 바깥벽 쪽 끝에서 출발해 꼭대기가 문 칸 쪽 탁 트인 바닥으로 나온다(SIEGE-29).</summary>
            public readonly int RiseFace;
            /// <summary>가장 낮은 이웃 통로의 바닥 높이. 망루 모든 방 층이 같은 계단 칸을 쌓는 기준이다(SIEGE-29).</summary>
            public readonly float FirstFloor;
            /// <summary>이 층 바닥 높이의 통로와 닿은 면 비트(1 &lt;&lt; 면). 이 면은 문 칸이 아니어도 벽판이다.</summary>
            public readonly int Walkways;
            /// <summary>이 층에 문이 나는 면 비트(1 &lt;&lt; 면). 계단 칸은 문을 내지 않는다.</summary>
            public readonly int Doors;

            public Interior(bool hasRoom, int innerFace, Vector2Int doorCell, Vector2Int stairCell, int riseFace,
                float firstFloor = 0f, int walkways = 0, int doors = 0)
            {
                HasRoom = hasRoom; InnerFace = innerFace; DoorCell = doorCell; StairCell = stairCell; RiseFace = riseFace;
                FirstFloor = firstFloor; Walkways = walkways; Doors = doors;
            }

            public bool HasDoor(int face) => (Doors & (1 << face)) != 0;
            public bool MeetsWalkway(int face) => (Walkways & (1 << face)) != 0;
        }

        /// <summary>
        /// 망루 한 층의 문 칸: 그 면의 칸 가운데 이 층 바닥 높이의 통로와 닿은 첫 칸. 계단 칸에는 문을 내지 않는다(SIEGE-29).
        /// </summary>
        public static bool FaceDoorCell(BuildLayout layout, BuildGridDefinition grid, BuildPlacement tier, Vector2Int stairCell, int face,
            out Vector2Int door)
        {
            door = default;
            if (layout == null || tier == null) return false;
            Vector2Int size = tier.Size, direction = BattlementRules.Directions[face];
            for (int x = 0; x < size.x; x++)
                for (int z = 0; z < size.y; z++)
                {
                    var key = tier.Cell + new Vector2Int(x, z);
                    var next = key + direction;
                    if (next.x >= tier.Cell.x && next.x < tier.Cell.x + size.x && next.y >= tier.Cell.y && next.y < tier.Cell.y + size.y) continue;
                    if (grid != null && (next.x < 0 || next.y < 0 || next.x >= grid.Width || next.y >= grid.Depth)) continue;
                    if (key == stairCell || !Walkway(layout, next, tier.Elevation)) continue;
                    door = key;
                    return true;
                }
            return false;
        }

        // 이웃 통로(성벽·성문 윗면) 가운데 가장 낮은 바닥 높이. 없으면 −1.
        static float LowestWalkway(BuildLayout layout, BuildGridDefinition grid, BuildPlacement tier)
        {
            float lowest = -1f;
            ForEachNeighbour(grid, tier.Cell, tier.Size, (inside, next, face) =>
            {
                BuildPlacement top = layout.Top(next);
                if (top == null || (top.Definition.Kind != BuildPieceKind.Wall && top.Definition.Kind != BuildPieceKind.Gate)) return;
                float surface = top.Elevation + top.Definition.Height;
                if (lowest < 0f || surface < lowest) lowest = surface;
            });
            return lowest;
        }

        /// <summary>
        /// 망루 맨 윗층 방: 바닥 높이가 이웃 성벽 통로와 같은 면을 문 쪽(안쪽)으로 고른다(가장 많이 닿은 쪽, 같으면 방향 순서).
        /// 안쪽 줄 두 칸 가운데 통로와 닿은 첫 칸이 문 칸, 다른 칸이 계단 칸이고 계단은 바깥벽 쪽 끝에서 문 칸 쪽으로 오른다.
        /// </summary>
        public static Interior RoomOf(BuildLayout layout, BuildGridDefinition grid, BuildPlacement top)
        {
            if (layout == null || top == null || top.Definition.Kind != BuildPieceKind.Tower) return default;
            Vector2Int size = top.Size;
            var counts = new int[4];
            // 가장 낮은 통로 바닥부터 위로 모든 층이 방이다. 그 아래 층은 꽉 찬 채 둔다(SIEGE-29).
            float floor = LowestWalkway(layout, grid, top);
            if (floor < 0f || top.Elevation < floor - HeightTolerance) return default;
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
            Vector2Int rise = door - stair;
            int riseFace = 0;
            for (int face = 0; face < 4; face++) if (BattlementRules.Directions[face] == rise) riseFace = face;
            // 이 층 바닥 높이의 통로와 닿은 면과 문 면(층마다 다르다).
            int walkways = 0, doors = 0;
            ForEachNeighbour(grid, top.Cell, size, (inside, next, face) =>
            {
                if (Walkway(layout, next, top.Elevation)) walkways |= 1 << face;
            });
            for (int face = 0; face < 4; face++)
                if ((walkways & (1 << face)) != 0 && FaceDoorCell(layout, grid, top, stair, face, out _)) doors |= 1 << face;
            return new Interior(true, inner, door, stair, riseFace, floor, walkways, doors);
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
