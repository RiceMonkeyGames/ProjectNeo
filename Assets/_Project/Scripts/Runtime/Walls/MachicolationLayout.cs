using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>성문 칸 바깥 면 하나에 놓일 마치콜레이션(SIEGE-12). 좌표는 성(ConstructionWorld) 기준이다.</summary>
    public readonly struct MachicolationSpot
    {
        public readonly Vector2Int Cell;
        /// <summary>면 번호(<see cref="BattlementRules.Directions"/>).</summary>
        public readonly int Face;
        /// <summary>사격 자리(그 칸 맨 위 조각 윗면 위).</summary>
        public readonly Vector3 StandPoint;
        /// <summary>성 바깥쪽 수평 방향.</summary>
        public readonly Vector3 Outward;
        /// <summary>사격 자리에서 성문 바닥까지 높이.</summary>
        public readonly float Drop;

        public MachicolationSpot(Vector2Int cell, int face, Vector3 standPoint, Vector3 outward, float drop)
        {
            Cell = cell; Face = face; StandPoint = standPoint; Outward = outward; Drop = drop;
        }
    }

    /// <summary>
    /// 마치콜레이션 자리 계산(SIEGE-12, 사용자 결정 2026-11-22). 지금은 성문 칸에만 처음부터 있다.
    /// 성문 칸마다 성 바깥을 향한 면(성가퀴가 서는 면) 가운데에 하나씩 놓는다. 면 가운데는 성가퀴의 빈틈이다.
    /// 성문 위에 성벽을 쌓았으면 그 칸 맨 위 조각 윗면에 놓는다.
    /// </summary>
    public static class MachicolationLayout
    {
        public static void Collect(BuildLayout layout, BuildGridDefinition grid, ICollection<Vector2Int> outside,
            float standInset, List<MachicolationSpot> output)
        {
            output.Clear();
            if (layout == null || grid == null) return;
            float size = grid.CellSize;
            float inset = Mathf.Clamp(standInset, 0f, size * 0.5f);
            for (int x = 0; x < grid.Width; x++)
                for (int z = 0; z < grid.Depth; z++)
                {
                    var cell = new Vector2Int(x, z);
                    BuildPlacement gate = layout.At(cell, 0);
                    if (gate == null || gate.Definition.Kind != BuildPieceKind.Gate) continue;
                    BuildPlacement top = layout.Top(cell);
                    if (top == null) continue;
                    int faces = BattlementRules.Faces(layout, grid, outside, cell);
                    if (faces == 0) continue;
                    float surface = top.Elevation + top.Definition.Height;
                    float drop = surface - gate.Elevation;
                    float minX = x * size, minZ = z * size, half = size * 0.5f;
                    for (int face = 0; face < BattlementRules.Directions.Length; face++)
                    {
                        if ((faces & (1 << face)) == 0) continue;
                        Vector2Int direction = BattlementRules.Directions[face];
                        var outward = new Vector3(direction.x, 0f, direction.y);
                        var center = new Vector3(minX + half, surface, minZ + half);
                        Vector3 stand = center + outward * (half - inset);
                        output.Add(new MachicolationSpot(cell, face, stand, outward, drop));
                    }
                }
        }
    }
}
