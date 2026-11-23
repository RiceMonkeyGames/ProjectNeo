using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 3층 성벽으로 오르는 계단 방 규칙(SIEGE-27, Q-59, 사용자 결정 2026-11-23). 씬을 알지 않는다.
    /// <list type="bullet">
    /// <item>성벽은 실제처럼 속이 꽉 차 있고, 계단이 있는 칸만 계단 방이다(사용자 결정 2026-11-23, 절충안).
    /// 맞붙은 3층 이상 성벽 칸 무리마다, 1층·2층 통로(성벽·보루·성문 맨 위, 망루 제외)와 맞닿은 칸 가운데 줄 끝 칸(무리 안 이웃이 하나 이하)이
    /// 계단 방이다. 그런 끝 칸이 없으면 맞닿은 칸 가운데 가장 앞선 칸(x, 다음 z). 나머지 칸은 꽉 찬 채로 두고 꼭대기 통로로만 잇는다.</item>
    /// <item>방 층: 그 칸에 맞닿은 가장 낮은 통로 바닥 높이의 층부터 맨 윗층까지. 그 아래층은 꽉 찬 채로 둔다.</item>
    /// <item>문: 층마다, 그 층 바닥과 같은 높이의 통로와 맞닿은 면. 무너진 이웃(<c>fallenTops</c>: 칸 → 무너지기 전 윗면 높이)도
    /// 문을 남긴다(트인 문).</item>
    /// <item>계단(<see cref="StairWell"/>): 모든 방 층이 같은 꺾인 계단을 쌓는다. 두 계단 줄이 양 옆벽에 붙고 그 사이가 발치 면에서 층참 밑까지
    /// 이어진 바닥 통로라, 발치 면과 맞은편 면은 모든 방 층에서 드나든다. 맨 아래 방 층은 바닥이 꽉 차 둘째 계단 쪽 면(계단 밑)으로도 들어온다.
    /// 첫 계단 쪽 면에는 문을 둘 수 없다. 통로 문을 가장 많이 살리는 짜임을 고르고(같으면 면 순서), 맞지 않는 문은 벽판으로 둔다.</item>
    /// <item>3층 이상 성벽 칸으로는 성벽 바깥 계단을 두지 않는다(<see cref="IsTallWall"/>). 바깥 계단은 1층↔2층만이다.</item>
    /// <item>맨 아래가 성문이고 그 위가 모두 성벽인 성문 칸도 성벽 칸과 똑같이 방이 된다(SIEGE-19, 사용자 결정 2026-11-23).
    /// 올린 쇠창살(두께 약 0.2m)은 위층 바깥 벽판 두께(0.5m) 안에 숨고, 방 안쪽은 벽에서 안으로 들어간 곳부터라 계단과 겹치지 않는다.</item>
    /// </list>
    /// </summary>
    public static class StairRoomRules
    {
        /// <summary>방을 두는 성벽 칸의 최소 층 수.</summary>
        public const int MinStoreys = 3;
        const float HeightTolerance = 0.01f;

        // 3층 이상 성벽 칸: 방 층 범위(통로와 맞닿지 않았으면 BaseLevel = int.MaxValue)와 면마다 통로 문이 날 층 비트.
        struct Candidate
        {
            public int BaseLevel, TopLevel;
            public int[] WalkwayLevels;
            public bool Touches => BaseLevel != int.MaxValue;
        }

        static readonly Dictionary<Vector2Int, Candidate> columns = new Dictionary<Vector2Int, Candidate>();
        static readonly Dictionary<Vector2Int, StairRoom> scratch = new Dictionary<Vector2Int, StairRoom>();
        static readonly HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        static readonly List<Vector2Int> group = new List<Vector2Int>();
        static readonly Queue<Vector2Int> queue = new Queue<Vector2Int>();

        public static void Collect(BuildLayout layout, ICollection<int> unfinished, IReadOnlyDictionary<Vector2Int, float> fallenTops,
            IDictionary<Vector2Int, StairRoom> result)
        {
            result.Clear();
            columns.Clear();
            if (layout == null) return;
            foreach (BuildPlacement placement in layout.Placements)
            {
                if (placement.Level != 0 || (placement.Definition.Kind != BuildPieceKind.Wall && placement.Definition.Kind != BuildPieceKind.Gate)) continue;
                for (int x = 0; x < placement.Size.x; x++)
                    for (int z = 0; z < placement.Size.y; z++)
                    {
                        Vector2Int cell = placement.Cell + new Vector2Int(x, z);
                        if (Find(layout, unfinished, fallenTops, cell, out Candidate candidate)) columns[cell] = candidate;
                    }
            }

            visited.Clear();
            foreach (Vector2Int start in columns.Keys)
            {
                if (visited.Contains(start)) continue;
                group.Clear();
                queue.Clear();
                queue.Enqueue(start);
                visited.Add(start);
                while (queue.Count > 0)
                {
                    Vector2Int cell = queue.Dequeue();
                    group.Add(cell);
                    for (int face = 0; face < 4; face++)
                    {
                        Vector2Int next = cell + BattlementRules.Directions[face];
                        if (columns.ContainsKey(next) && visited.Add(next)) queue.Enqueue(next);
                    }
                }
                bool any = false;
                Vector2Int first = default;
                foreach (Vector2Int cell in group)
                {
                    if (!columns[cell].Touches) continue;
                    if (!any || cell.x < first.x || (cell.x == first.x && cell.y < first.y)) first = cell;
                    any = true;
                    if (Neighbours(cell) <= 1) result[cell] = Room(columns[cell]);
                }
                if (any && !AnyIn(result, group)) result[first] = Room(columns[first]);
            }
            columns.Clear();
        }

        /// <summary>그 칸의 계단 방(배치 전체로 정한다, <see cref="Collect"/>).</summary>
        public static bool TryGet(BuildLayout layout, ICollection<int> unfinished, IReadOnlyDictionary<Vector2Int, float> fallenTops,
            Vector2Int cell, out StairRoom room)
        {
            Collect(layout, unfinished, fallenTops, scratch);
            return scratch.TryGetValue(cell, out room);
        }

        /// <summary>
        /// 그 칸이 3층 이상 쌓인 다 지은 성벽 칸인지. 그런 칸으로 오르는 단차는 계단 방이 맡으므로 성벽 바깥 계단을 두지 않는다.
        /// </summary>
        public static bool IsTallWall(BuildLayout layout, ICollection<int> unfinished, Vector2Int cell)
        {
            return layout != null && IsRoomColumn(layout, unfinished, cell, layout.ColumnHeight(cell));
        }

        /// <summary>
        /// 방을 둘 수 있는 기둥인지: 3층 이상이고 모든 층이 다 지은 성벽이다. 맨 아래가 성문이고 그 위가 모두 성벽이어도 된다
        /// (성문 칸, SIEGE-19, 사용자 결정 2026-11-23).
        /// </summary>
        static bool IsRoomColumn(BuildLayout layout, ICollection<int> unfinished, Vector2Int cell, int height)
        {
            if (height < MinStoreys) return false;
            for (int level = 0; level < height; level++)
            {
                BuildPlacement piece = layout.At(cell, level);
                if (piece == null || (unfinished != null && unfinished.Contains(piece.Id))) return false;
                BuildPieceKind kind = piece.Definition.Kind;
                if (kind != BuildPieceKind.Wall && !(level == 0 && kind == BuildPieceKind.Gate)) return false;
            }
            return true;
        }

        static int Neighbours(Vector2Int cell)
        {
            int count = 0;
            for (int face = 0; face < 4; face++) if (columns.ContainsKey(cell + BattlementRules.Directions[face])) count++;
            return count;
        }

        static bool AnyIn(IDictionary<Vector2Int, StairRoom> result, List<Vector2Int> cells)
        {
            foreach (Vector2Int cell in cells) if (result.ContainsKey(cell)) return true;
            return false;
        }

        // 통로 문을 가장 많이 살리는 짜임(발치, 첫 계단 면)을 고르고 맞는 문만 남긴다.
        static StairRoom Room(Candidate candidate)
        {
            int foot = 0, first = 1, bestScore = -1;
            for (int f = 0; f < 4; f++)
                for (int side = 1; side <= 3; side += 2)
                {
                    int a = (f + side) % 4;
                    int score = Kept(candidate, f, a);
                    if (score <= bestScore) continue;
                    bestScore = score; foot = f; first = a;
                }
            int doors = 0;
            for (int face = 0; face < 4; face++)
                for (int level = candidate.BaseLevel; level <= candidate.TopLevel; level++)
                    if ((candidate.WalkwayLevels[face] & (1 << level)) != 0 && Reachable(candidate, foot, first, level, face))
                        doors |= StairRoom.Bit(candidate.BaseLevel, level, face);
            return new StairRoom(candidate.BaseLevel, candidate.TopLevel, foot, first, doors);
        }

        /// <summary>
        /// 그 짜임에서 그 층·면으로 드나들 수 있는지. 발치 면과 맞은편 면은 모든 방 층(가운데 바닥 통로), 둘째 계단 쪽 면은 맨 아래 방 층만
        /// (계단 밑), 첫 계단 쪽 면은 막힌다.
        /// </summary>
        static bool Reachable(Candidate candidate, int foot, int first, int level, int face)
        {
            if (face == foot || face == (foot + 2) % 4) return true;
            return level == candidate.BaseLevel && face != first;
        }

        static int Kept(Candidate candidate, int foot, int first)
        {
            int kept = 0;
            for (int face = 0; face < 4; face++)
                for (int level = candidate.BaseLevel; level <= candidate.TopLevel; level++)
                    if ((candidate.WalkwayLevels[face] & (1 << level)) != 0 && Reachable(candidate, foot, first, level, face)) kept++;
            return kept;
        }

        static bool Find(BuildLayout layout, ICollection<int> unfinished, IReadOnlyDictionary<Vector2Int, float> fallenTops,
            Vector2Int cell, out Candidate candidate)
        {
            candidate = default;
            int height = layout.ColumnHeight(cell);
            if (!IsRoomColumn(layout, unfinished, cell, height)) return false;
            BuildPlacement top = layout.At(cell, height - 1);
            float roof = top.Elevation + top.Definition.Height;

            var faceLevels = new int[4];
            int lowest = int.MaxValue;
            for (int face = 0; face < 4; face++)
            {
                Vector2Int next = cell + BattlementRules.Directions[face];
                float current = Walkway(layout, unfinished, next);
                float fallen = fallenTops != null && fallenTops.TryGetValue(next, out float value) ? value : -1f;
                MarkDoor(layout, cell, height, roof, current, face, faceLevels, ref lowest);
                MarkDoor(layout, cell, height, roof, fallen, face, faceLevels, ref lowest);
            }
            // 통로와 맞닿지 않은 칸은 BaseLevel이 int.MaxValue다(무리의 줄 끝 판정에만 쓰고 방은 되지 않는다).
            candidate = new Candidate { BaseLevel = lowest, TopLevel = height - 1, WalkwayLevels = faceLevels };
            return true;
        }

        // 그 칸 맨 위가 통로(성벽·보루·성문, 다 지음)면 윗면 높이, 아니면 -1. 망루는 자기 방 문으로 잇는다.
        static float Walkway(BuildLayout layout, ICollection<int> unfinished, Vector2Int cell)
        {
            BuildPlacement top = layout.Top(cell);
            if (top == null || (unfinished != null && unfinished.Contains(top.Id))) return -1f;
            BuildPieceKind kind = top.Definition.Kind;
            if (kind != BuildPieceKind.Wall && kind != BuildPieceKind.Bastion && kind != BuildPieceKind.Gate) return -1f;
            return top.Elevation + top.Definition.Height;
        }

        // 통로 바닥 높이 floor가 방 칸의 어느 층 바닥과 같으면 그 면·층에 문 자리를 표시한다.
        static void MarkDoor(BuildLayout layout, Vector2Int cell, int height, float roof, float floor, int face, int[] faceLevels,
            ref int lowest)
        {
            if (floor <= HeightTolerance || floor >= roof - HeightTolerance) return;
            int level = LevelAt(layout, cell, height, floor);
            if (level < 0) return;
            faceLevels[face] |= 1 << level;
            lowest = Mathf.Min(lowest, level);
        }

        static int LevelAt(BuildLayout layout, Vector2Int cell, int height, float floor)
        {
            for (int level = 1; level < height; level++)
                if (Mathf.Abs(layout.At(cell, level).Elevation - floor) <= HeightTolerance) return level;
            return -1;
        }
    }
}
