using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Walls
{
    public enum PlacementIssue
    {
        None = 0, OutsideGrid = 1, Occupied = 2, ReservedGate = 3,
        MissingLanding = 4, UnitInTheWay = 5, NoPiece = 6, UnevenSupport = 7,
        /// <summary>받치는 조각이 아직 짓는 중이다(SIEGE-08).</summary>
        UnfinishedSupport = 8,
        /// <summary>보루는 성 바깥 빈 칸에, 같은 층·같은 높이의 성벽에 붙여 지어야 한다(SIEGE-11).</summary>
        BastionNeedsWall = 9,
        // 10은 사다리 자리(SIEGE-11)였다. 사다리 자리가 없어져(SIEGE-37) 비워 둔다.
        /// <summary>보루 위에는 보루만, 보루는 보루 위에만 쌓는다(SIEGE-11).</summary>
        BastionStack = 11,
        /// <summary>이웃 성벽이 앞뒤보다 낮은 골짜기(2층-1층-2층)가 된다(SIEGE-14, <see cref="BuildGridDefinition.ForbidValleys"/>).</summary>
        Valley = 12,
        /// <summary>망루는 성 바깥 빈 칸에, 같은 층·같은 높이의 성벽에 붙여 지어야 한다(SIEGE-14).</summary>
        TowerNeedsWall = 13,
        /// <summary>망루 위에는 망루 층만, 망루 층은 망루 위에만 쌓는다(SIEGE-14).</summary>
        TowerStack = 14,
        /// <summary>계단은 땅 위에만 놓는다(SIEGE-27, <see cref="BuildGridDefinition.GroundStairsOnly"/>).</summary>
        StairsGroundOnly = 15,
        /// <summary>그 조각의 층 제한(<see cref="BuildPieceDefinition.MaxStorey"/>)보다 높아진다(Q-59, SIEGE-18).</summary>
        StoreyCap = 16,
    }

    public sealed class BuildPlacement
    {
        public int Id { get; }
        public BuildPieceDefinition Definition { get; }
        public Vector2Int Cell { get; }
        public int Rotation { get; }
        public bool IsFixed { get; }
        /// <summary>쌓인 층. 땅 위가 0이다. 아래 조각이 부서지면 내려앉으며 줄어든다.</summary>
        public int Level { get; internal set; }
        /// <summary>조각 바닥의 높이. 아래에 쌓인 조각들의 높이 합이다.</summary>
        public float Elevation { get; internal set; }
        public Vector2Int Size => BuildLayout.RotatedSize(Definition.Footprint, Rotation);
        public BuildPlacement(int id, BuildPieceDefinition definition, Vector2Int cell, int rotation, bool isFixed,
            int level = 0, float elevation = 0f)
        {
            Id = id; Definition = definition; Cell = cell;
            Rotation = ((rotation % 4) + 4) % 4; IsFixed = isFixed;
            Level = level; Elevation = elevation;
        }
    }

    /// <summary>
    /// 격자 점유·회전·층 쌓기·배치 검사. 씬과 외형은 알지 않는다.
    /// 조각은 가리킨 칸의 맨 위에 놓인다. 여러 칸을 차지하는 조각은 모든 칸의 받침 높이가 같아야 한다.
    /// 계단 위에는 쌓을 수 없다. 고정 성문 자리는 바닥만 보호하며 성문 위에는 쌓을 수 있다.
    /// </summary>
    public sealed class BuildLayout
    {
        readonly BuildGridDefinition grid;
        // 키는 (x, 층, z).
        readonly Dictionary<Vector3Int, BuildPlacement> occupied = new Dictionary<Vector3Int, BuildPlacement>();
        readonly Dictionary<int, BuildPlacement> placements = new Dictionary<int, BuildPlacement>();
        readonly HashSet<Vector2Int> reserved = new HashSet<Vector2Int>();
        readonly List<BuildPlacement> settleOrder = new List<BuildPlacement>();
        int nextId = 1;
        bool gateRegistered;
        public IReadOnlyCollection<BuildPlacement> Placements => placements.Values;
        public BuildLayout(BuildGridDefinition grid) => this.grid = grid;

        public static Vector2Int RotatedSize(Vector2Int size, int rotation) =>
            rotation % 2 == 0 ? size : new Vector2Int(size.y, size.x);

        static Vector3Int Key(Vector2Int cell, int level) => new Vector3Int(cell.x, level, cell.y);
        static bool CanSupport(BuildPlacement placement) => placement.Definition.Kind != BuildPieceKind.Stairs;

        public Vector2Int WorldToCell(Vector3 localPosition) => new Vector2Int(
            Mathf.FloorToInt(localPosition.x / grid.CellSize), Mathf.FloorToInt(localPosition.z / grid.CellSize));

        public Vector3 Center(Vector2Int cell, Vector2Int size) =>
            new Vector3((cell.x + size.x * 0.5f) * grid.CellSize, 0f, (cell.y + size.y * 0.5f) * grid.CellSize);

        /// <summary>땅 위(0층)의 조각.</summary>
        public BuildPlacement At(Vector2Int cell) => At(cell, 0);
        public BuildPlacement At(Vector2Int cell, int level) =>
            occupied.TryGetValue(Key(cell, level), out var value) ? value : null;
        public BuildPlacement Find(int id) => placements.TryGetValue(id, out var value) ? value : null;
        public bool IsGateCell(Vector2Int cell) => reserved.Contains(cell);

        /// <summary>그 칸에 쌓인 조각 수.</summary>
        public int ColumnHeight(Vector2Int cell)
        {
            int level = 0;
            while (occupied.ContainsKey(Key(cell, level))) level++;
            return level;
        }

        /// <summary>그 칸의 맨 위 조각. 비어 있으면 null.</summary>
        public BuildPlacement Top(Vector2Int cell)
        {
            int height = ColumnHeight(cell);
            return height == 0 ? null : At(cell, height - 1);
        }

        /// <summary>바로 위층에 다른 조각이 얹혀 있는지.</summary>
        public bool IsCovered(BuildPlacement placement)
        {
            for (int x = 0; x < placement.Size.x; x++)
                for (int z = 0; z < placement.Size.y; z++)
                    if (At(placement.Cell + new Vector2Int(x, z), placement.Level + 1) != null) return true;
            return false;
        }

        public PlacementIssue Check(BuildPieceDefinition definition, Vector2Int cell, int rotation, bool fixedGate = false) =>
            Check(definition, cell, rotation, fixedGate, out _, out _);

        /// <summary>놓일 층과 바닥 높이를 함께 돌려준다. 놓을 수 없을 때의 높이는 미리보기 위치용이다.</summary>
        public PlacementIssue Check(BuildPieceDefinition definition, Vector2Int cell, int rotation, bool fixedGate,
            out int level, out float elevation)
        {
            level = 0; elevation = 0f;
            if (definition == null) return PlacementIssue.NoPiece;
            Vector2Int size = RotatedSize(definition.Footprint, rotation);
            if (cell.x < 0 || cell.y < 0 || cell.x + size.x > grid.Width || cell.y + size.y > grid.Depth)
                return PlacementIssue.OutsideGrid;
            // 막힌 동·서 끝 칸(분지 성벽 끝 탑 자리, SIEGE-40)도 격자 밖처럼 거절한다.
            if (!grid.IsBuildable(cell) || !grid.IsBuildable(cell + size - Vector2Int.one)) return PlacementIssue.OutsideGrid;
            if (definition.Kind == BuildPieceKind.Gate && (!fixedGate || gateRegistered)) return PlacementIssue.ReservedGate;
            int common = -1;
            float commonElevation = 0f;
            bool uneven = false;
            for (int x = 0; x < size.x; x++)
                for (int z = 0; z < size.y; z++)
                {
                    var key = cell + new Vector2Int(x, z);
                    int height = ColumnHeight(key);
                    // 성문 위의 적층은 허용하되, 파괴 후 빈 성문 자리에 새 바닥 조각을 놓지는 않는다.
                    if (reserved.Contains(key) && height == 0) return PlacementIssue.ReservedGate;
                    BuildPlacement top = height == 0 ? null : At(key, height - 1);
                    float surface = top == null ? 0f : top.Elevation + top.Definition.Height;
                    elevation = Mathf.Max(elevation, surface);
                    if (height >= grid.MaxLevels || (top != null && !CanSupport(top))) return PlacementIssue.Occupied;
                    if (common < 0) { common = height; commonElevation = surface; }
                    else if (height != common || !Mathf.Approximately(surface, commonElevation)) uneven = true;
                }
            if (uneven) return PlacementIssue.UnevenSupport;
            if (definition.Kind == BuildPieceKind.Stairs && grid.GroundStairsOnly && common > 0)
            {
                level = common; elevation = commonElevation;
                return PlacementIssue.StairsGroundOnly;
            }
            if (definition.Kind == BuildPieceKind.Gate && common > 0) return PlacementIssue.ReservedGate;
            level = common; elevation = commonElevation;
            if (common >= StoreyLimit(definition)) return PlacementIssue.StoreyCap;
            if (grid.ForbidValleys && ValleyFill(definition, cell, rotation, level, elevation) == 0) return PlacementIssue.Valley;
            return PlacementIssue.None;
        }

        // ── 골짜기 규칙(SIEGE-14). 성벽류(성벽·보루·성문) 맨 위 높이만 본다. 빈 칸·계단은 높이 0(골짜기가 아니다). ──

        const float ValleyTolerance = 0.01f;

        /// <summary>그 조각이 쌓일 수 있는 층 수. 격자 층 수와 조각의 층 제한 중 작은 쪽이다.</summary>
        int StoreyLimit(BuildPieceDefinition definition) =>
            definition.MaxStorey > 0 ? Mathf.Min(grid.MaxLevels, definition.MaxStorey) : grid.MaxLevels;

        static bool WallLike(BuildPlacement placement) => placement != null
            && (placement.Definition.Kind == BuildPieceKind.Wall || placement.Definition.Kind == BuildPieceKind.Bastion
                || placement.Definition.Kind == BuildPieceKind.Gate);

        /// <summary>그 칸 맨 위 성벽류의 윗면 높이. 비었거나 맨 위가 계단이면 0.</summary>
        public float WallSurface(Vector2Int cell)
        {
            BuildPlacement top = Top(cell);
            return WallLike(top) ? top.Elevation + top.Definition.Height : 0f;
        }

        float Surface(Vector2Int cell, Vector2Int overrideCell, float overrideHeight) =>
            cell == overrideCell ? overrideHeight : WallSurface(cell);

        // 그 칸이 같은 축 양쪽 이웃보다 모두 낮은 골짜기인지(그 칸 높이만 바꿔 본다).
        bool IsValley(Vector2Int cell, Vector2Int overrideCell, float overrideHeight)
        {
            float height = Surface(cell, overrideCell, overrideHeight);
            if (height <= ValleyTolerance) return false;
            for (int axis = 0; axis < 2; axis++)
            {
                Vector2Int step = axis == 0 ? Vector2Int.right : Vector2Int.up;
                if (Surface(cell + step, overrideCell, overrideHeight) > height + ValleyTolerance
                    && Surface(cell - step, overrideCell, overrideHeight) > height + ValleyTolerance) return true;
            }
            return false;
        }

        static readonly Vector2Int[] Around = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        /// <summary>
        /// 그 자리에 몇 개를 한 번에 쌓아야 골짜기가 생기지 않는지. 1이면 평소대로, 2 이상이면 양옆 높이까지 이어 쌓는다
        /// (무너진 2층 성벽 자리는 2층을 다 다시 짓는다). 어떻게 쌓아도 이웃이 골짜기가 되거나 층 제한을 넘으면 0.
        /// 골짜기 규칙을 쓰지 않거나 한 칸짜리 성벽·보루가 아니면 늘 1.
        /// </summary>
        public int ValleyFill(BuildPieceDefinition definition, Vector2Int cell, int rotation)
        {
            if (Check(definition, cell, rotation, false, out int level, out float elevation) != PlacementIssue.None) return 0;
            return ValleyFill(definition, cell, rotation, level, elevation);
        }

        int ValleyFill(BuildPieceDefinition definition, Vector2Int cell, int rotation, int level, float elevation)
        {
            if (!grid.ForbidValleys || definition.Kind == BuildPieceKind.Stairs || definition.Kind == BuildPieceKind.Gate
                || RotatedSize(definition.Footprint, rotation) != Vector2Int.one) return 1;
            for (int count = 1; level + count <= StoreyLimit(definition); count++)
            {
                float height = elevation + definition.Height * count;
                // 높이를 올리면 이웃 골짜기는 더 깊어질 뿐이라, 이웃이 골짜기가 되면 더 쌓아도 소용없다.
                foreach (Vector2Int step in Around)
                    if (IsValley(cell + step, cell, height)) return 0;
                if (!IsValley(cell, cell, height)) return count;
            }
            return 0;
        }

        /// <summary>그 조각을 빼면(그 칸이 조각 높이만큼 낮아지면) 골짜기가 생기는지. 골짜기 규칙을 쓰지 않으면 false.</summary>
        public bool RemovalMakesValley(BuildPlacement placement)
        {
            if (!grid.ForbidValleys || placement == null || !WallLike(placement)) return false;
            for (int x = 0; x < placement.Size.x; x++)
                for (int z = 0; z < placement.Size.y; z++)
                {
                    Vector2Int cell = placement.Cell + new Vector2Int(x, z);
                    float lowered = Mathf.Max(0f, WallSurface(cell) - placement.Definition.Height);
                    if (IsValley(cell, cell, lowered)) return true;
                }
            return false;
        }

        /// <summary>계단의 높은 쪽이 같은 층·같은 높이의 조각에 닿아 있는지.</summary>
        public bool HasLanding(BuildPlacement placement)
        {
            foreach (Vector2Int landing in Landings(placement.Definition.Footprint, placement.Cell, placement.Rotation))
            {
                BuildPlacement support = At(landing, placement.Level);
                if (support == null || !CanSupport(support)
                    || !Mathf.Approximately(support.Definition.Height, placement.Definition.Height)
                    || !Mathf.Approximately(support.Elevation, placement.Elevation)) return false;
            }
            return true;
        }

        public bool TryPlace(BuildPieceDefinition definition, Vector2Int cell, int rotation,
            out BuildPlacement placement, bool fixedGate = false)
        {
            placement = null;
            if (Check(definition, cell, rotation, fixedGate, out int level, out float elevation) != PlacementIssue.None) return false;
            placement = new BuildPlacement(nextId++, definition, cell, rotation, fixedGate, level, elevation);
            placements.Add(placement.Id, placement);
            Occupy(placement, true);
            if (fixedGate)
            {
                for (int x = 0; x < placement.Size.x; x++)
                    for (int z = 0; z < placement.Size.y; z++) reserved.Add(cell + new Vector2Int(x, z));
                gateRegistered = true;
            }
            return true;
        }

        void Occupy(BuildPlacement placement, bool add)
        {
            for (int x = 0; x < placement.Size.x; x++)
                for (int z = 0; z < placement.Size.y; z++)
                {
                    Vector3Int key = Key(placement.Cell + new Vector2Int(x, z), placement.Level);
                    if (add) occupied.Add(key, placement); else occupied.Remove(key);
                }
        }

        /// <summary>조각을 뺀다. 위에 쌓인 조각의 처리는 이어서 <see cref="Settle"/>을 부른다.</summary>
        public bool Remove(int id, bool destroyed = false)
        {
            if (!placements.TryGetValue(id, out var placement) || (placement.IsFixed && !destroyed)) return false;
            Occupy(placement, false);
            placements.Remove(id);
            return true;
        }

        /// <summary>
        /// 받침을 잃은 조각을 정리한다. 아래가 모두 비었으면 한 층씩 내려앉아 <paramref name="dropped"/>에,
        /// 일부만 받쳐지거나 계단 위에 걸리면 배치에서 빠져 <paramref name="unsupported"/>에 담긴다.
        /// </summary>
        public void Settle(List<BuildPlacement> dropped, List<BuildPlacement> unsupported)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                settleOrder.Clear();
                settleOrder.AddRange(placements.Values);
                settleOrder.Sort((a, b) => a.Level.CompareTo(b.Level));
                foreach (BuildPlacement placement in settleOrder)
                {
                    if (placement.Level == 0) continue;
                    int cells = 0, supported = 0, empty = 0;
                    for (int x = 0; x < placement.Size.x; x++)
                        for (int z = 0; z < placement.Size.y; z++)
                        {
                            BuildPlacement below = At(placement.Cell + new Vector2Int(x, z), placement.Level - 1);
                            cells++;
                            if (below == null) empty++;
                            else if (CanSupport(below)) supported++;
                        }
                    if (supported == cells) continue;
                    changed = true;
                    if (empty == cells)
                    {
                        Occupy(placement, false);
                        placement.Level--;
                        Occupy(placement, true);
                        if (!dropped.Contains(placement)) dropped.Add(placement);
                    }
                    else
                    {
                        Remove(placement.Id, true);
                        dropped.Remove(placement);
                        unsupported.Add(placement);
                    }
                }
            }
            dropped.Sort((a, b) => a.Level.CompareTo(b.Level));
            foreach (BuildPlacement placement in dropped)
            {
                BuildPlacement below = placement.Level == 0 ? null : At(placement.Cell, placement.Level - 1);
                placement.Elevation = below == null ? 0f : below.Elevation + below.Definition.Height;
            }
        }

        public static IEnumerable<Vector2Int> Landings(Vector2Int size, Vector2Int cell, int rotation)
        {
            rotation = ((rotation % 4) + 4) % 4;
            for (int x = 0; x < size.x; x++)
            {
                Vector2Int offset;
                switch (rotation)
                {
                    case 1: offset = new Vector2Int(size.y, size.x - 1 - x); break;
                    case 2: offset = new Vector2Int(size.x - 1 - x, -1); break;
                    case 3: offset = new Vector2Int(-1, x); break;
                    default: offset = new Vector2Int(x, size.y); break;
                }
                yield return cell + offset;
            }
        }
    }
}
