using System;
using System.Collections.Generic;
using System.Linq;
using ProjectNeo.Characters;
using ProjectNeo.Flow;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// SIEGE-40/39: Game 씬을 분지 배치로 바꾼다(사용자 결정 2026-11-27~28). 분지 초안 씬(<see cref="BasinScenePath"/>)의
    /// 데모 마을·성·들판·산과 성벽 끝 탑·기초를 Game으로 옮기고, Game의 성(<see cref="ConstructionWorld"/>, 37곳이 참조한다)은
    /// 그대로 둔 채 분지 격자 자리·크기·시작 배치로 다시 설정한다. 링 시절 안뜰 꾸밈은 걷어 낸다.
    /// 시설 건물은 임시로 성문 안쪽에 줄지어 두고, 데모 집에 연결하는 일은 내성 단계(SIEGE-39)에서 한다.
    /// 분지 초안 씬은 바꾸지 않는다(읽어서 복사만 한다).
    /// </summary>
    public static class GameBasinSetup
    {
        public const string BasinScenePath = "Assets/_Project/Scenes/Preview/FantasyKingdomBasin.unity";
        const string Data = "Assets/_Project/Data/GameFlow";
        const string Pieces = "Assets/_Project/Data/ConstructionPrototype/";
        // 분지 초안과 같은 격자(SIEGE-40 문서): 42×12칸, 5m, 성벽은 맨 안쪽 줄, 29번 칸 성문탑, 0·41번 칸은 끝 탑 자리.
        public const int Width = 42, Depth = 12, MaxLevels = 4, WallRow = 0, GateX = 29, BlockedSides = 1;
        public const float Cell = 5f;
        // 적 출현 가운데 칸: 성벽 바깥면(z 1칸)에서 90m 북쪽(18칸), 좌우는 StageSetup 간격.
        public static readonly Vector2 SpawnCell = new Vector2(GateX, WallRow + 1 + 18);
        // 분지 초안에서 옮겨 올 루트(그 씬의 맨 위 오브젝트 이름).
        static readonly string[] EnvironmentRoots = { "DemoWorld", "ApproachGround", "BasinMountains" };
        // 링 시절 Game 안뜰 꾸밈(GameCourtyardArtSetup). 분지에서는 데모 마을이 대신한다.
        static readonly string[] RingDecor = { "CourtyardGround", "CourtyardDecor", "FieldDecor", "Surroundings" };

        // 시설 → 데모 집(사용자 선택 2026-11-28). 같은 이름이 여럿인 집은 데모 좌표(Game 월드 x·z, m)에 가장 가까운 것.
        static readonly (string facility, string house, Vector2 near)[] FacilityHouses =
        {
            ("Tavern", "Preset_Tavern_01", new Vector2(23f, 46f)),
            ("Shop", "SM_Bld_Preset_House_07_Optimized", new Vector2(-8f, 78f)),
            ("Training", "Preset_Church_01_A", new Vector2(-72f, 98f)),
            ("Smithy", "Preset_Blacksmith_01", new Vector2(-23f, 79f)),
            ("Construction", "SM_Bld_Preset_House_02_A_Optimized", new Vector2(16f, 60f)),
            ("HeroHall", "SM_Bld_Preset_House_02_A_Optimized", new Vector2(-17f, 60f)),
        };

        // 시설 NPC 자리와 방향(사용자가 에디터에서 각 집 문 앞에 직접 놓은 값, 2026-11-28, Game 월드 좌표·y 회전).
        static readonly (string facility, Vector3 position, float yaw)[] NpcSpots =
        {
            ("Tavern", new Vector3(21.00f, -7.87f, 53.03f), 359.3f),
            ("Shop", new Vector3(-2.98f, -16.40f, 79.02f), 46.7f),
            ("Training", new Vector3(-69.98f, -24.88f, 78.03f), 39.0f),
            ("Smithy", new Vector3(-23.00f, -12.87f, 82.00f), 18.2f),
            ("Construction", new Vector3(18.00f, -7.87f, 55.98f), 194.5f),
            ("HeroHall", new Vector3(-12.02f, -7.96f, 53.96f), 212.3f),
        };

        [MenuItem("ProjectNeo/Setup/Apply Basin To Game Scene (SIEGE-40)")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            EditorSceneManager.SaveOpenScenes();
            Scene game = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            Scene basin = EditorSceneManager.OpenScene(BasinScenePath, OpenSceneMode.Additive);
            try
            {
                return Build(game, basin);
            }
            finally
            {
                // 초안 씬은 저장하지 않고 닫는다(옮긴 오브젝트는 복사본이다).
                EditorSceneManager.CloseScene(basin, true);
            }
        }

        static string Build(Scene game, Scene basin)
        {
            var log = new List<string>();
            ConstructionWorld world = Find<ConstructionWorld>(game);
            ConstructionWorld draft = Find<ConstructionWorld>(basin);
            Transform geometry = world.transform.Find("Geometry");

            // 1. 격자: Game 전용 격자 에셋의 값만 바꿔 GUID(저장·참조)를 지킨다.
            var grid = world.Grid;
            var gridData = new SerializedObject(grid);
            gridData.FindProperty("width").intValue = Width;
            gridData.FindProperty("depth").intValue = Depth;
            gridData.FindProperty("cellSize").floatValue = Cell;
            gridData.FindProperty("maxLevels").intValue = MaxLevels;
            gridData.FindProperty("forbidValleys").boolValue = true;
            gridData.FindProperty("groundStairsOnly").boolValue = true;
            gridData.FindProperty("castleBehindSouthEdge").boolValue = true;
            gridData.FindProperty("blockedSideCells").intValue = BlockedSides;
            gridData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(grid);

            // 2. 성 자리와 시작 배치(공용 5m 조각 정의).
            world.transform.SetPositionAndRotation(draft.transform.position, draft.transform.rotation);
            var wall = Required<BuildPieceDefinition>(Pieces + "Wall.asset");
            var gate = Required<BuildPieceDefinition>(Pieces + "Gate.asset");
            world.Configure(grid, geometry, geometry.GetComponent<Unity.AI.Navigation.NavMeshSurface>(), Seeds(wall, gate));
            EditorUtility.SetDirty(world);
            Transform ground = geometry.Find("Ground");
            ground.localScale = new Vector3(Width * Cell, 1f, Depth * Cell);
            ground.localPosition = new Vector3(Width * Cell * 0.5f, -0.5f, Depth * Cell * 0.5f);
            ground.GetComponent<Renderer>().enabled = false;
            var dresser = world.GetComponent<CastleKitDresser>();
            if (dresser != null)
            {
                var dresserData = new SerializedObject(dresser);
                dresserData.FindProperty("closedSideCells").intValue = BlockedSides;
                dresserData.ApplyModifiedPropertiesWithoutUndo();
                // 5m 칸 계단 방 값(5m 스파이크 기본값). 7.5m 칸 값(층참 2m, 계단 폭 2.16m)을 되돌린다.
                var kit = (CastleKitSettings)dresserData.FindProperty("settings").objectReferenceValue;
                if (kit != null)
                {
                    var kitData = new SerializedObject(kit);
                    kitData.FindProperty("roomLandingDepth").floatValue = 1f;
                    kitData.FindProperty("roomFlightWidth").floatValue = 1.46f;
                    kitData.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(kit);
                }
                EditorUtility.SetDirty(dresser);
            }
            if (world.GetComponent<ConstructionEditPreview>() == null) world.gameObject.AddComponent<ConstructionEditPreview>();

            // 3. 링 시절 꾸밈을 걷어 내고, 분지 배경·끝 탑·기초를 옮긴다(길찾기에 들도록 Geometry 아래).
            foreach (string name in RingDecor)
            {
                Transform old = geometry.Find(name);
                if (old != null) { Object.DestroyImmediate(old.gameObject); log.Add("removed " + name); }
            }
            // 다시 실행할 때 지난번에 옮긴 것을 지운다(같은 이름이 여럿일 수 있다).
            var stale = new HashSet<string>(EnvironmentRoots.Concat(new[] { "EndTowers", "Foundation" }));
            foreach (Transform parent in new[] { geometry, world.transform })
                foreach (Transform child in parent.Cast<Transform>().ToList())
                    if (stale.Contains(child.name)) Object.DestroyImmediate(child.gameObject);
            // 초안 씬에는 같은 이름의 루트가 여럿 있을 수 있다(들판을 여러 번 깔았다). 보이는 모습 그대로 모두 옮기고, 빈 것은 버린다.
            foreach (string name in EnvironmentRoots)
            {
                var sources = basin.GetRootGameObjects().Where(root => root.name == name && root.GetComponentsInChildren<Renderer>(true).Length > 0).ToList();
                if (sources.Count == 0) { log.Add("missing " + name); continue; }
                foreach (GameObject source in sources)
                {
                    SceneManager.MoveGameObjectToScene(source, game);
                    source.transform.SetParent(geometry, true);
                }
                log.Add("moved " + name + " x" + sources.Count);
            }
            foreach (string name in new[] { "EndTowers", "Foundation" })
            {
                Transform source = draft.transform.Find(name);
                if (source == null) { log.Add("missing " + name); continue; }
                source.SetParent(null, true);
                SceneManager.MoveGameObjectToScene(source.gameObject, game);
                source.SetParent(geometry, true);
                log.Add("moved " + name);
            }
            log.Add("nav-excluded unreadable colliders x" + ExcludeUnreadableNavSources(geometry));
            // 데모 씬의 해는 끈다(Game의 Sun 하나만 둔다).
            foreach (Light light in geometry.GetComponentsInChildren<Light>(true))
                if (light.type == LightType.Directional) light.gameObject.SetActive(false);

            // 4. 시설은 임시로 성문 안쪽 마을에 줄지어 둔다(내성 단계에서 데모 집에 연결). NPC는 시설 입구 앞으로.
            Transform layout = geometry.Find("CourtyardLayout");
            Vector3 gateInside = world.transform.TransformPoint(new Vector3((GateX + 0.5f) * Cell, 0f, -12f));
            if (layout != null)
            {
                int i = 0;
                foreach (Transform facility in layout)
                {
                    // 링 시절 시설 건물(집 모델과 충돌 상자)은 걷어 낸다. 데모 마을의 집이 시설 건물이 된다(사용자 2026-11-28).
                    // 시설 기능(FacilityStation·ConstructionStation)이 붙은 본체는 NPC가 참조하므로 보이지 않는 기준점으로 남긴다.
                    Transform visual = facility.Find("Visual");
                    if (visual != null) Object.DestroyImmediate(visual.gameObject);
                    foreach (Collider body in facility.GetComponents<Collider>()) body.enabled = false;
                    Vector3 spot = gateInside + new Vector3((i - 2.5f) * 24f, 0f, -20f - (i % 2) * 18f);
                    facility.SetPositionAndRotation(Ground(spot, world.transform), Quaternion.Euler(0f, 180f, 0f));
                    i++;
                }
                foreach (GameObject root in game.GetRootGameObjects())
                {
                    if (root.name != "Town") continue;
                    foreach (Transform npc in root.transform)
                    {
                        if (!npc.name.StartsWith("Npc_")) continue;
                        Transform facility = layout.Find(npc.name.Substring(4));
                        if (facility == null) continue;
                        npc.SetPositionAndRotation(Ground(facility.position + Vector3.forward * 9f, world.transform), Quaternion.Euler(0f, 180f, 0f));
                    }
                }
            }
            // 시설을 고른 데모 집에 붙인다: 기준점은 집 가운데 땅, NPC는 집에서 성문 쪽을 향한 면 바로 앞에서 성문 쪽(다가오는 플레이어)을 본다.
            if (layout != null) PlaceFacilitiesAtHouses(game, geometry, layout, world.transform, gateInside, log);

            // 링 시절 주민 8명과 전투 시작 깃발은 걷어 낸다(사용자 2026-11-28). 주민은 데모 마을 길에 맞춰 나중에 새로 놓는다.
            Transform startStation = geometry.Find("BattleStartStation");
            if (startStation != null) { Object.DestroyImmediate(startStation.gameObject); log.Add("removed BattleStartStation"); }
            foreach (GameObject root in game.GetRootGameObjects())
            {
                if (root.name != "Town") continue;
                var director = root.GetComponent<ProjectNeo.Town.TownDirector>();
                if (director != null)
                {
                    var data = new SerializedObject(director);
                    SerializedProperty residents = data.FindProperty("residents");
                    for (int r = residents.arraySize - 1; r >= 0; r--)
                    {
                        var resident = residents.GetArrayElementAtIndex(r).objectReferenceValue as Component;
                        if (resident == null || resident.name.StartsWith("Citizen_"))
                        {
                            residents.GetArrayElementAtIndex(r).objectReferenceValue = null;
                            residents.DeleteArrayElementAtIndex(r);
                        }
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(director);
                }
                int removed = 0;
                foreach (Transform child in root.transform.Cast<Transform>().ToList())
                    if (child.name.StartsWith("Citizen_")) { Object.DestroyImmediate(child.gameObject); removed++; }
                if (removed > 0) log.Add("removed citizens x" + removed);
            }

            // 5. 부대·플레이어는 성문 안쪽 길에서 성벽(북)을 본다.
            Vector3[] squadPoints = { gateInside + new Vector3(-14f, 0f, 0f), gateInside + new Vector3(14f, 0f, 0f), gateInside + new Vector3(0f, 0f, -6f) };
            foreach (GameObject root in game.GetRootGameObjects())
            {
                for (int squad = 0; squad < squadPoints.Length; squad++)
                    for (int i = 0; i < 4; i++)
                        if (root.name == "Squad" + (squad + 1) + "_" + (i + 1))
                            Place(root.transform, Ground(squadPoints[squad] + new Vector3((i % 2 - 0.5f) * 1.2f, 0f, -(i / 2) * 1.2f), world.transform));
                if (root.name == "Player") Place(root.transform, Ground(gateInside + new Vector3(0f, 0f, -16f), world.transform));
            }

            // 6. 적 출현 칸, 건설 카메라, 사다리.
            ApplySpawnCells();
            var cameraSettings = Required<BuildCameraSettings>(Data + "/BuildCamera.asset");
            var cameraData = new SerializedObject(cameraSettings);
            cameraData.FindProperty("orthographicSize").floatValue = 70f;
            cameraData.FindProperty("distance").floatValue = 320f;
            cameraData.FindProperty("initialFocus").vector2Value = new Vector2(0.5f, 0.35f);
            cameraData.FindProperty("minZoom").floatValue = 15f;
            cameraData.FindProperty("maxZoom").floatValue = 220f;
            cameraData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cameraSettings);
            var orbit = Find<ConstructionCameraController>(game);
            orbit.Configure(world, cameraSettings);
            var buildCamera = orbit.GetComponent<Camera>();
            buildCamera.orthographicSize = cameraSettings.OrthographicSize;
            buildCamera.farClipPlane = 2500f;
            buildCamera.transform.rotation = Quaternion.Euler(cameraSettings.Pitch, cameraSettings.InitialYaw, 0f);
            buildCamera.transform.position = world.transform.TransformPoint(new Vector3(Width * Cell * 0.5f, 0f, Depth * Cell * 0.35f))
                - buildCamera.transform.forward * cameraSettings.Distance;
            EditorUtility.SetDirty(orbit);
            Find<FirstPersonCamera>(game).GetComponent<Camera>().farClipPlane = 2500f;
            GameBattleSetup.ConfigureScene(game);

            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            AssetDatabase.SaveAssets();
            int renderers = game.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Renderer>(true).Length);
            log.Add("Game renderers=" + renderers);
            return string.Join("\n", log);
        }

        [MenuItem("ProjectNeo/Setup/Exclude Unreadable NavMesh Sources In Game Scene (BUG-curtain-navmesh-read)")]
        public static string ApplyNavSourceExclusion()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            EditorSceneManager.SaveOpenScenes();
            Scene game = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            Transform geometry = Find<ConstructionWorld>(game).transform.Find("Geometry");
            int count = ExcludeUnreadableNavSources(geometry);
            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            return "nav-excluded unreadable colliders x" + count;
        }

        /// <summary>
        /// BUG-curtain-navmesh-read: 실행 중 NavMesh 굽기(<see cref="ConstructionWorld"/>, 물리 충돌체 수집)는 MeshCollider의
        /// 메시를 읽는다. Read/Write가 꺼진 라이선스 메시(커튼·그림 같은 꾸밈)는 오류를 내고 플레이어 빌드에서는 빠진다.
        /// 첫 굽기는 <c>ConstructionWorld.Awake</c>에서 돌아 다른 오브젝트의 NavMeshModifier가 아직 켜지기 전이므로 제외 표시로는 막을 수 없다.
        /// 그래서 그 MeshCollider는 끄고, 메시 경계에 맞춘 BoxCollider로 바꾼다(원래 켜져 있던 것만 켠다).
        /// 에셋 가져오기 설정(서브모듈)은 건드리지 않는다. 다시 실행해도 같은 결과다.
        /// </summary>
        public static int ExcludeUnreadableNavSources(Transform geometry)
        {
            int count = 0;
            foreach (MeshCollider collider in geometry.GetComponentsInChildren<MeshCollider>(true))
            {
                if (collider.sharedMesh == null || collider.sharedMesh.isReadable) continue;
                GameObject owner = collider.gameObject;
                bool wasEnabled = collider.enabled;
                if (owner.TryGetComponent(out Unity.AI.Navigation.NavMeshModifier modifier)) Object.DestroyImmediate(modifier);
                if (!owner.TryGetComponent(out BoxCollider box))
                {
                    box = owner.AddComponent<BoxCollider>();
                    box.center = collider.sharedMesh.bounds.center;
                    box.size = collider.sharedMesh.bounds.size;
                    box.isTrigger = collider.isTrigger;
                    box.sharedMaterial = collider.sharedMaterial;
                    box.enabled = wasEnabled;
                }
                collider.enabled = false;
                EditorUtility.SetDirty(owner);
                count++;
            }
            return count;
        }

        static void PlaceFacilitiesAtHouses(Scene game, Transform geometry, Transform layout, Transform worldRoot, Vector3 gateInside, List<string> log)
        {
            Transform demo = geometry.Find("DemoWorld");
            if (demo == null) { log.Add("no DemoWorld for facilities"); return; }
            Transform town = game.GetRootGameObjects().FirstOrDefault(root => root.name == "Town")?.transform;
            foreach ((string facilityName, string houseName, Vector2 near) in FacilityHouses)
            {
                Transform facility = layout.Find(facilityName);
                Transform house = demo.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name == houseName && t.GetComponentsInChildren<Renderer>(true).Length > 0)
                    .OrderBy(t => (new Vector2(t.position.x, t.position.z) - near).sqrMagnitude).FirstOrDefault();
                if (facility == null || house == null) { log.Add("missing house for " + facilityName); continue; }
                Renderer[] parts = house.GetComponentsInChildren<Renderer>(true);
                Bounds bounds = parts[0].bounds;
                foreach (Renderer part in parts) bounds.Encapsulate(part.bounds);
                Vector3 centre = new Vector3(bounds.center.x, 0f, bounds.center.z);
                facility.SetPositionAndRotation(Ground(centre, worldRoot), Quaternion.identity);
                // 성문 쪽 방향의 집 가장자리 바깥 2.5m에 NPC.
                Vector3 toGate = gateInside - centre; toGate.y = 0f; toGate.Normalize();
                float reach = Mathf.Abs(toGate.x) * bounds.extents.x + Mathf.Abs(toGate.z) * bounds.extents.z + 2.5f;
                Transform npc = town != null ? town.Find("Npc_" + facilityName) : null;
                if (npc != null)
                {
                    // 사용자가 문 앞에 놓은 자리가 있으면 그대로, 없으면 성문 쪽 면 앞(임시).
                    int spot = Array.FindIndex(NpcSpots, s => s.facility == facilityName);
                    if (spot >= 0) npc.SetPositionAndRotation(NpcSpots[spot].position, Quaternion.Euler(0f, NpcSpots[spot].yaw, 0f));
                    else npc.SetPositionAndRotation(Ground(centre + toGate * reach, worldRoot), Quaternion.LookRotation(toGate));
                    // Play 때 NPC는 기억한 집 자리로 돌아간다(TownResident.ShowAtHome). 링 시절 값을 지금 자리로 바꾼다.
                    var resident = npc.GetComponent<ProjectNeo.Town.TownResident>();
                    if (resident != null)
                    {
                        var residentData = new SerializedObject(resident);
                        residentData.FindProperty("homePosition").vector3Value = npc.position;
                        residentData.FindProperty("homeYaw").floatValue = npc.eulerAngles.y;
                        residentData.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(resident);
                    }
                }
                log.Add(facilityName + " -> " + houseName + " at (" + centre.x.ToString("0") + ", " + centre.z.ToString("0") + ")");
            }
            // 링 시절 걷기 지점(주민용)과 피난처(전투 때 숨는 곳)는 들판 쪽이다. 주민이 없으니 걷기 지점은 비우고,
            // 피난처는 각 NPC 자리(자기 집 문 앞)로 둔다.
            var director = town != null ? town.GetComponent<ProjectNeo.Town.TownDirector>() : null;
            if (director != null)
            {
                var data = new SerializedObject(director);
                data.FindProperty("wanderPoints").arraySize = 0;
                SerializedProperty shelters = data.FindProperty("shelters");
                var spots = town.Cast<Transform>().Where(t => t.name.StartsWith("Npc_")).Select(t => t.position).ToList();
                shelters.arraySize = spots.Count;
                for (int i = 0; i < spots.Count; i++) shelters.GetArrayElementAtIndex(i).vector3Value = spots[i];
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(director);
            }
        }

        /// <summary>시작 배치: 성벽 줄의 1~40번 칸, 29번 칸은 고정 성문 위에 고정 성벽 한 층(2층 성문탑).</summary>
        static InitialBuildPiece[] Seeds(BuildPieceDefinition wall, BuildPieceDefinition gate)
        {
            var seeds = new List<InitialBuildPiece>();
            for (int x = BlockedSides; x < Width - BlockedSides; x++)
                if (x != GateX) seeds.Add(new InitialBuildPiece { definition = wall, cell = new Vector2Int(x, WallRow) });
            seeds.Add(new InitialBuildPiece { definition = gate, cell = new Vector2Int(GateX, WallRow), fixedGate = true });
            seeds.Add(new InitialBuildPiece { definition = wall, cell = new Vector2Int(GateX, WallRow), fixedGate = true });
            return seeds.ToArray();
        }

        static void ApplySpawnCells()
        {
            var battle = AssetDatabase.LoadAssetAtPath<GameBattleSettings>(Data + "/Battle.asset");
            if (battle != null) SetVector2(battle, "spawnCell", SpawnCell);
            (string asset, float offset)[] points = { ("SpawnLeft", -StageSetup.SideOffsetCells), ("SpawnCenter", 0f), ("SpawnRight", StageSetup.SideOffsetCells) };
            foreach ((string asset, float offset) in points)
            {
                var point = AssetDatabase.LoadAssetAtPath<ProjectNeo.Stages.SpawnPointDefinition>(StageSetup.Folder + "/" + asset + ".asset");
                if (point != null) SetVector2(point, "cell", SpawnCell + Vector2.right * offset);
            }
        }

        static void SetVector2(Object target, string property, Vector2 value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(property).vector2Value = value;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        // 그 자리의 땅 높이(성·유닛·NPC는 빼고 가장 높은 충돌면).
        static Vector3 Ground(Vector3 point, Transform world)
        {
            Physics.SyncTransforms();
            float best = float.NegativeInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(point.x, point.y + 400f, point.z), Vector3.down, 800f))
            {
                if (hit.collider.GetComponentInParent<ProjectNeo.Combat.CharacterActor>() != null) continue;
                if (hit.collider.GetComponentInParent<PlacedStructure>() != null) continue;
                best = Mathf.Max(best, hit.point.y);
            }
            return new Vector3(point.x, float.IsNegativeInfinity(best) ? world.position.y : best + 0.05f, point.z);
        }

        static void Place(Transform target, Vector3 position)
        {
            target.SetPositionAndRotation(position, Quaternion.identity);
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        static T Find<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault()
            ?? throw new InvalidOperationException(scene.path + " has no " + typeof(T).Name);

        static T Required<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing asset: " + path);
    }
}
