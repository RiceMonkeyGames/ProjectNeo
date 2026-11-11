using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Walls
{
    public enum PlacementIssue
    {
        None = 0, OutsideGrid = 1, Occupied = 2, ReservedGate = 3,
        MissingLanding = 4, UnitInTheWay = 5, NoPiece = 6,
    }

    public sealed class BuildPlacement
    {
        public int Id { get; }
        public BuildPieceDefinition Definition { get; }
        public Vector2Int Cell { get; }
        public int Rotation { get; }
        public bool IsFixed { get; }
        public Vector2Int Size => BuildLayout.RotatedSize(Definition.Footprint, Rotation);
        public BuildPlacement(int id, BuildPieceDefinition definition, Vector2Int cell, int rotation, bool isFixed)
        {
            Id = id; Definition = definition; Cell = cell;
            Rotation = ((rotation % 4) + 4) % 4; IsFixed = isFixed;
        }
    }

    /// <summary>격자 점유·회전·배치 검사. 씬과 외형은 알지 않는다.</summary>
    public sealed class BuildLayout
    {
        readonly BuildGridDefinition grid;
        readonly Dictionary<Vector2Int, BuildPlacement> occupied = new Dictionary<Vector2Int, BuildPlacement>();
        readonly Dictionary<int, BuildPlacement> placements = new Dictionary<int, BuildPlacement>();
        readonly HashSet<Vector2Int> reserved = new HashSet<Vector2Int>();
        int nextId = 1;
        bool gateRegistered;
        public IReadOnlyCollection<BuildPlacement> Placements => placements.Values;
        public BuildLayout(BuildGridDefinition grid) => this.grid = grid;

        public static Vector2Int RotatedSize(Vector2Int size, int rotation) =>
            rotation % 2 == 0 ? size : new Vector2Int(size.y, size.x);

        public Vector2Int WorldToCell(Vector3 localPosition) => new Vector2Int(
            Mathf.FloorToInt(localPosition.x / grid.CellSize), Mathf.FloorToInt(localPosition.z / grid.CellSize));

        public Vector3 Center(Vector2Int cell, Vector2Int size) =>
            new Vector3((cell.x + size.x * 0.5f) * grid.CellSize, 0f, (cell.y + size.y * 0.5f) * grid.CellSize);

        public BuildPlacement At(Vector2Int cell) => occupied.TryGetValue(cell, out var value) ? value : null;
        public BuildPlacement Find(int id) => placements.TryGetValue(id, out var value) ? value : null;

        public PlacementIssue Check(BuildPieceDefinition definition, Vector2Int cell, int rotation, bool fixedGate = false)
        {
            if (definition == null) return PlacementIssue.NoPiece;
            Vector2Int size = RotatedSize(definition.Footprint, rotation);
            if (cell.x < 0 || cell.y < 0 || cell.x + size.x > grid.Width || cell.y + size.y > grid.Depth)
                return PlacementIssue.OutsideGrid;
            if (definition.Kind == BuildPieceKind.Gate && (!fixedGate || gateRegistered)) return PlacementIssue.ReservedGate;
            for (int x = 0; x < size.x; x++)
                for (int z = 0; z < size.y; z++)
                {
                    var key = cell + new Vector2Int(x, z);
                    if (reserved.Contains(key)) return PlacementIssue.ReservedGate;
                    if (occupied.ContainsKey(key)) return PlacementIssue.Occupied;
                }
            return PlacementIssue.None;
        }

        public bool HasLanding(BuildPlacement placement)
        {
            foreach (Vector2Int landing in Landings(placement.Definition.Footprint, placement.Cell, placement.Rotation))
            {
                BuildPlacement support = At(landing);
                if (support == null || support.Definition.Kind == BuildPieceKind.Stairs
                    || !Mathf.Approximately(support.Definition.Height, placement.Definition.Height)) return false;
            }
            return true;
        }

        public bool TryPlace(BuildPieceDefinition definition, Vector2Int cell, int rotation,
            out BuildPlacement placement, bool fixedGate = false)
        {
            placement = null;
            if (Check(definition, cell, rotation, fixedGate) != PlacementIssue.None) return false;
            placement = new BuildPlacement(nextId++, definition, cell, rotation, fixedGate);
            placements.Add(placement.Id, placement);
            for (int x = 0; x < placement.Size.x; x++)
                for (int z = 0; z < placement.Size.y; z++)
                {
                    var key = cell + new Vector2Int(x, z);
                    occupied.Add(key, placement);
                    if (fixedGate) reserved.Add(key);
                }
            if (fixedGate) gateRegistered = true;
            return true;
        }

        public bool Remove(int id, bool destroyed = false)
        {
            if (!placements.TryGetValue(id, out var placement) || (placement.IsFixed && !destroyed)) return false;
            for (int x = 0; x < placement.Size.x; x++)
                for (int z = 0; z < placement.Size.y; z++) occupied.Remove(placement.Cell + new Vector2Int(x, z));
            placements.Remove(id);
            return true;
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
