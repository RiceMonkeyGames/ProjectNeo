using System;
using System.Collections.Generic;
using ProjectNeo.Core;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// 기본 성 안뜰의 겉모습(SIEGE-36). Synty 데모 씬처럼 격자를 드러내지 않게 깐다:
    /// 둥근 풀밭 조각을 겹쳐 깐 땅, 성문·광장·건물·계단을 잇는 굽은 흙길, 가운데 둥근 돌 광장과 분수,
    /// 길을 바라보게 제각각 돌린 시설 건물, 건물 옆 잔소품 무더기, 성벽 쪽 숲과 덤불·풀·바위.
    /// 시설 루트의 충돌 상자·기능 부품은 그대로 두고 Visual 자식만 바꾼다. 무작위는 고정 시드라 여러 번 실행해도 같은 결과다.
    /// 좌표는 지면(ConstructionWorld) 기준 m다.
    /// </summary>
    public static class GameCourtyardArtSetup
    {
        const string Pack = "Assets/ThirdParty/PolygonFantasyKingdom/Prefabs/";
        const string ArtFolder = "Assets/_Project/Art/Courtyard";
        const string MaterialFolder = ArtFolder + "/Materials";
        const string GroundMeshPath = ArtFolder + "/CourtyardGround.asset";
        const string OldFloorMeshPath = ArtFolder + "/CourtyardFloor.asset";
        const string GroundName = "CourtyardGround";
        const string OldFloorName = "CourtyardFloor";
        const string DecorName = "CourtyardDecor";
        const string ModelName = "Model";
        const int Seed = 36;

        /// <summary>시설 이름별 모델, 목표 바닥 크기(m), 자리(지면 기준 m), 입구 방향(도, 0 = 북). 입구는 가까운 길과 광장을 본다.</summary>
        internal static readonly (string building, string model, Vector2 footprint, Vector2 position, float yaw)[] Buildings =
        {
            ("Tavern", "Buildings/Preset_Houses/SM_Bld_Preset_Tavern_01_Optimized", new Vector2(18f, 18f), new Vector2(130f, 166f), 75f),
            ("Shop", "Buildings/Preset_Houses/SM_Bld_Preset_House_03_Optimized", new Vector2(11f, 12f), new Vector2(124f, 224f), 125f),
            ("Training", "Buildings/Preset_Houses/SM_Bld_Preset_Stables_01_Optimized", new Vector2(18f, 11f), new Vector2(116f, 123f), 50f),
            ("Smithy", "Buildings/Preset_Houses/SM_Bld_Preset_Blacksmith_01_Optimized", new Vector2(16f, 13f), new Vector2(226f, 164f), 285f),
            ("Construction", "Buildings/Preset_Houses/SM_Bld_Preset_House_05_Optimized", new Vector2(13f, 13f), new Vector2(230f, 226f), 230f),
            (HeroHallSetup.BuildingName, "Buildings/Preset_Houses/SM_Bld_Preset_Church_01_A_Optimized", new Vector2(13f, 20f), new Vector2(231f, 125f), 315f),
        };

        /// <summary>시설 하나의 자리 정보(지면 기준). NPC 설정(<see cref="GameTownSetup"/>)과 길 잇기에 쓴다.</summary>
        internal readonly struct BuildingSpot
        {
            public readonly string Name;
            public readonly Transform Root;
            public readonly Vector2 Center, Forward, Right, Half;
            public readonly float Yaw;
            public BuildingSpot(string name, Transform root, Vector2 center, float yaw, Vector2 half)
            {
                Name = name; Root = root; Center = center; Yaw = yaw; Half = half;
                Forward = Dir(yaw); Right = Dir(yaw + 90f);
            }
            /// <summary>입구 앞 길 끝(정면 바깥면에서 3m).</summary>
            public Vector2 Entrance => Center + Forward * (Half.y + 3f);
            /// <summary>NPC가 서는 곳: 입구 옆(문을 막지 않게 오른쪽으로 비켜) 정면 바깥.</summary>
            public Vector2 NpcSpot => Center + Forward * (Half.y + 1.4f) + Right * Mathf.Min(Half.x - 1f, 3f);
            public Vector2 Local(float x, float z) => Center + Right * x + Forward * z;
        }

        /// <summary>안뜰 배치 결과: 시설 자리와 시민 산책 지점(지면 기준).</summary>
        internal sealed class Plan
        {
            public readonly List<BuildingSpot> Buildings = new List<BuildingSpot>();
            public readonly List<Vector2> WanderPoints = new List<Vector2>();
            /// <summary>장식용 집 문 앞. 전투 때 주민이 숨는 곳에 더한다.</summary>
            public readonly List<Vector2> HouseDoors = new List<Vector2>();
        }

        [MenuItem("ProjectNeo/Setup/Apply Courtyard Art (SIEGE-36)")]
        public static void ApplyMenu()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            Scene game = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            ConstructionWorld world = GameFacilitySetup.Find<ConstructionWorld>(game)
                ?? throw new InvalidOperationException("ConstructionWorld is missing in the Game scene.");
            Plan plan = Apply(world.transform.Find("Geometry"));
            GameTownSetup.Apply(game, world, plan);
            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Geometry 아래 안뜰에 땅·길·건물 모델·조형물을 놓고 배치 결과를 돌려준다.</summary>
        internal static Plan Apply(Transform geometry)
        {
            Transform layout = geometry.Find("CourtyardLayout") ?? throw new InvalidOperationException("Missing CourtyardLayout.");
            var converted = new Dictionary<Material, Material>();
            var plan = new Plan();
            foreach ((string building, string model, Vector2 footprint, Vector2 position, float yaw) in Buildings)
            {
                Transform facility = layout.Find(building);
                if (facility == null) continue;
                Bounds bounds = DressFacility(facility, model, footprint, position, yaw, converted);
                plan.Buildings.Add(new BuildingSpot(building, facility, position, yaw, new Vector2(bounds.size.x, bounds.size.z) * 0.5f));
            }

            var paths = Paths(plan);
            var random = new SeededRandom(Seed);
            var keepOut = new KeepOut(plan, paths);
            BuildGround(geometry, paths, random, converted);
            PlaceDecor(geometry, plan, paths, keepOut, random, converted);
            PlaceField(geometry, random, converted);
            PlaceSurroundings(geometry, random, converted);
            foreach (List<Vector2> path in paths)
                for (int i = 0; i < path.Count; i += 4)
                    if (!keepOut.NearStairOrSquad(path[i], 4f)) plan.WanderPoints.Add(path[i]);
            return plan;
        }

        static Vector2 Dir(float yaw) => new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
        static float YawOf(Vector2 direction) => Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
        static Vector3 Ground(Vector2 point, float y = 0f) => new Vector3(point.x, y, point.y);

        // ---------- 고정 자리 ----------

        static Vector2 Plaza => new Vector2(GameFlowSetup.AvenueX, GameFlowSetup.PlazaZ);
        const float PlazaRadius = 13f;
        static Vector2 GateInside => new Vector2(GameFlowSetup.AvenueX, GameFlowSetup.WallNorth * GameFlowSetup.CellSize - 4f);
        static Vector2 SouthEnd => new Vector2(GameFlowSetup.AvenueX, (GameFlowSetup.WallSouth + 1) * GameFlowSetup.CellSize + 13f);

        /// <summary>땅 위 계단마다 오르는 발치(안뜰 쪽 칸 끝에서 3.5m).</summary>
        static IEnumerable<Vector2> StairFeet()
        {
            float size = GameFlowSetup.CellSize;
            foreach ((Vector2Int cell, int rotation) in GameFlowSetup.StairSeeds())
            {
                Vector2 center = new Vector2((cell.x + 0.5f) * size, (cell.y + 0.5f) * size);
                Vector2 up = rotation == 0 ? Vector2.up : rotation == 1 ? Vector2.right : rotation == 2 ? Vector2.down : Vector2.left;
                yield return center - up * (size * 0.5f + 3.5f);
            }
        }

        // ---------- 길 ----------

        /// <summary>성문·광장·남쪽 큰길·시설 입구·계단 발치를 잇는 굽은 길들. 각 길은 촘촘히 뽑은 점 목록이다.</summary>
        static List<List<Vector2>> Paths(Plan plan)
        {
            BuildingSpot Spot(string name) => plan.Buildings.Find(b => b.Name == name);
            Vector2 Edge(Vector2 toward) => Plaza + (toward - Plaza).normalized * (PlazaRadius - 1f);
            var feet = new List<Vector2>(StairFeet());
            // StairSeeds 순서: 북 2, 남 2, 동 2, 서 2.
            Vector2 north1 = feet[0], north2 = feet[1], south1 = feet[2], south2 = feet[3];
            Vector2 east1 = feet[4], east2 = feet[5], west1 = feet[6], west2 = feet[7];
            var routes = new List<Vector2[]>
            {
                new[] { GateInside, new Vector2(171f, 246f), new Vector2(180f, 220f), Edge(GateInside) },
                new[] { Edge(SouthEnd), new Vector2(181f, 148f), new Vector2(171.5f, 125f), SouthEnd },
                new[] { SouthEnd, new Vector2(150f, 107f), south1 },
                new[] { SouthEnd, new Vector2(203f, 108f), south2 },
                new[] { new Vector2(172f, 244f), new Vector2(150f, 255f), north1 },
                new[] { new Vector2(175f, 236f), new Vector2(204f, 252f), north2 },
            };
            foreach (BuildingSpot spot in plan.Buildings)
            {
                Vector2 entrance = spot.Entrance;
                Vector2 start = Edge(entrance);
                Vector2 mid = Vector2.Lerp(start, entrance, 0.5f);
                Vector2 side = new Vector2(-(entrance - start).y, (entrance - start).x).normalized;
                // 길이 곧게 뻗지 않게 가운데를 옆으로 민다(시설마다 방향이 다르다).
                float bend = (spot.Name.Length % 2 == 0 ? 1f : -1f) * Mathf.Min(7f, (entrance - start).magnitude * 0.18f);
                routes.Add(new[] { start, mid + side * bend, entrance });
            }
            routes.Add(new[] { Spot("Training").Entrance, new Vector2(103f, 140f), west1 });
            routes.Add(new[] { Spot("Shop").Entrance, new Vector2(104f, 206f), west2 });
            routes.Add(new[] { Spot(HeroHallSetup.BuildingName).Entrance, new Vector2(247f, 141f), east1 });
            routes.Add(new[] { Spot("Construction").Entrance, new Vector2(247f, 207f), east2 });
            routes.Add(new[] { Spot("Shop").Entrance, new Vector2(124f, 243f), north1 });
            routes.Add(new[] { Spot("Construction").Entrance, new Vector2(229f, 246f), north2 });

            var result = new List<List<Vector2>>();
            foreach (Vector2[] route in routes) result.Add(Resample(Spline(route), 1.6f));
            return result;
        }

        /// <summary>Catmull-Rom 곡선으로 점들을 부드럽게 잇는다.</summary>
        static List<Vector2> Spline(Vector2[] points)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 p0 = points[Mathf.Max(0, i - 1)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(points.Length - 1, i + 2)];
                for (int s = 0; s < 16; s++)
                {
                    float t = s / 16f, t2 = t * t, t3 = t2 * t;
                    result.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            result.Add(points[points.Length - 1]);
            return result;
        }

        static List<Vector2> Resample(List<Vector2> line, float step)
        {
            var result = new List<Vector2> { line[0] };
            float carried = 0f;
            for (int i = 1; i < line.Count; i++)
            {
                Vector2 a = line[i - 1], b = line[i];
                float length = Vector2.Distance(a, b);
                float at = step - carried;
                while (at <= length)
                {
                    result.Add(Vector2.Lerp(a, b, at / length));
                    at += step;
                }
                carried = length - (at - step);
            }
            if (Vector2.Distance(result[result.Count - 1], line[line.Count - 1]) > step * 0.5f) result.Add(line[line.Count - 1]);
            return result;
        }

        // ---------- 땅(합친 메시) ----------

        const string GrassTile = "Environments/SM_Env_Ground_Flat_01";
        const string DirtTile = "Environments/SM_Env_Ground_Flat_Dirt_01";
        const string PlazaQuarter = "Castle/SM_Bld_Castle_Floor_Stone_Round_L_01";
        static readonly string[] PathPieces =
        {
            "Environments/SM_Env_Path_01", "Environments/SM_Env_Path_02", "Environments/SM_Env_Path_03",
            "Environments/SM_Env_Path_04", "Environments/SM_Env_Path_05",
        };

        /// <summary>
        /// 풀밭(둥근 조각을 겹쳐 깔고 높이를 조금씩 달리해 깜빡임을 막는다, 건물 둘레는 흙), 굽은 흙길, 둥근 돌 광장을
        /// 렌더러 하나짜리 메시로 합친다. 충돌체는 없다(걷는 바닥은 지면 상자다).
        /// </summary>
        static void BuildGround(Transform geometry, List<List<Vector2>> paths, IRandomSource random, Dictionary<Material, Material> converted)
        {
            var batch = new MeshBatch(converted);
            float x0 = GameFlowSetup.WallWest * GameFlowSetup.CellSize + 4f, x1 = (GameFlowSetup.WallEast + 1) * GameFlowSetup.CellSize - 4f;
            float z0 = GameFlowSetup.WallSouth * GameFlowSetup.CellSize + 4f, z1 = (GameFlowSetup.WallNorth + 1) * GameFlowSetup.CellSize - 4f;
            // 조각(10.4m)보다 조금 좁게 깔아 둥근 가장자리가 겹치게 한다.
            const float step = 9.6f;
            for (float x = x0; x <= x1 + 0.01f; x += step)
                for (float z = z0; z <= z1 + 0.01f; z += step)
                {
                    var point = new Vector2(x + (random.NextFloat() - 0.5f) * 2f, z + (random.NextFloat() - 0.5f) * 2f);
                    // 흙 조각은 네모난 흙빛 판으로 보여 쓰지 않는다(흙빛은 길 조각이 맡는다). 무작위 순서를 지키려고 값은 그대로 뽑는다.
                    bool dirt = random.NextFloat() < 0f;
                    // 안뜰 풀밭은 들판 풀밭(0.004~0.016m)보다 높게 깔아 성벽 밑에서 겹쳐도 깜빡이지 않게 한다.
                    batch.Add(dirt ? DirtTile : GrassTile, point, random.NextInt(4) * 90f + (random.NextFloat() - 0.5f) * 20f,
                        1f, 0.035f + random.NextFloat() * 0.03f);
                }
            foreach (List<Vector2> path in paths)
                // 3.2m마다. 조각은 가운데가 솟은 흙 둔덕이라 풀밭 위로 드러나는 폭이 4~5m뿐이어서 이 간격이어야 길이 끊기지 않는다.
                for (int i = 0; i < path.Count; i += 2)
                    // 길 조각은 돌이 흙 면보다 솟아 있다. 돌 꼭대기 기준 0.3m 위로 올려야 흙 면이 풀밭 위로 나온다.
                    batch.Add(PathPieces[random.NextInt(PathPieces.Length)], path[i], random.NextFloat() * 360f,
                        1.15f + random.NextFloat() * 0.2f, 0.31f + random.NextFloat() * 0.04f);
            for (int quarter = 0; quarter < 4; quarter++)
                batch.Add(PlazaQuarter, Plaza, quarter * 90f, PlazaRadius / 5f, 0.37f, centerPivot: false);

            // 성 밖 들판(SIEGE-36): 지면 상자는 보이지 않으므로(충돌만) 풀밭 조각이 빈틈 없이 덮어야 한다.
            // 맵 안은 3배 조각(31m)을 24m 간격으로 겹쳐 깔고(안뜰 안에 완전히 들어가는 조각은 뺀다), 맵 밖 200m까지는 6배 조각을 더 낮게 깐다.
            float width = GameFlowSetup.GridWidth * GameFlowSetup.CellSize, depth = GameFlowSetup.GridDepth * GameFlowSetup.CellSize;
            const float fieldScale = 3f, fieldStep = 24f, fieldHalf = 15.6f;
            for (float x = 0f; x <= width + 12f; x += fieldStep)
                for (float z = 0f; z <= depth + 12f; z += fieldStep)
                {
                    var point = new Vector2(x + (random.NextFloat() - 0.5f) * 4f, z + (random.NextFloat() - 0.5f) * 4f);
                    if (point.x - fieldHalf > x0 && point.x + fieldHalf < x1 && point.y - fieldHalf > z0 && point.y + fieldHalf < z1) continue;
                    // 큰 흙 조각은 넓은 흙빛 네모로 보여 들판에는 풀밭만 깐다.
                    batch.Add(GrassTile, point, random.NextInt(4) * 90f + (random.NextFloat() - 0.5f) * 20f,
                        fieldScale, 0.004f + random.NextFloat() * 0.012f);
                }
            const float outerScale = 6f, outerStep = 50f, outerHalf = 31f, outerMargin = 220f;
            for (float x = -outerMargin; x <= width + outerMargin; x += outerStep)
                for (float z = -outerMargin; z <= depth + outerMargin; z += outerStep)
                {
                    var point = new Vector2(x + (random.NextFloat() - 0.5f) * 6f, z + (random.NextFloat() - 0.5f) * 6f);
                    if (point.x - outerHalf > 0f && point.x + outerHalf < width && point.y - outerHalf > 0f && point.y + outerHalf < depth) continue;
                    // 큰 흙 조각은 넓은 흙빛 네모로 보여 들판에는 풀밭만 깐다.
                    batch.Add(GrassTile, point, random.NextInt(4) * 90f + (random.NextFloat() - 0.5f) * 20f,
                        outerScale, -0.03f + random.NextFloat() * 0.01f);
                }
            foreach (Vector2 point in FieldRoad())
                batch.Add(PathPieces[random.NextInt(PathPieces.Length)], point, random.NextFloat() * 360f,
                    1.3f + random.NextFloat() * 0.2f, 0.29f + random.NextFloat() * 0.04f);

            Transform old = geometry.Find(OldFloorName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            if (AssetDatabase.LoadAssetAtPath<Mesh>(OldFloorMeshPath) != null) AssetDatabase.DeleteAsset(OldFloorMeshPath);
            batch.Build(geometry, GroundName, GroundMeshPath);
        }

        /// <summary>북쪽 들판 끝에서 성문 바깥까지 굽은 흙길(3.2m 간격 점).</summary>
        static List<Vector2> FieldRoad()
        {
            float gateOut = (GameFlowSetup.WallNorth + 1) * GameFlowSetup.CellSize + 2f;
            // 맵 북쪽 끝을 넘어 숲 띠 안까지 이어져, 길이 숲에서 나와 성문으로 들어가는 것처럼 보인다.
            float edge = GameFlowSetup.GridDepth * GameFlowSetup.CellSize + 45f;
            Vector2[] route =
            {
                new Vector2(GameFlowSetup.AvenueX - 18f, edge), new Vector2(GameFlowSetup.AvenueX - 6f, edge - 88f),
                new Vector2(GameFlowSetup.AvenueX + 7f, gateOut + 40f), new Vector2(GameFlowSetup.AvenueX, gateOut),
            };
            return Resample(Spline(route), 3.2f);
        }

        static readonly List<(Vector2 center, float yaw, Vector2 half)> buildingBoxes = new List<(Vector2, float, Vector2)>();
        static float NearestBuildingDistance(Vector2 point)
        {
            float best = float.MaxValue;
            foreach ((Vector2 center, float yaw, Vector2 half) in buildingBoxes)
            {
                Vector2 local = Rotate(point - center, -yaw);
                Vector2 outside = new Vector2(Mathf.Max(0f, Mathf.Abs(local.x) - half.x), Mathf.Max(0f, Mathf.Abs(local.y) - half.y));
                best = Mathf.Min(best, outside.magnitude);
            }
            return best;
        }

        static Vector2 Rotate(Vector2 v, float yaw)
        {
            // yaw는 북(+z)에서 시계 방향. 지면 (x, z) 평면에서 같은 방향으로 돌린다.
            float r = yaw * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c + v.y * s, -v.x * s + v.y * c);
        }

        /// <summary>프리팹 메시들을 머티리얼별로 모아 하나의 메시 에셋과 렌더러로 만든다.</summary>
        sealed class MeshBatch
        {
            readonly Dictionary<Material, Material> converted;
            readonly Dictionary<Material, List<CombineInstance>> parts = new Dictionary<Material, List<CombineInstance>>();
            readonly List<Material> order = new List<Material>();
            readonly Dictionary<string, (GameObject prefab, Bounds bounds)> cache = new Dictionary<string, (GameObject, Bounds)>();

            public MeshBatch(Dictionary<Material, Material> materials) { converted = materials; }

            /// <summary>
            /// 조각 하나를 <paramref name="point"/>에 놓는다. 윗면이 <paramref name="top"/> 높이가 된다.
            /// <paramref name="centerPivot"/>면 조각 가운데를 점에 맞추고, 아니면 프리팹 원점을 맞춘다(광장 1/4 조각).
            /// </summary>
            public void Add(string model, Vector2 point, float yaw, float scale, float top, bool centerPivot = true)
            {
                if (!cache.TryGetValue(model, out var entry))
                {
                    GameObject prefab = Load(model);
                    entry = (prefab, PrefabMeshBounds(prefab));
                    cache[model] = entry;
                }
                Bounds b = entry.bounds;
                Vector3 offset = centerPivot ? new Vector3(-b.center.x, 0f, -b.center.z) : Vector3.zero;
                offset.y = top / scale - b.max.y;
                Matrix4x4 place = Matrix4x4.TRS(Ground(point), Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale) * Matrix4x4.Translate(offset);
                Transform root = entry.prefab.transform;
                foreach (var filter in entry.prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (filter.sharedMesh == null || renderer == null) continue;
                    Matrix4x4 local = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    Material[] materials = renderer.sharedMaterials;
                    for (int s = 0; s < filter.sharedMesh.subMeshCount; s++)
                    {
                        Material material = CharacterModelSetup.ToUrp(materials[Mathf.Min(s, materials.Length - 1)], converted, MaterialFolder);
                        if (!parts.TryGetValue(material, out var list)) { list = new List<CombineInstance>(); parts[material] = list; order.Add(material); }
                        list.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = s, transform = place * local });
                    }
                }
            }

            public void Build(Transform parent, string name, string assetPath)
            {
                var pieces = new CombineInstance[order.Count];
                var temporary = new List<Mesh>();
                for (int i = 0; i < order.Count; i++)
                {
                    var part = new Mesh { indexFormat = IndexFormat.UInt32 };
                    part.CombineMeshes(parts[order[i]].ToArray(), true, true);
                    temporary.Add(part);
                    pieces[i] = new CombineInstance { mesh = part, transform = Matrix4x4.identity };
                }
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                bool created = mesh == null;
                if (created) mesh = new Mesh();
                mesh.Clear();
                mesh.name = name;
                mesh.indexFormat = IndexFormat.UInt32;
                mesh.CombineMeshes(pieces, false, false);
                mesh.RecalculateBounds();
                foreach (Mesh part in temporary) UnityEngine.Object.DestroyImmediate(part);
                EnsureFolder(ArtFolder);
                if (created) AssetDatabase.CreateAsset(mesh, assetPath);
                else EditorUtility.SetDirty(mesh);

                Transform old = parent.Find(name);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var root = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                root.transform.SetParent(parent, false);
                root.GetComponent<MeshFilter>().sharedMesh = mesh;
                root.GetComponent<MeshRenderer>().sharedMaterials = order.ToArray();
                GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            }
        }

        static Bounds PrefabMeshBounds(GameObject prefab)
        {
            var result = new Bounds();
            bool first = true;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds local = filter.sharedMesh.bounds;
                Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = toRoot.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? local.min.x : local.max.x,
                        (i & 2) == 0 ? local.min.y : local.max.y, (i & 4) == 0 ? local.min.z : local.max.z));
                    if (first) { result = new Bounds(corner, Vector3.zero); first = false; }
                    else result.Encapsulate(corner);
                }
            }
            return result;
        }

        // ---------- 시설 건물 ----------

        /// <summary>
        /// 시설을 자리·방향에 놓고 Visual의 임시 상자를 모델로 바꾼 뒤, 루트 충돌 상자를 모델 바닥 크기에 맞춘다.
        /// 루트 앞(+z)이 입구다(GameUnitSpawner가 이 앞쪽 바깥에 유닛을 세운다). Synty 집 모델도 +z가 정면이다.
        /// </summary>
        static Bounds DressFacility(Transform facility, string modelPath, Vector2 footprint, Vector2 position, float yaw,
            Dictionary<Material, Material> converted)
        {
            facility.localPosition = Ground(position);
            facility.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform visual = facility.Find("Visual");
            if (visual == null)
            {
                visual = new GameObject("Visual").transform;
                visual.SetParent(facility, false);
            }
            for (int i = visual.childCount - 1; i >= 0; i--)
            {
                Transform child = visual.GetChild(i);
                if (child.name == "FrontSign" || child.name == "RoofSign") continue;
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            GameObject model = Spawn(visual, ModelName, modelPath, 0f, converted, keepColliders: false);
            Bounds bounds = Fit(model.transform, visual, footprint);
            bounds = SinkToFoundation(visual, model.transform);
            buildingBoxes.RemoveAll(box => box.center == position);
            buildingBoxes.Add((position, yaw, new Vector2(bounds.size.x, bounds.size.z) * 0.5f));

            var body = facility.GetComponent<BoxCollider>();
            if (body == null) body = facility.gameObject.AddComponent<BoxCollider>();
            // 땅속에 묻힌 부분은 빼고 땅(0)부터 지붕까지만 막는다.
            body.center = new Vector3(0f, bounds.max.y * 0.5f, 0f);
            body.size = new Vector3(bounds.size.x, bounds.max.y, bounds.size.z);
            EditorUtility.SetDirty(body);

            foreach (Transform child in visual)
            {
                if (child.name == "FrontSign")
                {
                    // 정면 바깥. 글은 입구 쪽에서 읽히게 루트와 반대로 돌린다.
                    child.localPosition = new Vector3(0f, Mathf.Min(4f, bounds.max.y * 0.5f), bounds.size.z * 0.5f + 0.1f);
                    child.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    ((RectTransform)child).sizeDelta = new Vector2(bounds.size.x, 3f);
                }
                else if (child.name == "RoofSign")
                {
                    // 건설 시점에서 어느 건물이든 같은 방향(북쪽이 위)으로 읽히게 세계 기준으로 눕힌다.
                    child.rotation = facility.parent.rotation * Quaternion.Euler(90f, 0f, 0f);
                    child.localPosition = new Vector3(0f, bounds.max.y + 1.25f, 0f);
                    ((RectTransform)child).sizeDelta = new Vector2(Mathf.Max(bounds.size.x, bounds.size.z) + 2f, 3f);
                }
            }
            return bounds;
        }

        // ---------- 비켜 둘 곳 ----------

        /// <summary>조형물이 들어가면 안 되는 곳: 성벽 둘레, 모서리 탑, 계단 칸과 발치, 건물, 광장, 길, 부대·플레이어 자리, 이미 놓은 것.</summary>
        sealed class KeepOut
        {
            readonly Plan plan;
            readonly List<List<Vector2>> paths;
            readonly List<(Vector2 point, float radius)> placed = new List<(Vector2, float)>();
            readonly List<Vector2> feet = new List<Vector2>(StairFeet());
            readonly List<Rect> blocked = new List<Rect>();
            readonly List<Vector2> people = new List<Vector2>();

            public KeepOut(Plan layout, List<List<Vector2>> routes)
            {
                plan = layout; paths = routes;
                float size = GameFlowSetup.CellSize;
                foreach (Vector2Int cell in GameFlowSetup.CornerTowerCells())
                    blocked.Add(new Rect(cell.x * size - 3f, cell.y * size - 3f, size + 6f, size + 6f));
                foreach ((Vector2Int cell, int _) in GameFlowSetup.StairSeeds())
                    blocked.Add(new Rect(cell.x * size - 2f, cell.y * size - 2f, size + 4f, size + 4f));
                // 부대 시작 자리, 성문 집결지, 플레이어 시작 자리, 전투 시작 깃발, 성문 안쪽.
                float stairFoot = (GameFlowSetup.WallNorth - 1) * size - 5.5f;
                people.Add(new Vector2(16.5f * size, stairFoot));
                people.Add(new Vector2(30.5f * size, stairFoot));
                people.Add(new Vector2(GameFlowSetup.AvenueX, GameFlowSetup.WallNorth * size - 22.5f));
                people.Add(new Vector2(GameFlowSetup.AvenueX, (GameFlowSetup.WallSouth + 1) * size + 15f));
                people.Add(new Vector2((GameFlowSetup.GateX + 1.5f) * size, (GameFlowSetup.WallNorth - 1.5f) * size));
                people.Add(GateInside);
            }

            public bool NearStairOrSquad(Vector2 point, float margin)
            {
                foreach (Vector2 foot in feet) if (Vector2.Distance(point, foot) < margin) return true;
                foreach (Rect rect in blocked) if (rect.Contains(point)) return true;
                return false;
            }

            float PathDistance(Vector2 point)
            {
                float best = float.MaxValue;
                foreach (List<Vector2> path in paths)
                    foreach (Vector2 p in path) best = Mathf.Min(best, (p - point).sqrMagnitude);
                return Mathf.Sqrt(best);
            }

            /// <summary>
            /// <paramref name="radius"/> 크기의 물건을 놓을 수 있는가. <paramref name="pathClear"/>는 길 가운데에서 떨어질 거리,
            /// <paramref name="buildingClear"/>는 건물 벽에서 떨어질 거리(건물 옆 잔소품은 작게 준다).
            /// </summary>
            public bool Free(Vector2 point, float radius, float pathClear, float buildingClear = 3f, bool checkPlaza = true)
            {
                float margin = 3.5f + radius;
                float x0 = (GameFlowSetup.WallWest + 1) * GameFlowSetup.CellSize, x1 = GameFlowSetup.WallEast * GameFlowSetup.CellSize;
                float z0 = (GameFlowSetup.WallSouth + 1) * GameFlowSetup.CellSize, z1 = GameFlowSetup.WallNorth * GameFlowSetup.CellSize;
                if (point.x < x0 + margin || point.x > x1 - margin || point.y < z0 + margin || point.y > z1 - margin) return false;
                foreach (Rect rect in blocked) if (rect.Overlaps(new Rect(point.x - radius, point.y - radius, radius * 2f, radius * 2f))) return false;
                foreach (Vector2 foot in feet) if (Vector2.Distance(point, foot) < 6f + radius) return false;
                foreach (Vector2 spot in people) if (Vector2.Distance(point, spot) < 6f + radius) return false;
                foreach (BuildingSpot b in plan.Buildings)
                {
                    if (Vector2.Distance(point, b.NpcSpot) < 3f + radius || Vector2.Distance(point, b.Entrance) < 3.5f + radius) return false;
                    Vector2 local = Rotate(point - b.Center, -b.Yaw);
                    if (Mathf.Abs(local.x) < b.Half.x + buildingClear + radius && Mathf.Abs(local.y) < b.Half.y + buildingClear + radius) return false;
                }
                if (checkPlaza && Vector2.Distance(point, Plaza) < PlazaRadius + 1.5f + radius) return false;
                if (pathClear > 0f && PathDistance(point) < pathClear + radius) return false;
                foreach ((Vector2 other, float r) in placed) if (Vector2.Distance(point, other) < r + radius + 0.4f) return false;
                return true;
            }

            public void Take(Vector2 point, float radius) => placed.Add((point, radius));
        }

        // ---------- 조형물 ----------

        const string Fountain = "Environments/SM_Env_Fountain_01";
        const string King = "Bonus/SM_Prop_Statue_King_01";
        const string Knight = "Props/SM_Prop_Statue_Knight_01";
        const string Hero = "Props/SM_Prop_Statue_Hero_01";
        const string Angel = "Props/SM_Prop_Statue_Angel_01";
        const string Plinth = "Props/SM_Prop_Statue_Plinth_01";
        const string TreeA = "Environments/SM_Env_Tree_Round_01";
        const string TreeB = "Environments/SM_Env_Tree_Round_02";
        const string TreeLarge = "Environments/SM_Env_Tree_Large_01";
        const string Well = "Props/SM_Prop_Well_01";
        const string StallA = "Props/SM_Prop_Market_Stall_01";
        const string StallB = "Props/SM_Prop_Market_Stall_02";
        const string Cart = "Props/SM_Prop_Hay_Cart_01";
        const string Lamp = "Props/SM_Prop_Street_Lamp_01";
        const string Banner = "Props/Banners/SM_Prop_Battle_Banner_01";
        const string Brazier = "Props/BattleGround/SM_Prop_Camp_Brazier_01";
        const string Bench = "Props/Furniture/SM_Prop_Bench_Seat_01";
        const string Planter = "Environments/SM_Env_Planter_Box_01";
        const string FlowersA = "Environments/SM_Env_Bush_Flowers_01";
        const string FlowersB = "Environments/SM_Env_Bush_Flowers_02";
        const string Dummy = "Props/BattleGround/SM_Prop_Dummy_Armour_01";
        const string Target = "Props/BattleGround/SM_Prop_Target_01";
        const string Rack = "Props/BattleGround/SM_Prop_Weapon_Rack_01";
        const string Anvil = "Props/SM_Prop_Anvil_01";
        const string Forge = "Props/SM_Prop_Workbench_Forge_01_Preset";
        const string Barrels = "Props/SM_Prop_Barrel_Stack_01";
        const string Crates = "Props/SM_Prop_Crate_Stack_01";
        const string Sacks = "Props/SM_Prop_Sack_Stack_01";
        const string Hay = "Props/SM_Prop_Hay_Bale_01";
        const string Wood = "Props/SM_Prop_Wood_Pile_01";
        const string Fence = "Props/SM_Prop_Fence_01";
        const string Table = "Props/Furniture/SM_Prop_Table_Preset_01";
        const string GrassPatch = "Generic/SM_Generic_Grass_Patch_01";
        const string GrassTuft = "Environments/SM_Env_Grass_Tuft_01";
        const string Bush = "Environments/SM_Env_Bush_01";
        const string Rock = "Environments/SM_Env_Rock_Chunk_01";

        /// <summary>건물 옆 잔소품: 건물 기준 (가로 비율, 깊이 비율, 가로 m, 깊이 m), 방향, 배율, 막는지. 문 앞(가운데)은 비운다.</summary>
        static readonly Dictionary<string, (string model, float sx, float sz, float dx, float dz, float yaw, float scale, bool blocks)[]> Yards =
            new Dictionary<string, (string, float, float, float, float, float, float, bool)[]>
            {
                ["Tavern"] = new[]
                {
                    (Table, -0.55f, 1f, 0f, 3.5f, 15f, 1f, true), (Barrels, 1f, 0.35f, 1.4f, 0f, 90f, 1f, true),
                    (Crates, 1f, -0.4f, 2f, 0f, 80f, 1f, true), (Sacks, -1f, -0.5f, -1.5f, 0f, 20f, 1f, true),
                },
                ["Shop"] = new[]
                {
                    (StallA, -1f, 0.75f, -3f, 1.5f, 70f, 1.2f, true), (StallB, -1f, -0.1f, -3.5f, 0f, 95f, 1.2f, true),
                    (Crates, 1f, -0.3f, 2f, 0f, 260f, 1f, true), (Sacks, 1f, 0.3f, 1.6f, 0f, 0f, 1f, true),
                },
                ["Training"] = new[]
                {
                    (Dummy, -0.6f, 1f, 0f, 5f, 0f, 1.2f, false), (Dummy, -0.9f, 1f, -2f, 7.5f, 20f, 1.2f, false),
                    (Target, -1f, 1f, -5f, 9f, 340f, 1.2f, false), (Fence, -1f, 1f, -6.5f, 4.5f, 90f, 1f, false),
                    (Fence, -1f, 1f, -6.5f, 7f, 90f, 1f, false), (Rack, 1f, 0.2f, 1.5f, 0f, 270f, 1.1f, false),
                    (Hay, 1f, -0.5f, 1.6f, 0f, 10f, 1.1f, true), (Hay, 1f, -0.5f, 2.9f, 0.8f, 40f, 1.1f, true),
                },
                ["Smithy"] = new[]
                {
                    (Anvil, -0.55f, 1f, 0f, 2.5f, 30f, 1.2f, false), (Forge, -1f, 0.4f, -2f, 0f, 90f, 1.1f, false),
                    (Wood, 1f, -0.3f, 1.6f, 0f, 90f, 1.1f, true), (Barrels, 1f, 0.4f, 1.4f, 0f, 0f, 1f, true),
                },
                ["Construction"] = new[]
                {
                    (Cart, 1f, 0.3f, 3f, 0f, 70f, 1.2f, true), (Wood, -1f, 0.3f, -2f, 0f, 90f, 1.1f, true),
                    (Wood, -1f, -0.2f, -2f, 0f, 85f, 1.1f, true), (Crates, 1f, -0.5f, 2.2f, 0f, 100f, 1f, true),
                },
                [HeroHallSetup.BuildingName] = new[]
                {
                    (Hero, -0.75f, 1f, 0f, 3f, 0f, 1.4f, true), (Hero, 0.75f, 1f, 0f, 3f, 0f, 1.4f, true),
                    (Planter, -1f, 0.5f, -1.2f, 0f, 0f, 1.1f, false), (Planter, 1f, 0.5f, 1.2f, 0f, 0f, 1.1f, false),
                },
            };

        static void PlaceDecor(Transform geometry, Plan plan, List<List<Vector2>> paths, KeepOut keepOut, IRandomSource random,
            Dictionary<Material, Material> converted)
        {
            Transform old = geometry.Find(DecorName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(DecorName).transform;
            root.SetParent(geometry, false);
            int count = 0;
            void Put(string model, Vector2 point, float yaw, float scale, bool blocks, float radius, float y = 0f)
            {
                GameObject prop = Spawn(root, Short(model) + "_" + (count++), model, yaw, converted, blocks);
                prop.transform.localScale = Vector3.one * scale;
                prop.transform.localPosition = Ground(point, y);
                foreach (Transform part in prop.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(part.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
                keepOut.Take(point, radius);
            }
            float Jitter(float amount) => (random.NextFloat() - 0.5f) * 2f * amount;
            Vector2 FacePlaza(Vector2 from) => Plaza - from;

            // 광장: 분수, 둘레 벤치(길이 닿는 쪽은 비운다), 화단.
            Put(Fountain, Plaza, 20f, 2.2f, true, 6f, 0.33f);
            var spokes = new List<float>();
            foreach (List<Vector2> path in paths)
                foreach (Vector2 end in new[] { path[0], path[path.Count - 1] })
                    if (Vector2.Distance(end, Plaza) < PlazaRadius + 1f) spokes.Add(YawOf(end - Plaza));
            for (int i = 0; i < 14; i++)
            {
                float angle = i * (360f / 14f) + 9f;
                bool onSpoke = spokes.Exists(s => Mathf.Abs(Mathf.DeltaAngle(s, angle)) < 16f);
                if (onSpoke) continue;
                Vector2 point = Plaza + Dir(angle) * (PlazaRadius - 2f);
                if (i % 2 == 0) Put(Bench, point, angle + 180f, 1.2f, false, 1.2f, 0.37f);
                else Put(i % 4 == 1 ? FlowersA : FlowersB, point, angle, 1.5f, false, 1f, 0.37f);
            }

            // 왕 동상: 성문 길 옆, 광장을 본다. 성문 안쪽 기사 동상과 화로, 집결지 깃발.
            var kingSpot = new Vector2(GameFlowSetup.AvenueX + 12f, 212f);
            float kingYaw = YawOf(FacePlaza(kingSpot)) + 25f;
            Put(Plinth, kingSpot, kingYaw, 2.2f, true, 2.2f);
            Put(King, kingSpot, kingYaw, 2.2f, true, 0f, 3.9f);
            foreach (float side in new[] { -1f, 1f })
            {
                var knight = new Vector2(GameFlowSetup.AvenueX + side * 13f, GateInside.y - 4f);
                Put(Plinth, knight, 180f, 1.8f, true, 1.8f);
                Put(Knight, knight, 180f + side * 8f, 1.8f, true, 0f, 3.2f);
                Put(Brazier, knight + new Vector2(-side * 3.5f, 1f), 0f, 2f, false, 0.8f);
                Put(Banner, new Vector2(GameFlowSetup.AvenueX + side * 19f, GameFlowSetup.WallNorth * GameFlowSetup.CellSize - 26f),
                    side * 10f, 1.6f, false, 1f);
            }

            // 시설마다 옆 마당 잔소품(건물 방향을 따른다, 조금씩 비튼다).
            foreach (BuildingSpot b in plan.Buildings)
            {
                if (!Yards.TryGetValue(b.Name, out var items)) continue;
                foreach (var item in items)
                {
                    Vector2 point = b.Local(item.sx * b.Half.x + item.dx, item.sz * b.Half.y + item.dz);
                    Put(item.model, point, b.Yaw + item.yaw + Jitter(8f), item.scale, item.blocks, 1.2f);
                }
            }

            // 장식용 집: 쓰지 않는 집들을 빈 자리에 길을 보게 놓아 마을처럼 채운다(나무·소품보다 먼저 자리를 잡는다).
            PlaceHouses(root, plan, paths, keepOut, random, converted);

            // 큰 소품 몇 개: 우물, 천사 동상(정원). 빈 자리를 찾아 놓는다.
            PutFree(Well, new Vector2(203f, 238f), 1.4f, true, 2.5f, 6f);
            PutFree(Angel, new Vector2(146f, 244f), 1.5f, true, 2.5f, 6f);

            // 길가 가로등: 길을 따라 20m쯤마다 번갈아 옆에.
            foreach (List<Vector2> path in paths)
            {
                float side = 1f;
                for (int i = 6; i < path.Count - 6; i += 13)
                {
                    Vector2 along = (path[Mathf.Min(path.Count - 1, i + 1)] - path[i]).normalized;
                    Vector2 normal = new Vector2(-along.y, along.x) * side;
                    Vector2 point = path[i] + normal * 3.8f;
                    side = -side;
                    if (keepOut.Free(point, 0.6f, 3.2f)) Put(Lamp, point, YawOf(-normal), 1.6f, false, 0.6f);
                }
            }

            // 숲: 안뜰 가장자리 여러 곳에 나무를 몰아 심는다(크기·방향 제각각).
            Vector2[] groves =
            {
                new Vector2(97f, 252f), new Vector2(150f, 258f), new Vector2(254f, 250f), new Vector2(100f, 186f),
                new Vector2(252f, 186f), new Vector2(97f, 101f), new Vector2(150f, 100f), new Vector2(205f, 128f),
                new Vector2(155f, 140f), new Vector2(200f, 197f), new Vector2(205f, 263f), new Vector2(118f, 196f),
                new Vector2(248f, 108f), new Vector2(195f, 100f), new Vector2(140f, 185f), new Vector2(214f, 182f),
            };
            foreach (Vector2 grove in groves)
            {
                int trees = 3 + random.NextInt(3);
                for (int attempt = 0, planted = 0; attempt < 30 && planted < trees; attempt++)
                {
                    Vector2 point = grove + new Vector2(Jitter(10f), Jitter(10f));
                    bool large = planted == 0 && random.NextFloat() < 0.35f;
                    float radius = large ? 6f : 2.2f;
                    if (!keepOut.Free(point, radius, 3.5f)) continue;
                    Put(large ? TreeLarge : (random.NextFloat() < 0.5f ? TreeA : TreeB), point, random.NextFloat() * 360f,
                        large ? 0.8f + random.NextFloat() * 0.3f : 1.3f + random.NextFloat() * 0.6f, true, radius);
                    planted++;
                }
            }

            // 덤불: 나무 둘레와 성벽 안쪽을 따라.
            for (int attempt = 0, placed = 0; attempt < 400 && placed < 45; attempt++)
            {
                Vector2 point = attempt % 2 == 0 ? groves[random.NextInt(groves.Length)] + new Vector2(Jitter(14f), Jitter(14f))
                    : WallSide(random);
                if (!keepOut.Free(point, 1.6f, 3f)) continue;
                Put(Bush, point, random.NextFloat() * 360f, 0.6f + random.NextFloat() * 0.5f, false, 1.6f);
                placed++;
            }
            // 바위, 풀 무더기, 꽃: 고르게 뿌리지 않고 무더기로 모아 심는다(데모 씬처럼 빈 곳과 몰린 곳이 갈린다).
            Clusters(new[] { Rock, Rock, GrassTuft }, 16, 2, 4, 0.8f, 0.35f, 0.5f);
            Clusters(new[] { GrassPatch, GrassTuft, GrassTuft }, 40, 3, 6, 1f, 0.8f, 0.6f);
            Clusters(new[] { FlowersA, FlowersB, GrassTuft }, 22, 3, 5, 0.8f, 1.1f, 0.5f);
            Clusters(new[] { Bush, FlowersA, GrassTuft }, 14, 2, 4, 1.4f, 0.7f, 0.4f);

            void PutFree(string model, Vector2 near, float scale, bool blocks, float radius, float search)
            {
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    Vector2 point = near + (attempt == 0 ? Vector2.zero : new Vector2(Jitter(search), Jitter(search)));
                    if (!keepOut.Free(point, radius, 3.5f)) continue;
                    Put(model, point, random.NextFloat() * 360f, scale, blocks, radius);
                    return;
                }
            }

            // 무더기 clusters개: 빈 자리 하나를 고르고 그 둘레 4m 안에 min~max개를 모아 놓는다. 길가에도 붙을 수 있다.
            void Clusters(string[] models, int clusters, int min, int max, float radius, float scale, float scaleJitter)
            {
                for (int attempt = 0, made = 0; attempt < clusters * 10 && made < clusters; attempt++)
                {
                    var center = new Vector2(84f + random.NextFloat() * 177f, 92f + random.NextFloat() * 177f);
                    if (!keepOut.Free(center, radius, 2.6f, 0.8f)) continue;
                    made++;
                    int amount = min + random.NextInt(max - min + 1);
                    for (int i = 0, tries = 0; i < amount && tries < amount * 4; tries++)
                    {
                        Vector2 point = center + new Vector2(Jitter(4f), Jitter(4f));
                        if (!keepOut.Free(point, radius * 0.6f, 2.4f, 0.8f)) continue;
                        Put(models[random.NextInt(models.Length)], point, random.NextFloat() * 360f,
                            scale + random.NextFloat() * scaleJitter, false, radius * 0.6f);
                        i++;
                    }
                }
            }
        }

        /// <summary>장식용 집 모델(시설 모델과 겹치지 않는다).</summary>
        static readonly string[] DecorHouses =
        {
            "SM_Bld_Preset_House_01_A_Optimized", "SM_Bld_Preset_House_02_A_Optimized", "SM_Bld_Preset_House_04_Optimized",
            "SM_Bld_Preset_House_06_Optimized", "SM_Bld_Preset_House_07_Optimized", "SM_Bld_Preset_House_08_Optimized",
            "SM_Bld_Preset_House_09_B_Optimized", "SM_Bld_Preset_House_09_C_Optimized", "SM_Bld_Preset_House_10_Optimized",
            "SM_Bld_Preset_Hut_01_Optimized", "SM_Bld_Preset_Shelter_01_Optimized", "SM_Bld_Preset_House_Windmill_01_Optimized",
            "SM_Bld_Preset_Tower_01_Optimized", "SM_Bld_Preset_House_Archway_02_Optimized",
        };
        const float DecorHouseScale = 1.2f;

        /// <summary>
        /// 장식용 집을 빈 자리에 놓는다. 정면(+z)은 가장 가까운 길을 본다. 충돌 상자로 길찾기가 돌아가게 하고,
        /// 뜬 본체는 받침에 앉힌다. 자리가 없으면 그 집은 건너뛴다.
        /// </summary>
        static void PlaceHouses(Transform root, Plan plan, List<List<Vector2>> paths, KeepOut keepOut, IRandomSource random,
            Dictionary<Material, Material> converted)
        {
            foreach (string name in DecorHouses)
            {
                var house = new GameObject("House_" + name.Replace("SM_Bld_Preset_", string.Empty).Replace("_Optimized", string.Empty)).transform;
                house.SetParent(root, false);
                var visual = new GameObject("Visual").transform;
                visual.SetParent(house, false);
                GameObject model = Spawn(visual, ModelName, "Buildings/Preset_Houses/" + name, 0f, converted, keepColliders: false);
                model.transform.localScale = Vector3.one * DecorHouseScale;
                Bounds raw = MeshBounds(model.transform, visual);
                model.transform.localPosition = new Vector3(-raw.center.x, -raw.min.y, -raw.center.z);
                Bounds bounds = MeshBounds(model.transform, visual);
                float radius = new Vector2(bounds.size.x, bounds.size.z).magnitude * 0.5f;

                bool placed = false;
                for (int attempt = 0; attempt < 80 && !placed; attempt++)
                {
                    var point = new Vector2(84f + random.NextFloat() * 177f, 92f + random.NextFloat() * 177f);
                    if (!keepOut.Free(point, radius, 3f, 3f)) continue;
                    Vector2 toPath = NearestPathPoint(paths, point) - point;
                    float yaw = YawOf(toPath) + (random.NextFloat() - 0.5f) * 20f;
                    house.localPosition = Ground(point);
                    house.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    keepOut.Take(point, radius);
                    plan.HouseDoors.Add(point + Dir(yaw) * (bounds.size.z * 0.5f + 2f));
                    placed = true;
                }
                if (!placed) { UnityEngine.Object.DestroyImmediate(house.gameObject); continue; }
                bounds = SinkToFoundation(visual, model.transform);
                var body = house.gameObject.AddComponent<BoxCollider>();
                body.center = new Vector3(0f, bounds.max.y * 0.5f, 0f);
                body.size = new Vector3(bounds.size.x, bounds.max.y, bounds.size.z);
                foreach (Transform part in house.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(part.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            }
        }

        static Vector2 NearestPathPoint(List<List<Vector2>> paths, Vector2 point)
        {
            Vector2 best = point + Vector2.up;
            float bestDistance = float.MaxValue;
            foreach (List<Vector2> path in paths)
                foreach (Vector2 p in path)
                {
                    float d = (p - point).sqrMagnitude;
                    if (d < bestDistance) { bestDistance = d; best = p; }
                }
            return best;
        }

        const string FieldName = "FieldDecor";

        /// <summary>
        /// 성 밖 들판 꾸밈(SIEGE-36). 적이 오가는 들판은 막지 않는다: 풀·꽃·바위 무더기는 충돌체가 없고,
        /// 길을 막는 나무는 맵 서·동·남쪽 가장자리 띠(성벽에서 30m 넘게 떨어진 곳)에만 숲으로 심는다.
        /// </summary>
        static void PlaceField(Transform geometry, IRandomSource random, Dictionary<Material, Material> converted)
        {
            Transform old = geometry.Find(FieldName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(FieldName).transform;
            root.SetParent(geometry, false);
            float width = GameFlowSetup.GridWidth * GameFlowSetup.CellSize, depth = GameFlowSetup.GridDepth * GameFlowSetup.CellSize;
            float wallX0 = GameFlowSetup.WallWest * GameFlowSetup.CellSize, wallX1 = (GameFlowSetup.WallEast + 1) * GameFlowSetup.CellSize;
            float wallZ0 = GameFlowSetup.WallSouth * GameFlowSetup.CellSize, wallZ1 = (GameFlowSetup.WallNorth + 1) * GameFlowSetup.CellSize;
            List<Vector2> road = FieldRoad();
            var placed = new List<(Vector2 point, float radius)>();
            int count = 0;
            float Jitter(float amount) => (random.NextFloat() - 0.5f) * 2f * amount;
            bool Free(Vector2 point, float radius)
            {
                if (point.x < 2f || point.x > width - 2f || point.y < 2f || point.y > depth - 2f) return false;
                if (point.x > wallX0 - 6f && point.x < wallX1 + 6f && point.y > wallZ0 - 6f && point.y < wallZ1 + 6f) return false;
                foreach (Vector2 p in road) if ((p - point).sqrMagnitude < (4f + radius) * (4f + radius)) return false;
                foreach ((Vector2 other, float r) in placed) if (Vector2.Distance(point, other) < r + radius + 0.4f) return false;
                return true;
            }
            void Put(string model, Vector2 point, float scale, bool blocks, float radius)
            {
                GameObject prop = Spawn(root, Short(model) + "_" + (count++), model, random.NextFloat() * 360f, converted, blocks);
                prop.transform.localScale = Vector3.one * scale;
                prop.transform.localPosition = Ground(point);
                foreach (Transform part in prop.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(part.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
                placed.Add((point, radius));
            }

            // 가장자리 숲: 서·동쪽 띠(x 45m 안쪽 / 바깥)와 남쪽 띠(z 50m 안쪽).
            for (int grove = 0, made = 0; grove < 80 && made < 16; grove++)
            {
                int band = random.NextInt(3);
                Vector2 center = band == 0 ? new Vector2(6f + random.NextFloat() * 38f, 10f + random.NextFloat() * (depth - 20f))
                    : band == 1 ? new Vector2(width - 6f - random.NextFloat() * 38f, 10f + random.NextFloat() * (depth - 20f))
                    : new Vector2(50f + random.NextFloat() * (width - 100f), 6f + random.NextFloat() * 42f);
                if (!Free(center, 3f)) continue;
                made++;
                int trees = 4 + random.NextInt(4);
                for (int i = 0, tries = 0; i < trees && tries < 30; tries++)
                {
                    Vector2 point = center + new Vector2(Jitter(11f), Jitter(11f));
                    bool inBand = point.x < 45f || point.x > width - 45f || point.y < 50f;
                    bool large = random.NextFloat() < 0.25f;
                    float radius = large ? 6f : 2.2f;
                    if (!inBand || !Free(point, radius)) continue;
                    Put(large ? TreeLarge : (random.NextFloat() < 0.5f ? TreeA : TreeB), point,
                        large ? 0.8f + random.NextFloat() * 0.3f : 1.3f + random.NextFloat() * 0.6f, true, radius);
                    i++;
                }
            }
            // 들판 무더기(충돌 없음): 풀, 꽃, 바위, 덤불.
            (string[] models, int clusters, float scale)[] kinds =
            {
                (new[] { GrassPatch, GrassTuft, GrassTuft }, 70, 0.9f), (new[] { FlowersA, FlowersB, GrassTuft }, 30, 1.1f),
                (new[] { Rock, Rock, GrassTuft }, 25, 0.4f), (new[] { Bush, GrassTuft }, 30, 0.7f),
            };
            foreach ((string[] models, int clusters, float scale) in kinds)
                for (int attempt = 0, made = 0; attempt < clusters * 10 && made < clusters; attempt++)
                {
                    var center = new Vector2(random.NextFloat() * width, random.NextFloat() * depth);
                    if (!Free(center, 1f)) continue;
                    made++;
                    int amount = 2 + random.NextInt(4);
                    for (int i = 0, tries = 0; i < amount && tries < amount * 4; tries++)
                    {
                        Vector2 point = center + new Vector2(Jitter(5f), Jitter(5f));
                        if (!Free(point, 0.6f)) continue;
                        Put(models[random.NextInt(models.Length)], point, scale + random.NextFloat() * 0.5f, false, 0.6f);
                        i++;
                    }
                }
        }

        const string SurroundingsName = "Surroundings";
        static readonly string[] Pines =
        {
            "Environments/SM_Env_Tree_Pine_Cluster_01", "Environments/SM_Env_Tree_Pine_Cluster_02",
            "Environments/SM_Env_Tree_Pine_Cluster_04", "Environments/SM_Env_Tree_Pine_Cluster_05",
        };
        static readonly string[] Hills =
        {
            "Generic/SM_Generic_Ground_04", "Generic/SM_Generic_Ground_05", "Generic/SM_Generic_Ground_06",
            "Generic/SM_Generic_Ground_07", "Generic/SM_Generic_Ground_08", "Generic/SM_Generic_Ground_09",
        };
        static readonly string[] Mountains = { "Generic/SM_Generic_Mountains_Grass_01", "Generic/SM_Generic_Mountains_Grass_02" };
        static readonly string[] Cliffs = { "Environments/SM_Env_Rock_Cliff_01", "Environments/SM_Env_Rock_Cliff_03" };

        /// <summary>
        /// 맵 바깥 경치(SIEGE-36): 경계 바로 밖을 두르는 소나무 숲 띠(판 끝이 보이지 않게), 그 너머 구릉, 멀리 산과 바위 절벽.
        /// 모두 맵 밖이고 충돌체가 없어 길찾기·전투에 영향이 없다.
        /// </summary>
        static void PlaceSurroundings(Transform geometry, IRandomSource random, Dictionary<Material, Material> converted)
        {
            Transform old = geometry.Find(SurroundingsName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(SurroundingsName).transform;
            root.SetParent(geometry, false);
            float width = GameFlowSetup.GridWidth * GameFlowSetup.CellSize, depth = GameFlowSetup.GridDepth * GameFlowSetup.CellSize;
            var center = new Vector2(width * 0.5f, depth * 0.5f);
            List<Vector2> road = FieldRoad();
            int count = 0;
            void Put(string model, Vector2 point, float scale, float y = 0f)
            {
                GameObject prop = Spawn(root, Short(model) + "_" + (count++), model, random.NextFloat() * 360f, converted, keepColliders: false);
                prop.transform.localScale = Vector3.one * scale;
                prop.transform.localPosition = Ground(point, y);
                foreach (Transform part in prop.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(part.gameObject, StaticEditorFlags.BatchingStatic);
            }
            // 맵 둘레 사각형에서 바깥쪽으로 distance만큼 떨어진 점(둘레를 따라 t: 0~1).
            Vector2 Around(float t, float distance)
            {
                float perimeter = 2f * (width + depth), s = t * perimeter;
                if (s < width) return new Vector2(s, -distance);
                s -= width;
                if (s < depth) return new Vector2(width + distance, s);
                s -= depth;
                if (s < width) return new Vector2(width - s, depth + distance);
                s -= width;
                return new Vector2(-distance, depth - s);
            }
            bool OnRoad(Vector2 point)
            {
                foreach (Vector2 p in road) if ((p - point).sqrMagnitude < 81f) return true;
                return false;
            }

            // 숲 띠: 경계 밖 6~70m에 소나무 군락을 겹겹이(안쪽은 촘촘히). 길이 지나는 곳은 비운다.
            float perimeterLength = 2f * (width + depth);
            for (int layer = 0; layer < 3; layer++)
            {
                float spacing = 9f + layer * 3f;
                int amount = Mathf.RoundToInt(perimeterLength / spacing);
                for (int i = 0; i < amount; i++)
                {
                    Vector2 point = Around((i + random.NextFloat() * 0.8f) / amount, 6f + layer * 20f + random.NextFloat() * 14f);
                    if (OnRoad(point)) continue;
                    bool broad = random.NextFloat() < 0.15f;
                    Put(broad ? TreeLarge : Pines[random.NextInt(Pines.Length)], point,
                        broad ? 0.9f + random.NextFloat() * 0.4f : 0.8f + random.NextFloat() * 0.6f);
                }
            }
            // 구릉: 숲 띠 너머 80~200m.
            for (int i = 0; i < 46; i++)
            {
                Vector2 point = Around(random.NextFloat(), 80f + random.NextFloat() * 120f);
                Put(Hills[random.NextInt(Hills.Length)], point, 2f + random.NextFloat() * 2.5f, -1.5f);
                if (random.NextFloat() < 0.5f)
                    Put(Pines[random.NextInt(Pines.Length)], point + new Vector2((random.NextFloat() - 0.5f) * 30f, (random.NextFloat() - 0.5f) * 30f),
                        0.8f + random.NextFloat() * 0.5f, 3f);
            }
            // 산과 절벽: 맵 가운데에서 450~650m 둘레의 지평선.
            for (int i = 0; i < 22; i++)
            {
                float angle = (i + random.NextFloat() * 0.7f) / 22f * 360f;
                Vector2 point = center + Dir(angle) * (450f + random.NextFloat() * 200f);
                Put(Mountains[random.NextInt(Mountains.Length)], point, 6f + random.NextFloat() * 5f, -4f);
                if (i % 3 == 0)
                    Put(Cliffs[random.NextInt(Cliffs.Length)], center + Dir(angle + 6f) * (330f + random.NextFloat() * 60f),
                        2f + random.NextFloat() * 1.5f, -2f);
            }
        }

        /// <summary>성벽 안쪽 면에서 4~9m 떨어진 아무 점.</summary>
        static Vector2 WallSide(IRandomSource random)
        {
            float x0 = (GameFlowSetup.WallWest + 1) * GameFlowSetup.CellSize, x1 = GameFlowSetup.WallEast * GameFlowSetup.CellSize;
            float z0 = (GameFlowSetup.WallSouth + 1) * GameFlowSetup.CellSize, z1 = GameFlowSetup.WallNorth * GameFlowSetup.CellSize;
            float inset = 4f + random.NextFloat() * 5f, t = random.NextFloat();
            switch (random.NextInt(4))
            {
                case 0: return new Vector2(Mathf.Lerp(x0, x1, t), z0 + inset);
                case 1: return new Vector2(Mathf.Lerp(x0, x1, t), z1 - inset);
                case 2: return new Vector2(x0 + inset, Mathf.Lerp(z0, z1, t));
                default: return new Vector2(x1 - inset, Mathf.Lerp(z0, z1, t));
            }
        }

        static string Short(string model) => model.Substring(model.LastIndexOf('/') + 1).Replace("SM_", string.Empty);

        // ---------- 공용 ----------

        /// <summary>원본 프리팹과 연결하지 않은 복사본을 만들고 머티리얼을 URP로 바꾼다. keepColliders가 아니면 모델 충돌체를 지운다.</summary>
        internal static GameObject Spawn(Transform parent, string name, string modelPath, float yaw, Dictionary<Material, Material> converted,
            bool keepColliders, string pack = Pack, string materialFolder = MaterialFolder)
        {
            var instance = UnityEngine.Object.Instantiate(Load(modelPath, pack));
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (!keepColliders)
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = CharacterModelSetup.ToUrp(materials[i], converted, materialFolder);
                renderer.sharedMaterials = materials;
            }
            return instance;
        }

        /// <summary>모델을 같은 배율로 키우거나 줄여 바닥 크기 안에 맞추고, 바닥 가운데·지면 위에 놓는다. 맞춘 뒤의 경계를 돌려준다.</summary>
        static Bounds Fit(Transform model, Transform space, Vector2 footprint)
        {
            model.localScale = Vector3.one;
            model.localPosition = Vector3.zero;
            Bounds bounds = MeshBounds(model, space);
            float scale = Mathf.Min(footprint.x / Mathf.Max(1e-3f, bounds.size.x), footprint.y / Mathf.Max(1e-3f, bounds.size.z));
            model.localScale = Vector3.one * scale;
            bounds = MeshBounds(model, space);
            model.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            return MeshBounds(model, space);
        }

        /// <summary>
        /// Synty 집 모델은 비탈용이라 가장 낮은 점(기둥·계단 끝)만 땅에 닿고 돌 기단은 떠 있다.
        /// 기단 바닥 높이(<see cref="FoundationHeight"/>)만큼 건물을 내려 기단이 땅에 앉게 한다. 더 낮은 기둥·계단 끝은 땅속으로 들어간다.
        /// </summary>
        static Bounds SinkToFoundation(Transform visual, Transform model)
        {
            float lift = FoundationHeight(model, visual);
            if (lift > 0.05f) model.localPosition += Vector3.down * lift;
            return MeshBounds(model, visual);
        }

        /// <summary>
        /// 건물 바닥을 12×12 지점으로 나눠 지점마다 아래에서 위로 올린 선이 처음 닿는 면 높이를 구하고,
        /// 가장 많은 지점이 걸리는 높이(0.25m 단위)를 돌려준다(가장 낮은 점 기준). 처마처럼 높은 곳(4.5m 위)은 뺀다.
        /// 낮은 폴리곤이라 꼭짓점 대신 삼각형 면으로 잰다.
        /// </summary>
        static float FoundationHeight(Transform model, Transform space)
        {
            Bounds bounds = MeshBounds(model, space);
            const int samples = 12;
            var lowest = new float[samples * samples];
            for (int i = 0; i < lowest.Length; i++) lowest[i] = float.MaxValue;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                Matrix4x4 toSpace = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Vector3[] vertices = mesh.vertices;
                for (int v = 0; v < vertices.Length; v++) vertices[v] = toSpace.MultiplyPoint3x4(vertices[v]);
                int[] triangles = mesh.triangles;
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                    float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
                    float minZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), maxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
                    float area = (b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z);
                    if (Mathf.Abs(area) < 1e-6f) continue;
                    for (int iz = 0; iz < samples; iz++)
                    {
                        float z = bounds.min.z + (iz + 0.5f) / samples * bounds.size.z;
                        if (z < minZ || z > maxZ) continue;
                        for (int ix = 0; ix < samples; ix++)
                        {
                            float x = bounds.min.x + (ix + 0.5f) / samples * bounds.size.x;
                            if (x < minX || x > maxX) continue;
                            // 바닥(x, z)에서 본 무게중심 좌표로 삼각형 안인지 보고 높이를 보간한다.
                            float w1 = ((b.x - x) * (c.z - z) - (c.x - x) * (b.z - z)) / area;
                            float w2 = ((c.x - x) * (a.z - z) - (a.x - x) * (c.z - z)) / area;
                            float w3 = 1f - w1 - w2;
                            if (w1 < 0f || w2 < 0f || w3 < 0f) continue;
                            float y = w1 * a.y + w2 * b.y + w3 * c.y;
                            int index = iz * samples + ix;
                            if (y < lowest[index]) lowest[index] = y;
                        }
                    }
                }
            }
            var counts = new Dictionary<int, int>();
            foreach (float y in lowest)
            {
                if (y == float.MaxValue || y - bounds.min.y > 4.5f) continue;
                int bin = Mathf.FloorToInt((y - bounds.min.y) / 0.25f);
                counts[bin] = counts.TryGetValue(bin, out int n) ? n + 1 : 1;
            }
            int best = 0, bestCount = -1;
            foreach (var entry in counts)
                if (entry.Value > bestCount || entry.Value == bestCount && entry.Key < best) { best = entry.Key; bestCount = entry.Value; }
            return bestCount < 0 ? 0f : best * 0.25f;
        }

        static Bounds MeshBounds(Transform model, Transform space)
        {
            var result = new Bounds();
            bool first = true;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds local = filter.sharedMesh.bounds;
                Matrix4x4 toSpace = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? local.min.x : local.max.x, (i & 2) == 0 ? local.min.y : local.max.y,
                        (i & 4) == 0 ? local.min.z : local.max.z);
                    Vector3 point = toSpace.MultiplyPoint3x4(corner);
                    if (first) { result = new Bounds(point, Vector3.zero); first = false; }
                    else result.Encapsulate(point);
                }
            }
            return result;
        }

        static GameObject Load(string modelPath, string pack = Pack) => AssetDatabase.LoadAssetAtPath<GameObject>(pack + modelPath + ".prefab")
            ?? throw new InvalidOperationException("Courtyard model missing: " + modelPath + " (run git submodule update --init).");

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
