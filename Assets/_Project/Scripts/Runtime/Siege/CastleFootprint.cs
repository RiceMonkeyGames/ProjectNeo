using System.Collections.Generic;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 공성 적이 쓰는 성 안팎 판정(SIEGE-15). 씬을 알지 않는다. 준비 중 배치가 바뀔 때(<c>layoutRevision</c>)나
    /// 적이 오는 방향(<c>outward</c>)이 바뀔 때만 다시 세므로 전투 중 무너짐으로는 바뀌지 않는다.
    /// <list type="bullet">
    /// <item>성 범위: 성벽·성문 조각이 차지한 칸을 모두 담는 사각형을, 적이 오는 반대쪽(-outward)으로 격자 끝까지 늘린 것.
    /// 시작부터 틈이 난 성과, 한 줄 성벽만 있는 시험 장면의 성 안쪽도 성 안으로 본다.</item>
    /// <item>바깥 칸: 격자 가장자리에서 빈 칸을 따라 닿는 칸(성가퀴와 같은 기준). 궁수가 설 면의 방향을 고를 때 쓴다.</item>
    /// </list>
    /// </summary>
    public sealed class CastleFootprint
    {
        readonly HashSet<Vector2Int> outside = new HashSet<Vector2Int>();
        BuildGridDefinition grid;
        int revision = -1;
        Vector2Int outwardSeen;
        // 성 범위(늘린 사각형)와, 테두리를 뺀 안쪽 판정에서 격자 끝까지 늘린 쪽은 테두리로 보지 않는다.
        Vector2Int min, max;
        bool openMinX, openMaxX, openMinZ, openMaxZ;

        /// <summary>성벽·성문이 하나라도 있는지. 없으면 성 범위가 없다.</summary>
        public bool HasCastle { get; private set; }
        /// <summary>성벽·성문 사각형(늘리기 전)의 중심(성 기준 x·z, 미터).</summary>
        public Vector2 Center { get; private set; }

        /// <summary>다음 <see cref="Refresh"/>에서 반드시 다시 세게 한다(다른 성을 보게 되었을 때).</summary>
        public void Invalidate()
        {
            grid = null;
            revision = -1;
        }

        /// <summary>지금 배치로 성 범위와 바깥 칸을 맞춘다. 같은 격자·배치 리비전·방향이면 아무것도 하지 않는다.</summary>
        public void Refresh(BuildLayout current, BuildGridDefinition definition, int layoutRevision, Vector2Int outward)
        {
            if (current == null || definition == null) return;
            if (ReferenceEquals(grid, definition) && layoutRevision == revision && outward == outwardSeen) return;
            grid = definition;
            revision = layoutRevision;
            outwardSeen = outward;
            BattlementRules.CollectOutside(current, definition, outside);
            HasCastle = false;
            min = new Vector2Int(int.MaxValue, int.MaxValue);
            max = new Vector2Int(int.MinValue, int.MinValue);
            foreach (BuildPlacement placement in current.Placements)
            {
                BuildPieceKind kind = placement.Definition.Kind;
                if (kind != BuildPieceKind.Wall && kind != BuildPieceKind.Gate) continue;
                HasCastle = true;
                min = Vector2Int.Min(min, placement.Cell);
                max = Vector2Int.Max(max, placement.Cell + placement.Size - Vector2Int.one);
            }
            openMinX = openMaxX = openMinZ = openMaxZ = false;
            if (!HasCastle)
            {
                Center = Vector2.zero;
                return;
            }
            Center = new Vector2((min.x + max.x + 1) * 0.5f, (min.y + max.y + 1) * 0.5f) * definition.CellSize;
            // 적은 +outward 쪽에서 온다. 반대쪽은 성의 뒤이므로 격자 끝까지 성 안으로 본다.
            if (outward.x > 0) { min.x = 0; openMinX = true; }
            else if (outward.x < 0) { max.x = definition.Width - 1; openMaxX = true; }
            if (outward.y > 0) { min.y = 0; openMinZ = true; }
            else if (outward.y < 0) { max.y = definition.Depth - 1; openMaxZ = true; }
        }

        /// <summary>성 범위 안(테두리 칸 포함)인지. 돌파구 대상 아군인지 볼 때 쓴다.</summary>
        public bool IsWithinCastle(Vector2Int cell) =>
            HasCastle && cell.x >= min.x && cell.x <= max.x && cell.y >= min.y && cell.y <= max.y;

        /// <summary>
        /// 성 범위의 테두리 칸(격자 끝까지 늘린 쪽은 빼고)을 뺀 안쪽인지. 공격자가 이미 성 안에 들어왔는지 볼 때 쓴다.
        /// 성문 앞이나 성벽 틈 칸에 선 공격자는 아직 안으로 보지 않는다.
        /// </summary>
        public bool IsInsideCastle(Vector2Int cell) =>
            IsWithinCastle(cell)
            && (openMinX || cell.x > min.x) && (openMaxX || cell.x < max.x)
            && (openMinZ || cell.y > min.y) && (openMaxZ || cell.y < max.y);

        /// <summary>격자 밖이거나 바깥 칸인지. 아직 맞추지 않았으면 모두 바깥으로 본다.</summary>
        public bool IsOutside(Vector2Int cell) => grid == null || BattlementRules.IsOutside(grid, outside, cell);

        /// <summary>
        /// 조각이 있는 칸에서 바깥 칸과 맞닿은 면의 비트(비트 i = <see cref="BattlementRules.Directions"/>[i]).
        /// 바깥 칸이거나 조각이 없는 칸이면 0.
        /// </summary>
        public int OutwardFaceMask(BuildLayout current, Vector2Int cell)
        {
            if (current == null || IsOutside(cell) || current.ColumnHeight(cell) == 0) return 0;
            int mask = 0;
            for (int i = 0; i < BattlementRules.Directions.Length; i++)
                if (IsOutside(cell + BattlementRules.Directions[i])) mask |= 1 << i;
            return mask;
        }
    }
}
