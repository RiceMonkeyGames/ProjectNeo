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
        const float HeightTolerance = 0.01f;

        /// <summary>면 <paramref name="face"/>(0 북, 1 동, 2 남, 3 서)가 바깥을 보도록 돌리는 각도(°).</summary>
        public static float FaceYaw(int face)
        {
            Vector2Int direction = BattlementRules.Directions[face];
            return Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
        }

        /// <summary>킷으로 꾸미는 성벽류(통로가 있는 조각)인지.</summary>
        public static bool IsWallLike(BuildPieceKind kind) =>
            kind == BuildPieceKind.Wall || kind == BuildPieceKind.Bastion || kind == BuildPieceKind.Gate;

        public static void Collect(BuildLayout layout, BuildGridDefinition grid, ICollection<Vector2Int> outside,
            ICollection<int> unfinished, IReadOnlyDictionary<Vector2Int, int> breaches, CastleKitSettings settings,
            List<CastleKitPlacement> result, ICollection<Vector2Int> rubble = null)
        {
            result.Clear();
            pillars.Clear();
            caps.Clear();
            columns.Clear();
            if (layout == null || grid == null || outside == null || settings == null) return;
            float size = grid.CellSize;
            foreach (BuildPlacement placement in layout.Placements)
            {
                if (!IsWallLike(placement.Definition.Kind) || Unfinished(unfinished, placement)) continue;
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
            for (int face = 0; face < 4; face++)
            {
                Vector2Int next = cell + BattlementRules.Directions[face];
                // 무너진 칸을 향한 면도 비운다. 그 칸에 놓이는 V자 잔해가 이 면을 덮고, 이 칸은 온전하게 남는다.
                if (Covered(layout, unfinished, breaches, next, placement.Level)) continue;
                bool outer = BattlementRules.IsOutside(grid, outside, next);
                int along = face % 2 == 0 ? cell.x : cell.y;
                CastleKitPiece piece = outer && Mod(along, settings.AccentSpacingCells) == 0 ? CastleKitPiece.PanelAccent : CastleKitPiece.Panel;
                FaceEnds(cell, face, size, out Vector3 start, out Vector3 end);
                Claim(pillars, start, end, size, placement.Level, cell, y);
                result.Add(new CastleKitPlacement(piece, cell, Pivot(face, start, end, y), FaceYaw(face)));
                // 성 안쪽 면 맨 윗층에는 데모처럼 돋을새김 띠(Wall_Panel_Stone_02)를 덧댄다.
                if (!outer && layout.Top(cell) == placement)
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
            // 나무 바닥 기준점은 칸의 +x·+z 모서리다(모듈이 −x·−z 쪽으로 펼쳐진다).
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
                result.Add(Stair(settings, cell, face, size, low, 0f));
                return;
            }
        }

        static bool Same(BuildLayout layout, ICollection<int> unfinished, Vector2Int cell, float height) =>
            Mathf.Abs(TopOf(layout, unfinished, cell, out _) - height) <= HeightTolerance;

        // 그 칸 맨 위가 성벽류이고 정확히 한 층 높은지.
        static bool Higher(BuildLayout layout, ICollection<int> unfinished, Vector2Int cell, float low)
        {
            float high = TopOf(layout, unfinished, cell, out BuildPlacement upper);
            return high >= 0f && Mathf.Abs(high - low - upper.Definition.Height) <= HeightTolerance;
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
