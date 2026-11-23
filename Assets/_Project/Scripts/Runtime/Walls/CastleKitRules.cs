using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 성 킷 조립 규칙(SIEGE-14 시험). 씬을 알지 않는다. PolygonFantasyKingdom 데모 성벽의 짜임을 따른다:
    /// 성벽·보루 칸 한 층마다, 같은 층에 이어진 조각이 없는 면에 벽판(바깥 면은 격자 간격마다 장식 벽판), 벽판 이음새(칸 모서리)마다 돌기둥,
    /// 맨 위에 나무 통로 바닥, 성가퀴 면(<see cref="BattlementRules.Faces"/>)에 흉벽과 모서리 기둥 머리,
    /// 성문 칸에 바깥 틀(문 포함)과 안쪽 틀, 맨 위 높이가 한 층 다른 이웃 사이에 계단.
    /// 무너진 칸(<c>breaches</c>: 칸 → 무너진 가장 낮은 층)의 이웃은 온전하게 두고, 무너진 칸 안에 V자 잔해를 놓는다.
    /// 무너진 보루 칸(<c>rubble</c>)에는 돌무더기만 깔고 이웃 성벽 면은 그대로 둔다.
    /// 위치는 칸 크기 기준이라 킷 모듈(5m)과 칸 크기가 다르면 쓰는 쪽이 조각을 고르게 늘리거나 줄인다.
    /// </summary>
    public static class CastleKitRules
    {
        // 이음새 기둥 하나를 여러 칸이 공유하므로 모서리(격자 점, 층)로 한 번만 놓는다. 값: (주인 칸, 높이).
        static readonly Dictionary<Vector3Int, (Vector2Int owner, float y)> pillars = new Dictionary<Vector3Int, (Vector2Int, float)>();
        static readonly Dictionary<Vector3Int, (Vector2Int owner, float y)> caps = new Dictionary<Vector3Int, (Vector2Int, float)>();
        static readonly HashSet<Vector2Int> columns = new HashSet<Vector2Int>();
        static readonly Dictionary<Vector2Int, StairRoom> roomCells = new Dictionary<Vector2Int, StairRoom>();
        static readonly List<Rect> areas = new List<Rect>();
        const float HeightTolerance = 0.01f;

        /// <summary>면 <paramref name="face"/>(0 북, 1 동, 2 남, 3 서)가 바깥을 보도록 돌리는 각도(°).</summary>
        public static float FaceYaw(int face)
        {
            Vector2Int direction = BattlementRules.Directions[face];
            return Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
        }

        /// <summary>킷으로 꾸미는 성벽류(통로가 있는 조각)인지.</summary>
        public static bool IsWallLike(BuildPieceKind kind) =>
            kind == BuildPieceKind.Wall || kind == BuildPieceKind.Bastion || kind == BuildPieceKind.Gate || kind == BuildPieceKind.Tower;

        /// <param name="fallenTops">무너진 칸 → 무너지기 전 윗면 높이. 그 칸을 향한 계단 방 문을 트인 문으로 남긴다(SIEGE-27).</param>
        /// <param name="rooms">있으면 계단 방 칸(<see cref="StairRoomRules"/>)을 담아 준다(충돌 상자를 끄는 쪽이 쓴다).</param>
        public static void Collect(BuildLayout layout, BuildGridDefinition grid, ICollection<Vector2Int> outside,
            ICollection<int> unfinished, IReadOnlyDictionary<Vector2Int, int> breaches, CastleKitSettings settings,
            List<CastleKitPlacement> result, ICollection<Vector2Int> rubble = null,
            IReadOnlyDictionary<Vector2Int, float> fallenTops = null, IDictionary<Vector2Int, StairRoom> rooms = null)
        {
            result.Clear();
            pillars.Clear();
            caps.Clear();
            columns.Clear();
            roomCells.Clear();
            rooms?.Clear();
            if (layout == null || grid == null || outside == null || settings == null) return;
            float size = grid.CellSize;
            StairRoomRules.Collect(layout, unfinished, fallenTops, roomCells);
            if (rooms != null) foreach (KeyValuePair<Vector2Int, StairRoom> entry in roomCells) rooms[entry.Key] = entry.Value;
            foreach (BuildPlacement placement in layout.Placements)
            {
                if (!IsWallLike(placement.Definition.Kind) || Unfinished(unfinished, placement)) continue;
                if (placement.Definition.Kind == BuildPieceKind.Tower)
                {
                    AddTower(layout, grid, outside, unfinished, breaches, settings, placement, size, result);
                    continue;
                }
                for (int x = 0; x < placement.Size.x; x++)
                    for (int z = 0; z < placement.Size.y; z++)
                    {
                        Vector2Int cell = placement.Cell + new Vector2Int(x, z);
                        columns.Add(cell);
                        if (placement.Definition.Kind == BuildPieceKind.Gate) AddGate(layout, placement, cell, size, result);
                        else AddPanels(layout, grid, outside, unfinished, breaches, settings, placement, cell, size, result);
                        if (layout.Top(cell) == placement) AddTop(layout, grid, outside, breaches, placement, cell, size, result);
                    }
            }
            foreach (KeyValuePair<Vector2Int, StairRoom> entry in roomCells) AddRoom(layout, settings, entry.Key, entry.Value, size, result);
            foreach (Vector2Int cell in columns) AddStairs(layout, grid, outside, unfinished, settings, cell, size, result);
            if (breaches != null)
                foreach (KeyValuePair<Vector2Int, int> breach in breaches) AddBreach(layout, unfinished, breach.Key, size, result);
            if (rubble != null)
                foreach (Vector2Int cell in rubble) AddRubble(layout, unfinished, cell, size, result);
            foreach (KeyValuePair<Vector3Int, (Vector2Int owner, float y)> entry in pillars)
                result.Add(new CastleKitPlacement(CastleKitPiece.Pillar, entry.Value.owner,
                    new Vector3(entry.Key.x * size, entry.Value.y, entry.Key.z * size), 0f));
            foreach (KeyValuePair<Vector3Int, (Vector2Int owner, float y)> entry in caps)
                result.Add(new CastleKitPlacement(CastleKitPiece.BattlementCap, entry.Value.owner,
                    new Vector3(entry.Key.x * size, entry.Value.y, entry.Key.z * size), 0f));
        }

        static bool Unfinished(ICollection<int> unfinished, BuildPlacement placement) =>
            unfinished != null && unfinished.Contains(placement.Id);

        /// <summary>
        /// 그 칸·층 쪽 면을 비워 둘지: 같은 층에 다 지은 조각(계단 제외)이 붙어 있거나, 그 칸이 그 층 이상에서 무너졌다.
        /// </summary>
        public static bool Covered(BuildLayout layout, ICollection<int> unfinished, IReadOnlyDictionary<Vector2Int, int> breaches,
            Vector2Int next, int level)
        {
            BuildPlacement neighbour = layout.At(next, level);
            if (neighbour != null && neighbour.Definition.Kind != BuildPieceKind.Stairs && !Unfinished(unfinished, neighbour)) return true;
            return breaches != null && breaches.TryGetValue(next, out int from) && level >= from;
        }

        static void AddPanels(BuildLayout layout, BuildGridDefinition grid, ICollection<Vector2Int> outside, ICollection<int> unfinished,
            IReadOnlyDictionary<Vector2Int, int> breaches, CastleKitSettings settings, BuildPlacement placement, Vector2Int cell,
            float size, List<CastleKitPlacement> result)
        {
            float y = placement.Elevation;
            // 계단 방 층(SIEGE-27)은 조각 상자 대신 킷 벽판이 벽 충돌을 맡는다: 같은 모양의 충돌체 벽판, 통로 쪽 문, 이웃과 맞붙은 면의 안벽.
            bool room = roomCells.TryGetValue(cell, out StairRoom stairRoom) && stairRoom.Contains(placement.Level);
            for (int face = 0; face < 4; face++)
            {
                Vector2Int next = cell + BattlementRules.Directions[face];
                // 무너진 칸을 향한 면도 비운다. 그 칸에 놓이는 V자 잔해가 이 면을 덮고, 이 칸은 온전하게 남는다.
                if (Covered(layout, unfinished, breaches, next, placement.Level))
                {
                    // 맞붙은 면에는 안벽을 둔다. 이웃도 같은 층이 계단 방이면 앞선 칸만 둔다(같은 자리에 두 장 겹치지 않게).
                    if (room && layout.At(next, placement.Level) != null
                        && (!roomCells.TryGetValue(next, out StairRoom other) || !other.Contains(placement.Level) || Before(cell, next)))
                    {
                        FaceEnds(cell, face, size, out Vector3 a, out Vector3 b);
                        result.Add(new CastleKitPlacement(CastleKitPiece.TowerPanel, cell, Pivot(face, a, b, y), FaceYaw(face)));
                    }
                    continue;
                }
                bool outer = BattlementRules.IsOutside(grid, outside, next);
                int along = face % 2 == 0 ? cell.x : cell.y;
                bool door = room && stairRoom.HasDoor(placement.Level, face);
                CastleKitPiece piece = outer && Mod(along, settings.AccentSpacingCells) == 0 ? CastleKitPiece.PanelAccent : CastleKitPiece.Panel;
                if (door) piece = CastleKitPiece.TowerDoor;
                else if (room && piece == CastleKitPiece.Panel) piece = CastleKitPiece.TowerPanel;
                FaceEnds(cell, face, size, out Vector3 start, out Vector3 end);
                Claim(pillars, start, end, size, placement.Level, cell, y);
                result.Add(new CastleKitPlacement(piece, cell, Pivot(face, start, end, y), FaceYaw(face)));
                // 성 안쪽 면 맨 윗층에는 데모처럼 돋을새김 띠(Wall_Panel_Stone_02)를 덧댄다. 문 위에는 두지 않는다.
                if (!outer && !door && layout.Top(cell) == placement)
                {
                    float drop = settings.InnerReliefDrop * size / settings.ModuleSize;
                    float top = y + placement.Definition.Height;
                    result.Add(new CastleKitPlacement(CastleKitPiece.InnerRelief, cell, Pivot(face, start, end, top - drop), FaceYaw(face)));
                }
            }
        }

        /// <summary>
        /// 무너진 칸(데모의 무너진 성벽처럼 V자). 이웃 성벽은 온전하게 남고, 이 칸 안에서 각 이웃 성벽 끝부터 칸 가운데로
        /// 들쭉날쭉하게 내려오는 반쪽을 놓아 가운데가 땅까지 트인 V자가 된다(사용자 요청 2026-11-22, 적이 그 틈으로 들어온다).
        /// 반쪽마다: 성벽 두께 양쪽 면에 부서진 벽판(DestroyedWall_Top_01, 이음새에서 5m → 칸 가운데에서 0m),
        /// 두 면 사이에 같은 기울기의 단면(DestroyedWall_Rubble_Top_01). 무너진 높이가 여러 층이면 세로로 늘린다.
        /// 땅까지 무너졌으면 가운데에 낮은 돌무더기를 깐다. 모두 외형 전용이라 길을 막지 않는다.
        /// </summary>
        static void AddBreach(BuildLayout layout, ICollection<int> unfinished, Vector2Int cell, float size, List<CastleKitPlacement> result)
        {
            float baseY = Mathf.Max(0f, TopOf(layout, unfinished, cell, out _));
            var center = new Vector3((cell.x + 0.5f) * size, baseY, (cell.y + 0.5f) * size);
            float thickness = 0.25f * size / 5f;
            Vector3 pileAxis = Vector3.zero;
            for (int side = 0; side < 4; side++)
            {
                Vector2Int direction = BattlementRules.Directions[side];
                float high = TopOf(layout, unfinished, cell + direction, out _);
                // 이 칸 성벽에 기대어 있던 계단은 남아 틈을 막는다(사용자 결정 2026-11-23). 계단 끝에도 같은 무너진 단면을 붙인다.
                bool stair = high < 0f && LeansOn(layout, unfinished, cell + direction, cell, out high);
                if (high <= baseY + HeightTolerance) continue;
                float scaleY = (high - baseY) / size;
                // 모델의 로컬 +x가 이음새에서 칸 가운데 쪽(무너진 쪽)을 보게 한다. 로컬 +z는 성벽 두께 방향이다.
                var toward = new Vector3(-direction.x, 0f, -direction.y);
                var across = new Vector3(-toward.z, 0f, toward.x);
                float yaw = Mathf.Atan2(-toward.z, toward.x) * Mathf.Rad2Deg;
                FaceEnds(cell, side, size, out Vector3 start, out Vector3 end);
                Vector3 first = Vector3.Dot(end - start, across) > 0f ? start : end;
                Vector3 cap = first - across * thickness;
                cap.y = baseY;
                result.Add(new CastleKitPlacement(CastleKitPiece.RubbleTop, cell, cap, yaw, false, scaleY));
                // 계단 쪽은 단면만 둔다(계단 옆면은 이웃 성벽 V자 반쪽의 벽판과 겹친다).
                if (stair) continue;
                foreach (Vector3 joint in new[] { start, end })
                {
                    // 부서진 벽판은 로컬 z 0~0.5 두께라, 면(이음새 끝) 위에 두께 가운데가 오도록 물린다.
                    Vector3 anchor = joint - across * thickness;
                    anchor.y = baseY;
                    result.Add(new CastleKitPlacement(CastleKitPiece.BrokenTop, cell, anchor, yaw, false, scaleY));
                }
                if (pileAxis == Vector3.zero) pileAxis = toward;
            }
            if (pileAxis == Vector3.zero || baseY > HeightTolerance) return;
            AddPile(cell, center, pileAxis, size, result);
        }

        /// <summary>
        /// 무너진 보루 칸: 돌무더기만 깐다. 보루는 성벽 바깥에 덧붙은 조각이라, 무너져도 뒤의 성벽은 온전하다
        /// (V자 반쪽을 성벽 면에 기대어 놓으면 다시 지은 성벽까지 부서져 보인다, 사용자 보고 2026-11-23).
        /// 그 칸에 무엇이든 다시 지으면 쓰는 쪽이 목록에서 뺀다.
        /// </summary>
        static void AddRubble(BuildLayout layout, ICollection<int> unfinished, Vector2Int cell, float size, List<CastleKitPlacement> result)
        {
            if (layout.Top(cell) != null) return;
            var center = new Vector3((cell.x + 0.5f) * size, 0f, (cell.y + 0.5f) * size);
            // 돌무더기 길이를 붙어 있던 성벽과 나란히 둔다(성벽 쪽을 향하는 축). 성벽이 없으면 남북 축.
            Vector3 axis = Vector3.forward;
            for (int side = 0; side < 4; side++)
            {
                Vector2Int direction = BattlementRules.Directions[side];
                if (TopOf(layout, unfinished, cell + direction, out _) < 0f) continue;
                axis = new Vector3(direction.x, 0f, direction.y);
                break;
            }
            AddPile(cell, center, axis, size, result);
        }

        // 돌무더기 모델: 로컬 x로 길게(한 모듈), 로컬 −z로 쌓인다. 길이를 축과 직각(성벽 두께 방향)에 두고, 쌓인 쪽을 칸 가운데에 맞춘다.
        static void AddPile(Vector2Int cell, Vector3 center, Vector3 axis, float size, List<CastleKitPlacement> result)
        {
            var pileAcross = new Vector3(-axis.z, 0f, axis.x);
            float pileYaw = Mathf.Atan2(-pileAcross.z, pileAcross.x) * Mathf.Rad2Deg;
            Vector3 pileLocalZ = Quaternion.Euler(0f, pileYaw, 0f) * Vector3.forward;
            Vector3 pivot = center + pileLocalZ * (1.48f * size / 5f);
            // 낮게 깔아(0.5배, 약 0.9m) 틈으로 드나드는 유닛이 돌무더기에 묻혀 보이지 않게 한다.
            result.Add(new CastleKitPlacement(CastleKitPiece.RubblePile, cell, pivot, pileYaw, false, 0.5f));
        }

        /// <summary>
        /// <paramref name="cell"/>의 맨 위가 다 지은 계단이고 그 계단의 높은 쪽(착지면)이 <paramref name="target"/> 칸을 향하는지.
        /// 그러면 계단 꼭대기 높이를 <paramref name="top"/>에 준다.
        /// </summary>
        static bool LeansOn(BuildLayout layout, ICollection<int> unfinished, Vector2Int cell, Vector2Int target, out float top)
        {
            top = -1f;
            BuildPlacement stairs = layout.Top(cell);
            if (stairs == null || stairs.Definition.Kind != BuildPieceKind.Stairs || Unfinished(unfinished, stairs)) return false;
            foreach (Vector2Int landing in BuildLayout.Landings(stairs.Definition.Footprint, stairs.Cell, stairs.Rotation))
            {
                if (landing != target) continue;
                top = stairs.Elevation + stairs.Definition.Height;
                return true;
            }
            return false;
        }

        // 성문: 회전 방향(0 북, 1 동…)이 성 바깥 쪽이다. 바깥 면에 문이 든 틀, 맞은편 면에 문 없는 틀,
        // 두 틀 사이 통로 양옆에는 통로 쪽을 보는 벽판을 세워 이웃 성벽 속(바깥 벽판과 안쪽 벽판 사이 빈 곳)이 보이지 않게 한다.
        static void AddGate(BuildLayout layout, BuildPlacement placement, Vector2Int cell, float size, List<CastleKitPlacement> result)
        {
            int outer = (placement.Rotation % 4 + 4) % 4, inner = (outer + 2) % 4;
            float y = placement.Elevation;
            // 위층이 얹혀 있으면(기본 2층 성문) 통로 천장으로 나무 바닥을 둔다. 맨 위면 AddTop이 통로 바닥을 둔다.
            if (layout.Top(cell) != placement)
                result.Add(new CastleKitPlacement(CastleKitPiece.Floor, cell,
                    new Vector3((cell.x + 1) * size, y + placement.Definition.Height, (cell.y + 1) * size), 0f));
            FaceEnds(cell, outer, size, out Vector3 start, out Vector3 end);
            result.Add(new CastleKitPlacement(CastleKitPiece.GateOuter, cell, Pivot(outer, start, end, y), FaceYaw(outer)));
            FaceEnds(cell, inner, size, out start, out end);
            result.Add(new CastleKitPlacement(CastleKitPiece.GateInner, cell, Pivot(inner, start, end, y), FaceYaw(inner)));
            foreach (int side in new[] { (outer + 1) % 4, (outer + 3) % 4 })
            {
                int facing = (side + 2) % 4;
                FaceEnds(cell, side, size, out start, out end);
                result.Add(new CastleKitPlacement(CastleKitPiece.Panel, cell, Pivot(facing, start, end, y), FaceYaw(facing)));
            }
        }

        static void AddTop(BuildLayout layout, BuildGridDefinition grid, ICollection<Vector2Int> outside,
            IReadOnlyDictionary<Vector2Int, int> breaches, BuildPlacement placement, Vector2Int cell, float size,
            List<CastleKitPlacement> result)
        {
            float top = placement.Elevation + placement.Definition.Height;
            // 나무 바닥 기준점은 칸의 +x·+z 모서리다(모듈이 −x·−z 쪽으로 펼쳐진다). 계단 방 칸의 통로 바닥은 AddRoom이 구멍을 뚫어 둔다.
            if (!roomCells.ContainsKey(cell))
                result.Add(new CastleKitPlacement(CastleKitPiece.Floor, cell, new Vector3((cell.x + 1) * size, top, (cell.y + 1) * size), 0f));
            int faces = BattlementRules.Faces(layout, grid, outside, cell);
            for (int face = 0; face < 4; face++)
            {
                if ((faces & (1 << face)) == 0) continue;
                // 무너진 칸을 향한 면에는 흉벽을 세우지 않는다(그 칸의 V자 잔해가 이어진다).
                if (breaches != null && breaches.ContainsKey(cell + BattlementRules.Directions[face])) continue;
                FaceEnds(cell, face, size, out Vector3 start, out Vector3 end);
                Claim(caps, start, end, size, placement.Level, cell, top);
                result.Add(new CastleKitPlacement(CastleKitPiece.Battlement, cell, Pivot(face, start, end, top), FaceYaw(face)));
            }
        }

        static float TopOf(BuildLayout layout, ICollection<int> unfinished, Vector2Int cell, out BuildPlacement top)
        {
            top = layout.Top(cell);
            if (top == null || !IsWallLike(top.Definition.Kind) || Unfinished(unfinished, top)) return -1f;
            return top.Elevation + top.Definition.Height;
        }

        /// <summary>
        /// 그 칸 맨 위에서 한 층 높은 이웃(성벽류)으로 오르는 계단. 칸마다 한 번 정한다(사용자 결정 2026-11-22, 방식 A).
        /// <list type="bullet">
        /// <item>골짜기(앞뒤가 모두 한 층 높음): 직선 계단 둘을 통로 폭 양쪽 절반에 나란히, 서로 반대 방향으로 놓아 V자로 만나지 않게 한다.</item>
        /// <item>모서리(뒤쪽은 끊기고 옆 한쪽만 같은 높이로 이어짐): 층참에서 꺾이는 계단을 놓아 들어오는 통로 쪽에서 오르기 시작한다.</item>
        /// <item>높은 줄과 낮은 줄이 나란히 붙은 구간의 가운데 칸(양옆이 같은 높이, 뒤쪽 끊김)에는 놓지 않는다(구간 양 끝에만).</item>
        /// <item>그 밖(직선 통로 끝 등)은 통로 가운데에 직선 계단.</item>
        /// </list>
        /// </summary>
        static void AddStairs(BuildLayout layout, BuildGridDefinition grid, ICollection<Vector2Int> outside, ICollection<int> unfinished,
            CastleKitSettings settings, Vector2Int cell, float size, List<CastleKitPlacement> result)
        {
            float low = TopOf(layout, unfinished, cell, out _);
            if (low < 0f) return;
            for (int face = 0; face < 4; face++)
            {
                Vector2Int direction = BattlementRules.Directions[face];
                if (!Higher(layout, unfinished, cell + direction, low)) continue;
                int sideFace = (face + 1) % 4;
                Vector2Int across = BattlementRules.Directions[sideFace];
                if (Higher(layout, unfinished, cell - direction, low))
                {
                    // 골짜기: 같은 옆쪽(+) 거리만큼 비키면 두 계단의 옆 방향이 반대라 서로 다른 절반에 놓인다.
                    float lateral = size * 0.25f;
                    result.Add(Stair(settings, cell, face, size, low, lateral));
                    result.Add(Stair(settings, cell, (face + 2) % 4, size, low, lateral));
                    return;
                }
                bool behind = Same(layout, unfinished, cell - direction, low);
                bool sideA = Same(layout, unfinished, cell + across, low), sideB = Same(layout, unfinished, cell - across, low);
                if (sideA && sideB && !behind) continue;
                if (!behind && sideA != sideB)
                {
                    result.Add(CornerStair(cell, face, sideA ? sideFace : (face + 3) % 4, size, low));
                    return;
                }
                // 같은 높이 통로는 없고 옆 한쪽에서 계단 방 문으로만 들어오는 모서리(2층-1층 모서리-3층, 사용자 보고 2026-11-23):
                // 꺾임 계단을 문 쪽에서 시작하게 두되, 문이 면 가운데라 계단 끝이 아닌 옆으로 올라타도록 옆판 없는 경사 충돌과 길찾기 연결을 둔다.
                bool doorA = DoorInto(layout, cell, cell + across, low), doorB = DoorInto(layout, cell, cell - across, low);
                if (!behind && !sideA && !sideB && doorA != doorB)
                {
                    AddSideEntryCornerStair(settings, cell, face, doorA ? sideFace : (face + 3) % 4, size, low, result);
                    return;
                }
                result.Add(Stair(settings, cell, face, size, low, 0f));
                return;
            }
        }

        // 꺾임 계단 모델(Stairs_Wood_Corner_01, 모듈 기준 로컬 좌표): 아래 계단은 x 4.08(바닥) → 1.8(반 층), z 0 ~ −1.9 띠,
        // 층참은 x 0 ~ 1.8 · z 0 ~ −1.9, 위 계단은 x 0 ~ 1.8 띠를 z −1.9(반 층) → −5(한 층).
        const float CornerLowerStart = 4.08f, CornerLowerEnd = 1.8f, CornerBand = 1.9f, CornerTopEnd = 5f;

        /// <summary>
        /// 옆으로 올라타는 꺾임 계단: 외형은 같은 모델(충돌체 없음), 충돌은 옆판 없는 경사 판 둘과 층참 판(<see cref="CastleKitPiece.RoomCollider"/>),
        /// 문 앞 바닥에서 아래 계단 시작부로 길찾기 연결 하나.
        /// </summary>
        static void AddSideEntryCornerStair(CastleKitSettings settings, Vector2Int cell, int face, int armFace, float size, float low,
            List<CastleKitPlacement> result)
        {
            CastleKitPlacement visual = CornerStair(cell, face, armFace, size, low);
            result.Add(new CastleKitPlacement(CastleKitPiece.CornerStairsOpen, cell, visual.Position, visual.Yaw, visual.MirrorZ));
            float scale = size / settings.ModuleSize, s = scale * settings.CornerStairStretch;
            Quaternion turn = Quaternion.Euler(0f, visual.Yaw, 0f);
            Vector3 Local(float x, float y, float z) => visual.Position + turn * new Vector3(x * s, y * scale, (visual.MirrorZ ? -z : z) * s);
            float half = 2.5f, ramp = settings.RoomRampThickness * scale;
            float midBand = -CornerBand * 0.5f;
            result.Add(Ramp(cell, Local(CornerLowerStart, 0f, midBand), Local(CornerLowerEnd, half, midBand), CornerBand * s, ramp));
            // 모델 위 끝은 늘린 만큼 높은 칸 위로 약 0.6m 걸치지만, 경사 판은 칸 경계에서 높은 칸 윗면 높이가 되게 한다(높은 칸 옆면에 걸리지 않게).
            float edge = CornerTopEnd / settings.CornerStairStretch;
            result.Add(Ramp(cell, Local(CornerLowerEnd * 0.5f, half, -CornerBand), Local(CornerLowerEnd * 0.5f, CornerTopEnd, -edge),
                CornerLowerEnd * s, ramp));
            Vector3 a = Local(0f, half, 0f), b = Local(CornerLowerEnd, half, -CornerBand);
            result.Add(Slab(cell, Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z)),
                low + half * scale, settings.RoomSlabThickness * scale));
            // 문 앞 바닥(아래 계단 띠 바깥)에서 아래 계단 시작부 위로.
            const float boardX = 3.6f;
            float boardY = half * (CornerLowerStart - boardX) / (CornerLowerStart - CornerLowerEnd);
            result.Add(Link(cell, Local(boardX, 0f, -CornerBand - 1f), Local(boardX, boardY, midBand), 0f));
        }

        // next가 이 칸을 향해 height 높이에 문이 난 계단 방인지.
        static bool DoorInto(BuildLayout layout, Vector2Int cell, Vector2Int next, float height)
        {
            if (!roomCells.TryGetValue(next, out StairRoom room)) return false;
            Vector2Int back = cell - next;
            for (int face = 0; face < 4; face++)
            {
                if (BattlementRules.Directions[face] != back) continue;
                for (int level = room.BaseLevel; level <= room.TopLevel; level++)
                {
                    BuildPlacement storey = layout.At(next, level);
                    if (storey != null && Mathf.Abs(storey.Elevation - height) <= HeightTolerance) return room.HasDoor(level, face);
                }
            }
            return false;
        }

        static bool Same(BuildLayout layout, ICollection<int> unfinished, Vector2Int cell, float height) =>
            Mathf.Abs(TopOf(layout, unfinished, cell, out _) - height) <= HeightTolerance;

        // 그 칸 맨 위가 성벽류이고 정확히 한 층 높은지. 망루는 안쪽 계단·문으로 오르므로 통로에 계단을 두지 않는다.
        // 3층 이상 성벽 칸으로도 바깥 계단을 두지 않는다(계단 방이 맡는다). 바깥 계단은 1층↔2층만이다(SIEGE-27).
        static bool Higher(BuildLayout layout, ICollection<int> unfinished, Vector2Int cell, float low)
        {
            float high = TopOf(layout, unfinished, cell, out BuildPlacement upper);
            return high >= 0f && upper.Definition.Kind != BuildPieceKind.Tower && !StairRoomRules.IsTallWall(layout, unfinished, cell)
                && Mathf.Abs(high - low - upper.Definition.Height) <= HeightTolerance;
        }

        /// <summary>
        /// 3층 성벽 칸의 계단 방(SIEGE-27). 방 층마다 같은 꺾인 계단(<see cref="StairWell"/>)을 쌓는다:
        /// 반 층 계단 둘(외형)과 경사 충돌, 층참(바닥 조각 + 판), 천장(위층 바닥·지붕 통로)은 둘째 계단 위를 뚫은 바닥 조각 + 판.
        /// 맨 아래 방 층 바닥은 그 아래 꽉 찬 층의 윗면이라 바닥 조각만 둔다. 벽·문은 <see cref="AddPanels"/>가 둔다.
        /// </summary>
        static void AddRoom(BuildLayout layout, CastleKitSettings settings, Vector2Int cell, StairRoom room, float size,
            List<CastleKitPlacement> result)
        {
            float scale = size / settings.ModuleSize;
            float slab = settings.RoomSlabThickness * scale, ramp = settings.RoomRampThickness * scale;
            var whole = Rect.MinMaxRect(cell.x * size, cell.y * size, (cell.x + 1) * size, (cell.y + 1) * size);
            for (int level = room.BaseLevel; level <= room.TopLevel; level++)
            {
                BuildPlacement storey = layout.At(cell, level);
                float floor = storey.Elevation, height = storey.Definition.Height;
                StairWell well = StairWell.Create(cell, size, floor, height, room.FootFace, room.FirstFlightFace,
                    settings.RoomWallInset * scale, settings.RoomLandingDepth * scale, settings.RoomHoleOverhang * scale,
                    settings.RoomFlightWidth * scale);
                if (level == room.BaseLevel)
                    result.Add(FloorArea(cell, whole, floor, size));
                result.Add(RoomStairs(settings, cell, well.FirstBottom, well.FirstTop, well.FlightWidth, size));
                CastleKitPlacement second = RoomStairs(settings, cell, well.SecondBottom, well.SecondTop, well.FlightWidth, size);
                result.Add(second);
                // 둘째 계단 모델의 기둥은 층참 높이까지만 내려온다. 바닥까지 나무 기둥으로 잇는다(사용자 보고 2026-11-23, 공중에 뜬 기둥).
                AddPost(settings, cell, second, settings.StairPostNear, floor, well.LandingHeight, size, result);
                AddPost(settings, cell, second, settings.StairPostFar, floor, well.LandingHeight, size, result);
                result.Add(Ramp(cell, well.FirstBottom, well.FirstTop, well.FlightWidth, ramp));
                result.Add(Ramp(cell, well.SecondBottom, well.SecondTop, well.FlightWidth, ramp));
                result.Add(FloorArea(cell, well.Landing, well.LandingHeight, size));
                result.Add(Slab(cell, well.Landing, well.LandingHeight, slab));
                // 꺾이는 곳은 벽과 계단 사이 낭떠러지에 끼어 길찾기 메시가 끊길 수 있어 연결로 잇는다(맨 아래 방 층 발치는 바닥이 꽉 차 있다).
                result.Add(Link(cell, well.LandingFirst, well.LandingSecond, 0f));
                if (level > room.BaseLevel) result.Add(Link(cell, well.FootFirst, well.FootSecond, 0f));
                areas.Clear();
                well.CeilingAreas(areas);
                foreach (Rect area in areas)
                {
                    result.Add(FloorArea(cell, area, well.Ceiling, size));
                    result.Add(Slab(cell, area, well.Ceiling, slab));
                }
            }
        }

        // 계단 모델 로컬 (x, z)의 기둥 아래에 floor → top 높이의 나무 기둥(기준점 바닥 가운데).
        static void AddPost(CastleKitSettings settings, Vector2Int cell, CastleKitPlacement stairs, Vector2 post, float floor, float top,
            float size, List<CastleKitPlacement> result)
        {
            float scale = size / settings.ModuleSize;
            Vector3 local = new Vector3(post.x * scale * stairs.StretchX, 0f, post.y * scale * stairs.StretchZ);
            Vector3 point = stairs.Position + Quaternion.Euler(0f, stairs.Yaw, 0f) * local;
            point.y = floor;
            // 계단 모델 기둥과 같은 굵기로(계단을 줄인 만큼 가로·세로로 줄인다).
            result.Add(new CastleKitPlacement(CastleKitPiece.RoomPost, cell, point, stairs.Yaw, false,
                (top - floor) / (settings.RoomPostHeight * scale), stairs.StretchX * settings.RoomPostThickness, stairs.StretchZ * settings.RoomPostThickness));
        }

        // 나무 바닥(Floor_Wood_01: 기준점 +x·+z 모서리에서 −x·−z로 한 모듈)을 사각형에 맞춰 늘린다.
        static CastleKitPlacement FloorArea(Vector2Int cell, Rect area, float y, float size) =>
            new CastleKitPlacement(CastleKitPiece.Floor, cell, new Vector3(area.xMax, y, area.yMax), 0f,
                stretchX: area.width / size, stretchZ: area.height / size);

        // a ↔ b 양방향 길찾기 연결(평평한 곳을 가로지른다). 폭은 a → b와 직각 방향이다.
        static CastleKitPlacement Link(Vector2Int cell, Vector3 a, Vector3 b, float width)
        {
            Vector3 flat = b - a;
            flat.y = 0f;
            float length = flat.magnitude;
            Vector3 along = flat / Mathf.Max(1e-4f, length);
            float yaw = Mathf.Atan2(-along.z, along.x) * Mathf.Rad2Deg;
            return new CastleKitPlacement(CastleKitPiece.RoomLink, cell, (a + b) * 0.5f, yaw, stretchX: length, stretchZ: width);
        }

        // 윗면이 top 높이인 판 충돌.
        static CastleKitPlacement Slab(Vector2Int cell, Rect area, float top, float thickness) =>
            new CastleKitPlacement(CastleKitPiece.RoomCollider, cell, new Vector3(area.center.x, top - thickness * 0.5f, area.center.y), 0f,
                scaleY: thickness, stretchX: area.width, stretchZ: area.height);

        // 디딤면 가운데 선(bottom → top) 아래에 붙인 경사 판 충돌. 로컬 +x가 오르는 쪽, 로컬 z축으로 기울인다.
        static CastleKitPlacement Ramp(Vector2Int cell, Vector3 bottom, Vector3 top, float width, float thickness)
        {
            Vector3 flat = top - bottom;
            float rise = flat.y;
            flat.y = 0f;
            float run = flat.magnitude;
            Vector3 along = flat / Mathf.Max(1e-4f, run);
            float yaw = Mathf.Atan2(-along.z, along.x) * Mathf.Rad2Deg;
            float tilt = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            Vector3 normal = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, tilt) * Vector3.up;
            Vector3 center = (bottom + top) * 0.5f - normal * (thickness * 0.5f);
            return new CastleKitPlacement(CastleKitPiece.RoomCollider, cell, center, yaw, false, thickness,
                Mathf.Sqrt(run * run + rise * rise), width, tilt);
        }

        // 반 층 계단 외형(Stairs_Wood_Straight_01: 기준점은 높은 끝·낮은 높이, 로컬 +x로 한 모듈 내려가고 한 모듈 오르며, 로컬 −z로 폭).
        static CastleKitPlacement RoomStairs(CastleKitSettings settings, Vector2Int cell, Vector3 bottom, Vector3 top, float width, float size)
        {
            Vector3 down = bottom - top;
            down.y = 0f;
            float run = down.magnitude;
            down /= Mathf.Max(1e-4f, run);
            float yaw = Mathf.Atan2(-down.z, down.x) * Mathf.Rad2Deg;
            Vector3 side = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 pivot = new Vector3(top.x, bottom.y, top.z) + side * (width * 0.5f);
            float modelWidth = settings.StairWidth * size / settings.ModuleSize;
            return new CastleKitPlacement(CastleKitPiece.RoomStairs, cell, pivot, yaw, false, (top.y - bottom.y) / size,
                run / size, width / modelWidth);
        }

        /// <summary>
        /// 망루(데모 탑과 같은 짜임, 사용자 결정 2026-11-23). 층마다 드러난 면에 벽판(이음새 기둥은 망루 네 모서리에만),
        /// 맨 윗층은 통로 높이의 방: 통로 쪽 면 한 칸에 문, 그 옆 칸에 통로 쪽 벽을 따라 지붕으로 오르는 계단, 나머지 면은 화살 구멍 벽판.
        /// 지붕은 나무 바닥(계단 위는 반 바닥으로 뚫는다), 둘레에 내민 받침과 바깥으로 내민 흉벽, 모서리에 높은 흉벽 기둥.
        /// 맨 윗층 벽판·계단은 충돌체가 있고, 지붕 충돌은 쓰는 쪽(<see cref="CastleKitDresser"/>)이 조각 위에 판으로 둔다.
        /// </summary>
        static void AddTower(BuildLayout layout, BuildGridDefinition grid, ICollection<Vector2Int> outside, ICollection<int> unfinished,
            IReadOnlyDictionary<Vector2Int, int> breaches, CastleKitSettings settings, BuildPlacement placement, float size,
            List<CastleKitPlacement> result)
        {
            bool topTier = layout.Top(placement.Cell) == placement;
            TowerRules.Interior room = topTier ? TowerRules.RoomOf(layout, grid, placement) : default;
            float y = placement.Elevation;
            for (int x = 0; x < placement.Size.x; x++)
                for (int z = 0; z < placement.Size.y; z++)
                {
                    Vector2Int cell = placement.Cell + new Vector2Int(x, z);
                    for (int face = 0; face < 4; face++)
                    {
                        if (Covered(layout, unfinished, breaches, cell + BattlementRules.Directions[face], placement.Level)) continue;
                        CastleKitPiece piece = CastleKitPiece.Panel;
                        if (topTier)
                        {
                            if (room.HasRoom && face == room.InnerFace)
                                piece = cell == room.DoorCell ? CastleKitPiece.TowerDoor : CastleKitPiece.TowerPanel;
                            else piece = CastleKitPiece.TowerArrowslit;
                        }
                        FaceEnds(cell, face, size, out Vector3 start, out Vector3 end);
                        foreach (Vector3 joint in new[] { start, end })
                            if (IsCorner(placement, joint, size)) Claim(pillars, joint, joint, size, placement.Level, cell, y);
                        result.Add(new CastleKitPlacement(piece, cell, Pivot(face, start, end, y), FaceYaw(face)));
                    }
                }
            if (topTier) AddTowerTop(layout, settings, placement, room, size, result);
        }

        static void AddTowerTop(BuildLayout layout, CastleKitSettings settings, BuildPlacement placement, TowerRules.Interior room,
            float size, List<CastleKitPlacement> result)
        {
            float floor = placement.Elevation, top = floor + placement.Definition.Height;
            float scale = size / settings.ModuleSize;
            for (int x = 0; x < placement.Size.x; x++)
                for (int z = 0; z < placement.Size.y; z++)
                {
                    Vector2Int cell = placement.Cell + new Vector2Int(x, z);
                    var corner = new Vector3((cell.x + 1) * size, 0f, (cell.y + 1) * size);
                    if (room.HasRoom) result.Add(new CastleKitPlacement(CastleKitPiece.Floor, cell, corner + Vector3.up * floor, 0f));
                    // 계단 칸 지붕은 통로 쪽 절반을 비워 계단이 지붕으로 나온다.
                    if (room.HasRoom && cell == room.StairCell)
                        result.Add(HalfFloor(cell, -BattlementRules.Directions[room.InnerFace], size, top));
                    else result.Add(new CastleKitPlacement(CastleKitPiece.Floor, cell, corner + Vector3.up * top, 0f));
                    for (int face = 0; face < 4; face++)
                    {
                        Vector2Int next = cell + BattlementRules.Directions[face];
                        if (Inside(placement, next)) continue;
                        // 지붕보다 낮은 쪽 둘레에만(데모처럼 성벽 쪽 면도 포함).
                        BuildPlacement other = layout.Top(next);
                        if (other != null && other.Elevation + other.Definition.Height >= top - HeightTolerance) continue;
                        Vector2Int d = BattlementRules.Directions[face];
                        Vector3 outward = new Vector3(d.x, 0f, d.y) * (settings.TowerOverhang * scale);
                        FaceEnds(cell, face, size, out Vector3 start, out Vector3 end);
                        Vector3 pivot = Pivot(face, start, end, top) + outward;
                        result.Add(new CastleKitPlacement(CastleKitPiece.TowerCorbel, cell, pivot, FaceYaw(face)));
                        result.Add(new CastleKitPlacement(CastleKitPiece.Battlement, cell, pivot, FaceYaw(face)));
                    }
                }
            // 네 모서리: 면 f와 시계 방향 다음 면 사이 모서리의 받침은 FaceYaw(f)로 돌리면 바깥을 본다.
            float x0 = placement.Cell.x * size, z0 = placement.Cell.y * size;
            float x1 = x0 + placement.Size.x * size, z1 = z0 + placement.Size.y * size;
            Vector3[] corners = { new Vector3(x1, top, z1), new Vector3(x1, top, z0), new Vector3(x0, top, z0), new Vector3(x0, top, z1) };
            for (int face = 0; face < 4; face++)
            {
                Vector3 point = corners[face];
                Vector2Int owner = new Vector2Int(Mathf.Min(Mathf.FloorToInt(point.x / size), placement.Cell.x + placement.Size.x - 1),
                    Mathf.Min(Mathf.FloorToInt(point.z / size), placement.Cell.y + placement.Size.y - 1));
                result.Add(new CastleKitPlacement(CastleKitPiece.TowerCorbelCorner, owner, point, FaceYaw(face)));
                Vector2Int a = BattlementRules.Directions[face], b = BattlementRules.Directions[(face + 1) % 4];
                Vector3 diagonal = new Vector3(a.x + b.x, 0f, a.y + b.y) * (settings.TowerCapOffset * scale);
                result.Add(new CastleKitPlacement(CastleKitPiece.TowerCap, owner, point + diagonal, 0f));
            }
            if (!room.HasRoom) return;
            // 방 안 계단: 계단 칸에서 문 칸 반대쪽 면으로 오르고, 통로 쪽 벽을 따라 붙인다(벽 두께 안쪽).
            float width = settings.StairWidth * scale;
            float yaw = FaceYaw(room.RiseFace) + 90f;
            Vector3 side = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector2Int inner = BattlementRules.Directions[room.InnerFace];
            float lateral = size * 0.5f - 0.3f * scale - width * 0.5f;
            if (Vector3.Dot(side, new Vector3(inner.x, 0f, inner.y)) < 0f) lateral = -lateral;
            result.Add(Stair(settings, room.StairCell, room.RiseFace, size, floor, lateral));
        }

        // 반 바닥(Floor_Wood_03: 기준점에서 로컬 −x로 반 모듈, −z로 한 모듈). 칸의 direction 쪽 절반을 덮는다.
        static CastleKitPlacement HalfFloor(Vector2Int cell, Vector2Int direction, float size, float y)
        {
            float yaw = Mathf.Atan2(-direction.y, direction.x) * Mathf.Rad2Deg;
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var center = new Vector3((cell.x + 0.5f) * size, y, (cell.y + 0.5f) * size);
            Vector3 pivot = center + new Vector3(direction.x, 0f, direction.y) * (size * 0.5f) + forward * (size * 0.5f);
            return new CastleKitPlacement(CastleKitPiece.FloorHalf, cell, pivot, yaw);
        }

        static bool Inside(BuildPlacement placement, Vector2Int cell) =>
            cell.x >= placement.Cell.x && cell.y >= placement.Cell.y
            && cell.x < placement.Cell.x + placement.Size.x && cell.y < placement.Cell.y + placement.Size.y;

        // 성 좌표 점이 조각 바닥 사각형의 모서리인지.
        static bool IsCorner(BuildPlacement placement, Vector3 point, float size)
        {
            int px = Mathf.RoundToInt(point.x / size), pz = Mathf.RoundToInt(point.z / size);
            bool edgeX = px == placement.Cell.x || px == placement.Cell.x + placement.Size.x;
            bool edgeZ = pz == placement.Cell.y || pz == placement.Cell.y + placement.Size.y;
            return edgeX && edgeZ;
        }

        // 계단 모델(Stairs_Wood_Straight_01)은 기준점이 꼭대기이고 로컬 +x로 내려가며(한 칸 길이) 로컬 −z로 폭이 펼쳐진다.
        // 그래서 기준점을 높은 이웃과 맞닿은 칸 끝에 두고 로컬 +x가 높은 이웃의 반대쪽을 보게 한다.
        // 기본은 통로 폭 가운데(사용자 요청 2026-11-22), <paramref name="lateral"/>만큼 계단 옆 방향(로컬 +z 쪽)으로 비킨다.
        static CastleKitPlacement Stair(CastleKitSettings settings, Vector2Int cell, int face, float size, float y, float lateral)
        {
            Vector2Int direction = BattlementRules.Directions[face];
            float yaw = FaceYaw(face) + 90f;
            Vector3 side = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            float width = settings.StairWidth * size / settings.ModuleSize;
            var center = new Vector3((cell.x + 0.5f) * size, y, (cell.y + 0.5f) * size);
            Vector3 pivot = center + new Vector3(direction.x, 0f, direction.y) * (size * 0.5f) + side * (width * 0.5f + lateral);
            return new CastleKitPlacement(CastleKitPiece.WallStairs, cell, pivot, yaw);
        }

        /// <summary>
        /// 모서리 칸의 꺾임 계단(Stairs_Wood_Corner_01). 모델(모듈 기준): 기준점은 칸 한 모서리, 아래 계단은 로컬 +x 끝(x≈4)에서
        /// 시작해 −x로 오르고(z 0~−1.9 띠), x 0~1.8의 2.5m 층참에서 꺾여 위 계단이 −z로 올라 z=−5 끝에서 5m에 닿는다.
        /// 그래서 로컬 +x를 들어오는 통로 쪽(<paramref name="armFace"/>), 로컬 −z를 높은 이웃 쪽(<paramref name="face"/>)으로 돌린다.
        /// 회전만으로 두 축을 맞출 수 없으면 로컬 z를 뒤집는다. 기준점은 통로 반대쪽·높은 이웃 반대쪽 모서리(바깥 모서리)다.
        /// </summary>
        static CastleKitPlacement CornerStair(Vector2Int cell, int face, int armFace, float size, float y)
        {
            Vector2Int d = BattlementRules.Directions[face], a = BattlementRules.Directions[armFace];
            var arm = new Vector3(a.x, 0f, a.y);
            var up = new Vector3(d.x, 0f, d.y);
            float yaw = Mathf.Atan2(-arm.z, arm.x) * Mathf.Rad2Deg;
            Vector3 localZ = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            bool mirror = Vector3.Dot(localZ, up) > 0f;
            float half = size * 0.5f;
            var center = new Vector3((cell.x + 0.5f) * size, y, (cell.y + 0.5f) * size);
            return new CastleKitPlacement(CastleKitPiece.CornerStairs, cell, center - arm * half - up * half, yaw, mirror);
        }

        // 면 양 끝(성 좌표, 높이 0). 면 순서: 0 북(+z), 1 동(+x), 2 남(−z), 3 서(−x).
        static void FaceEnds(Vector2Int cell, int face, float size, out Vector3 start, out Vector3 end)
        {
            float x0 = cell.x * size, x1 = (cell.x + 1) * size, z0 = cell.y * size, z1 = (cell.y + 1) * size;
            switch (face)
            {
                case 0: start = new Vector3(x0, 0f, z1); end = new Vector3(x1, 0f, z1); break;
                case 1: start = new Vector3(x1, 0f, z0); end = new Vector3(x1, 0f, z1); break;
                case 2: start = new Vector3(x0, 0f, z0); end = new Vector3(x1, 0f, z0); break;
                default: start = new Vector3(x0, 0f, z0); end = new Vector3(x0, 0f, z1); break;
            }
        }

        // 벽판·흉벽·성문 모델은 기준점에서 로컬 −x로 한 모듈 펼쳐진다. 그래서 면 양 끝 중 로컬 +x(오른쪽) 쪽 끝이 기준점이다.
        static Vector3 Pivot(int face, Vector3 start, Vector3 end, float y)
        {
            Vector3 right = Quaternion.Euler(0f, FaceYaw(face), 0f) * Vector3.right;
            Vector3 pivot = Vector3.Dot(end - start, right) > 0f ? end : start;
            pivot.y = y;
            return pivot;
        }

        static void Claim(Dictionary<Vector3Int, (Vector2Int owner, float y)> corners, Vector3 start, Vector3 end, float size,
            int level, Vector2Int cell, float y)
        {
            foreach (Vector3 point in new[] { start, end })
            {
                var key = new Vector3Int(Mathf.RoundToInt(point.x / size), level, Mathf.RoundToInt(point.z / size));
                // 주인은 가장 작은 칸(x, 다음 z)이다. 어느 칸 순서로 모아도 같은 결과가 된다.
                if (corners.TryGetValue(key, out (Vector2Int owner, float y) existing) && Before(existing.owner, cell)) continue;
                corners[key] = (cell, y);
            }
        }

        static bool Before(Vector2Int a, Vector2Int b) => a.x < b.x || (a.x == b.x && a.y < b.y);

        static int Mod(int value, int divisor) => ((value % divisor) + divisor) % divisor;
    }
}
