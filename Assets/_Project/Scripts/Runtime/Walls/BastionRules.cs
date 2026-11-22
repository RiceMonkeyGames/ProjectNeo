using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 보루 배치 규칙(SIEGE-11, 사용자 결정 2026-11-22). 씬을 알지 않는다. <see cref="BuildLayout.Check"/>를 통과한 뒤에 부른다.
    /// 보루는 성 바깥 빈 칸에, 네 옆 중 하나가 같은 층·같은 높이의 성벽 조각과 맞닿게 짓는다. 보루 위에는 보루만 쌓고
    /// 보루는 보루 위에만 쌓는다. 쌓은 층도 그 층에서 성벽과 맞닿아야 한다. 사다리가 서는 칸에는 짓지 않는다.
    /// </summary>
    public static class BastionRules
    {
        const float HeightTolerance = 0.01f;

        /// <param name="outside">격자 가장자리에서 빈 칸만 따라 닿는 칸(<see cref="BattlementRules.CollectOutside"/>).</param>
        /// <param name="ladderLanes">사다리가 서는 칸. 없으면 null.</param>
        /// <param name="level">놓일 층(<see cref="BuildLayout.Check"/>가 돌려준 값).</param>
        public static PlacementIssue Check(BuildLayout layout, BuildGridDefinition grid, ICollection<Vector2Int> outside,
            ICollection<Vector2Int> ladderLanes, BuildPieceDefinition definition, Vector2Int cell, int rotation,
            int level, float elevation)
        {
            if (layout == null || definition == null) return PlacementIssue.None;
            Vector2Int size = BuildLayout.RotatedSize(definition.Footprint, rotation);
            bool bastion = definition.Kind == BuildPieceKind.Bastion;
            for (int x = 0; x < size.x; x++)
                for (int z = 0; z < size.y; z++)
                {
                    Vector2Int key = cell + new Vector2Int(x, z);
                    BuildPlacement below = level > 0 ? layout.At(key, level - 1) : null;
                    bool onBastion = below != null && below.Definition.Kind == BuildPieceKind.Bastion;
                    if (level > 0 && bastion != onBastion) return PlacementIssue.BastionStack;
                    if (!bastion) continue;
                    if (ladderLanes != null && ladderLanes.Contains(key)) return PlacementIssue.LadderLane;
                    if (level == 0 && (outside == null || !outside.Contains(key))) return PlacementIssue.BastionNeedsWall;
                }
            if (!bastion) return PlacementIssue.None;
            return TouchesWall(layout, grid, cell, size, level, elevation) ? PlacementIssue.None : PlacementIssue.BastionNeedsWall;
        }

        /// <summary>
        /// 윗면 높이가 <paramref name="surface"/>인 조각을 그 칸들에 두면, 바깥 둘레 성벽 칸의 맨 위(통로)와 높이가 맞아 걸어서 이어지는지.
        /// 성벽 옆에 보루를 지었는데 성벽이 더 높게 쌓여 있으면 false다. 짓기는 허용하고 알리기만 한다(사용자 결정 2026-11-22).
        /// </summary>
        public static bool JoinsWalkway(BuildLayout layout, BuildGridDefinition grid, Vector2Int cell, Vector2Int size, float surface)
        {
            if (layout == null) return false;
            for (int x = 0; x < size.x; x++)
                for (int z = 0; z < size.y; z++)
                {
                    Vector2Int key = cell + new Vector2Int(x, z);
                    foreach (Vector2Int direction in BattlementRules.Directions)
                    {
                        Vector2Int next = key + direction;
                        if (next.x >= cell.x && next.x < cell.x + size.x && next.y >= cell.y && next.y < cell.y + size.y) continue;
                        if (grid != null && (next.x < 0 || next.y < 0 || next.x >= grid.Width || next.y >= grid.Depth)) continue;
                        BuildPlacement top = layout.Top(next);
                        if (top != null && top.Definition.Kind == BuildPieceKind.Wall
                            && Mathf.Abs(top.Elevation + top.Definition.Height - surface) <= HeightTolerance) return true;
                    }
                }
            return false;
        }

        /// <summary>조각이 차지할 칸들의 바깥 둘레에 같은 층·같은 바닥 높이의 성벽 조각이 있는지.</summary>
        public static bool TouchesWall(BuildLayout layout, BuildGridDefinition grid, Vector2Int cell, Vector2Int size,
            int level, float elevation)
        {
            for (int x = 0; x < size.x; x++)
                for (int z = 0; z < size.y; z++)
                {
                    Vector2Int key = cell + new Vector2Int(x, z);
                    foreach (Vector2Int direction in BattlementRules.Directions)
                    {
                        Vector2Int next = key + direction;
                        if (next.x >= cell.x && next.x < cell.x + size.x && next.y >= cell.y && next.y < cell.y + size.y) continue;
                        if (grid != null && (next.x < 0 || next.y < 0 || next.x >= grid.Width || next.y >= grid.Depth)) continue;
                        BuildPlacement other = layout.At(next, level);
                        if (other != null && other.Definition.Kind == BuildPieceKind.Wall
                            && Mathf.Abs(other.Elevation - elevation) <= HeightTolerance) return true;
                    }
                }
            return false;
        }
    }
}
