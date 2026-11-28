using System.Collections.Generic;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 운반병이 사다리를 걸 수 있는 자리 목록(SIEGE-37). 맨 위 조각이 일반 성벽인 칸의 바깥 면(성가퀴가 서는 면) 중
    /// 앞 칸이 비어 있는 면의 빈틈마다 한 자리다. 보루·탑·성문 면에는 걸지 않는다(사용자 2026-11-28).
    /// 사다리 밑과 위쪽 끝이 모두 길찾기 메시 위에 있는 자리만 넣는다. 배치·길찾기 판이 바뀔 때만 다시 만든다.
    /// </summary>
    public sealed class LadderSlotCatalog
    {
        readonly List<LadderSlot> slots = new List<LadderSlot>();
        readonly List<float> offsets = new List<float>();
        ConstructionWorld world;
        int layoutRevision = -1, navigationRevision = -1;

        public IReadOnlyList<LadderSlot> Slots => slots;

        public void Invalidate() => world = null;

        /// <param name="fallbackOutward">성가퀴 부품이 없는 성에서 쓸 바깥 방향.</param>
        public void Refresh(ConstructionWorld owner, SiegeSettings settings, Vector2Int fallbackOutward)
        {
            if (owner != null && owner == world && layoutRevision == owner.LayoutRevision
                && navigationRevision == owner.NavigationRevision) return;
            world = owner;
            slots.Clear();
            if (owner == null || owner.Layout == null || owner.Grid == null || settings == null) return;
            layoutRevision = owner.LayoutRevision;
            navigationRevision = owner.NavigationRevision;
            var battlements = owner.GetComponent<CastleBattlements>();
            if (battlements != null) battlements.LadderCrenelOffsets(offsets);
            else { offsets.Clear(); offsets.Add(0f); }
            BuildGridDefinition grid = owner.Grid;
            int fallbackFaces = 1 << Mathf.Max(0, BattlementRules.DirectionIndex(fallbackOutward));
            for (int x = 0; x < grid.Width; x++)
                for (int z = 0; z < grid.Depth; z++)
                {
                    var cell = new Vector2Int(x, z);
                    BuildPlacement top = owner.Layout.Top(cell);
                    if (top == null || top.Definition.Kind != BuildPieceKind.Wall) continue;
                    int faces = battlements != null ? battlements.FacesAt(cell) : fallbackFaces;
                    for (int d = 0; d < BattlementRules.Directions.Length; d++)
                    {
                        if ((faces & (1 << d)) == 0) continue;
                        Vector2Int direction = BattlementRules.Directions[d];
                        Vector2Int front = cell + direction;
                        // 앞 칸에 보루·탑 등 조각이 있으면 그 면에는 걸 수 없다.
                        if (InGrid(grid, front) && owner.Layout.Top(front) != null) continue;
                        for (int i = 0; i < offsets.Count; i++)
                            TryAdd(owner, settings, battlements, cell, direction, offsets[i]);
                    }
                }
        }

        void TryAdd(ConstructionWorld owner, SiegeSettings settings, CastleBattlements battlements,
            Vector2Int cell, Vector2Int direction, float offset)
        {
            if (!SiegeLadder.TryDescribeSlot(owner, settings, battlements, cell, direction, offset,
                    out Vector3 basePoint, out Vector3 topPoint)) return;
            if (!NavMesh.SamplePosition(basePoint, out NavMeshHit foot, settings.LadderUseRadius, NavMesh.AllAreas)
                || Mathf.Abs(foot.position.y - basePoint.y) > settings.LadderHeightTolerance) return;
            // 다 오른 뒤 내려설 곳이 없는 자리는 고르지 않는다(SiegeAttackerBrain.IsLadderReachable과 같은 기준).
            if (!NavMesh.SamplePosition(topPoint, out NavMeshHit landing, 1.5f, NavMesh.AllAreas)
                || Mathf.Abs(landing.position.y - topPoint.y) > settings.LadderHeightTolerance) return;
            slots.Add(new LadderSlot(cell, direction, offset, foot.position, topPoint));
        }

        static bool InGrid(BuildGridDefinition grid, Vector2Int cell) =>
            cell.x >= 0 && cell.y >= 0 && cell.x < grid.Width && cell.y < grid.Depth;
    }
}
