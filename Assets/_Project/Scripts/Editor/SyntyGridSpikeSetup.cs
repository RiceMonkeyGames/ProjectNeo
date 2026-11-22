using System;
using System.Collections.Generic;
using System.Linq;
using ProjectNeo.Stats;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// SIEGE-14 시험: 5m 격자 + PolygonFantasyKingdom 성 킷. 성벽 지휘 프로토타입 씬을 복사해 격자·조각 정의만 5m로 바꾸고
    /// 성벽 칸을 킷 조각으로 꾸민다(<see cref="CastleKitDresser"/>). Game 씬과 공용 규칙 값은 건드리지 않는다.
    /// 다시 실행하면 시험 씬을 새로 만든다.
    /// </summary>
    public static class SyntyGridSpikeSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/SyntyGridSpike.unity";
        const string Data = "Assets/_Project/Data/SyntyGridSpike";
        const string Prefabs = "Assets/_Project/Prefabs/SyntyGridSpike";
        const string KitFolder = "Assets/_Project/Prefabs/CastleKit";
        const string Castle = "Assets/ThirdParty/PolygonFantasyKingdom/Prefabs/Castle/";
        const float Cell = 5f;
        const float StairsHealth = 50f;
        const float TowerHealth = 400f;
        const float TowerRangedBonus = 0.2f;
        // 지붕 무기 자리: 망루 가운데에서 ±1.3m(정규화 0.13). 계단 위 지붕 구멍은 늘 가운데에서 2.5m 밖이라 대포(길이 1.8m)가 걸치지 않는다.
        const float TowerMount = 0.13f;

        // Doorway: 메쉬 충돌체 대신 문 구멍을 넓힌 상자 셋(데모 문 구멍 약 1.25m는 길찾기 반경 0.5m로 막힌다).
        // Exact: 원본의 단순화 볼록 충돌체(흉벽은 총안까지 막힌 상자) 대신 보이는 메쉬 그대로(총안으로 쏠 수 있다).
        enum Body { None = 0, Solid = 1, FrameOnly = 2, Doorway = 3, Exact = 4 }
        // 충돌: 튀어나온 기둥·장식 벽판, 성벽 위 계단, 흉벽(성가퀴·모서리 기둥)만 남긴다(길찾기 메시에 들어간다). 벽판 몸통은 조각 상자가 맡고,
        // 성문 통로는 조각의 문 충돌체가 맡는다. 흉벽은 모델 모양 그대로 막아 통로 밖으로 떨어지지 않게 한다(사용자 요청 2026-11-23).
        // 흉벽 충돌체는 Game 성가퀴처럼 ShotCover(아군 통과)를 단다. 성가퀴 자리·웅크림 같은 나머지 엄폐 규칙은 Game 통합 때 팀원과 맞춘다.
        static readonly (CastleKitPiece piece, string model, Body body)[] Kit =
        {
            (CastleKitPiece.Panel, "SM_Bld_Castle_Wall_01", Body.None),
            (CastleKitPiece.PanelAccent, "SM_Bld_Castle_Wall_04", Body.Solid),
            (CastleKitPiece.Pillar, "SM_Bld_Castle_Pillar_Stone_05", Body.Solid),
            (CastleKitPiece.Floor, "SM_Bld_Castle_Floor_Wood_01", Body.None),
            (CastleKitPiece.Battlement, "SM_Bld_Castle_Battlements_01", Body.Exact),
            (CastleKitPiece.BattlementCap, "SM_Bld_Castle_Battlements_Pillar_02", Body.Exact),
            (CastleKitPiece.GateOuter, "SM_Bld_Castle_Wall_Gate_01", Body.None),
            (CastleKitPiece.GateInner, "SM_Bld_Castle_Wall_Gate_01", Body.FrameOnly),
            (CastleKitPiece.WallStairs, "SM_Bld_Stairs_Wood_Straight_01", Body.Solid),
            // 머리 위(맨 윗층 꼭대기 아래 약 0~3m)라 길찾기·이동에 닿지 않으므로 충돌체를 뺀다.
            (CastleKitPiece.InnerRelief, "SM_Bld_Castle_Wall_Panel_Stone_02", Body.None),
            // 무너진 끝 단면과 돌무더기는 외형 전용(무너진 칸 통로를 막지 않는다).
            (CastleKitPiece.RubbleTop, "SM_Bld_Castle_DestroyedWall_Rubble_Top_01", Body.None),
            (CastleKitPiece.RubbleMiddle, "SM_Bld_Castle_DestroyedWall_Rubble_Middle_01", Body.None),
            (CastleKitPiece.RubbleBottom, "SM_Bld_Castle_DestroyedWall_Rubble_Bottom_01", Body.None),
            (CastleKitPiece.RubblePile, "SM_Bld_Castle_DestroyedWall_RubblePile_01", Body.None),
            (CastleKitPiece.PanelHalf, "SM_Bld_Castle_Wall_Cut_01", Body.None),
            (CastleKitPiece.BrokenTop, "SM_Bld_Castle_DestroyedWall_Top_01", Body.None),
            (CastleKitPiece.BrokenMiddle, "SM_Bld_Castle_DestroyedWall_Middle_01", Body.None),
            (CastleKitPiece.BrokenBottom, "SM_Bld_Castle_DestroyedWall_Bottom_01", Body.None),
            (CastleKitPiece.BattlementHalf, "SM_Bld_Castle_Battlements_01_Half", Body.None),
            (CastleKitPiece.CornerStairs, "SM_Bld_Stairs_Wood_Corner_01", Body.Solid),
            (CastleKitPiece.FloorHalf, "SM_Bld_Castle_Floor_Wood_03", Body.None),
            // 망루(데모 탑): 맨윗층은 조각 상자 대신 이 벽판들이 충돌을 맡는다(방 안으로 걸어 들어간다).
            (CastleKitPiece.TowerPanel, "SM_Bld_Castle_Wall_01", Body.Solid),
            (CastleKitPiece.TowerArrowslit, "SM_Bld_Castle_Wall_Arrowslit_01", Body.Solid),
            (CastleKitPiece.TowerDoor, "SM_Bld_Castle_Wall_Door_01", Body.Doorway),
            (CastleKitPiece.TowerCorbel, "SM_Bld_Castle_Wall_Corbels_01", Body.None),
            (CastleKitPiece.TowerCorbelCorner, "SM_Bld_Castle_Wall_Corbels_Corner_01", Body.None),
            (CastleKitPiece.TowerCap, "SM_Bld_Castle_Battlements_Pillar_01", Body.Exact),
        };
        const string KeysHint = "시험 키  P: 쇠창살 올림/내림 · O: 성문 열기/닫기 · B: 성문 부수기/고치기 · K: 조준한 성벽·계단·망루 무너뜨리기";

        [MenuItem("ProjectNeo/Setup/Create Synty Grid Spike (SIEGE-14)")]
        public static void Create()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WallCommandSetup.ScenePath) == null)
                throw new InvalidOperationException("Run Create Wall Command Prototype first.");
            CombatPrototypeSetup.EnsureFolder(Data);
            CombatPrototypeSetup.EnsureFolder(Prefabs);
            CombatPrototypeSetup.EnsureFolder(KitFolder);

            CreateKit();
            Replace(BuildGridDefinition.Create(16, 14, Cell, 3, forbidValleys: true), Data + "/Grid.asset");
            Material stone = CombatPrototypeSetup.MaterialAsset("ConstructionStone", new Color(0.58f, 0.66f, 0.75f));
            Replace(BuildPieceDefinition.Create(BuildPieceKind.Wall, "성벽", Vector2Int.one, Cell,
                prefab: KitStructurePrefab("KitWall", stone)), Data + "/Wall.asset");
            // 계단은 성벽(체력 100)보다 쉽게 무너진다(사용자 결정 2026-11-23, 임시 값 50). 무너진 성벽 뒤에 남아 틈을 막으므로,
            // 적이 부수고 들어올 수 있게 약하게 둔다. 적이 계단을 공격 대상으로 고르는 규칙은 Game 통합 때 팀원과 맞춘다.
            Replace(BuildPieceDefinition.Create(BuildPieceKind.Stairs, "계단", Vector2Int.one, Cell, StairsHealth,
                Required<GameObject>("Assets/_Project/Prefabs/ConstructionPrototype/Stairs.prefab")), Data + "/Stairs.asset");
            Replace(BuildPieceDefinition.Create(BuildPieceKind.Gate, "메인 성문", Vector2Int.one, Cell, 300f,
                Required<GameObject>("Assets/_Project/Prefabs/ConstructionPrototype/MainGate.prefab")), Data + "/Gate.asset");
            // 보루(SIEGE-11)도 5m 한 칸. 프리팹·설치 자리(윗면 가운데)는 3m 보루와 같다.
            BuildPieceDefinition source = BastionSetup.CreateBastion();
            Replace(BuildPieceDefinition.Create(BuildPieceKind.Bastion, "보루", Vector2Int.one, Cell, BastionSetup.MaxHealth,
                source.Prefab, new BuildMountPoint("top", Vector3.up)), Data + "/Bastion.asset");
            // 망루(사용자 결정 2026-11-23): 2×2, 층마다 한 칸 높이, 지붕 칸마다 무기 자리 하나, 지붕 위 아군 원거리 사거리·공격력 +20%.
            // 체력은 임시 값(보루 150보다 강하게). 값은 에셋에서 바꾼다.
            var tower = BuildPieceDefinition.Create(BuildPieceKind.Tower, "망루", new Vector2Int(2, 2), Cell, TowerHealth,
                KitStructurePrefab("KitTower", CombatPrototypeSetup.MaterialAsset("ConstructionTower", new Color(0.62f, 0.58f, 0.5f))),
                new BuildMountPoint("roof_sw", new Vector3(-TowerMount, 1f, -TowerMount)), new BuildMountPoint("roof_se", new Vector3(TowerMount, 1f, -TowerMount)),
                new BuildMountPoint("roof_nw", new Vector3(-TowerMount, 1f, TowerMount)), new BuildMountPoint("roof_ne", new Vector3(TowerMount, 1f, TowerMount)));
            tower.SetOccupantModifiers(StatModifier.Percent(StatType.AttackRange, TowerRangedBonus),
                StatModifier.Percent(StatType.Attack, TowerRangedBonus));
            Replace(tower, Data + "/Tower.asset");
            var camera = BuildCameraSettings.Create();
            var serializedCamera = new SerializedObject(camera);
            serializedCamera.FindProperty("orthographicSize").floatValue = 34f;
            serializedCamera.FindProperty("distance").floatValue = 80f;
            serializedCamera.FindProperty("maxZoom").floatValue = 200f;
            serializedCamera.ApplyModifiedPropertiesWithoutUndo();
            Replace(camera, Data + "/Camera.asset");
            AssetDatabase.SaveAssets();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(WallCommandSetup.ScenePath, ScenePath)) throw new InvalidOperationException("Scene copy failed.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // 씬을 열면 앞서 만든 에셋 참조가 끊기므로 경로로 다시 읽는다(BattlementSetup과 같다).
            var kit = Required<CastleKitSettings>(Data + "/CastleKit.asset");
            var grid = Required<BuildGridDefinition>(Data + "/Grid.asset");
            var wall = Required<BuildPieceDefinition>(Data + "/Wall.asset");
            var stairs = Required<BuildPieceDefinition>(Data + "/Stairs.asset");
            var gate = Required<BuildPieceDefinition>(Data + "/Gate.asset");
            var bastion = Required<BuildPieceDefinition>(Data + "/Bastion.asset");
            tower = Required<BuildPieceDefinition>(Data + "/Tower.asset");
            camera = Required<BuildCameraSettings>(Data + "/Camera.asset");
            ConstructionWorld world = Find<ConstructionWorld>(scene);
            Transform geometry = world.transform.Find("Geometry");
            world.transform.position = new Vector3(-grid.Width * Cell * 0.5f, 0f, -grid.Depth * Cell * 0.5f);
            Transform ground = geometry.Find("Ground");
            ground.localScale = new Vector3(grid.Width * Cell, 1f, grid.Depth * Cell);
            ground.localPosition = new Vector3(grid.Width * Cell * 0.5f, -0.5f, grid.Depth * Cell * 0.5f);
            world.Configure(grid, geometry, geometry.GetComponent<Unity.AI.Navigation.NavMeshSurface>(), Seeds(wall, stairs, gate, bastion, tower));
            EditorUtility.SetDirty(world);

            Find<ConstructionCameraController>(scene).Configure(world, camera);
            ConstructionHud hud = Find<ConstructionHud>(scene);
            var serializedHud = new SerializedObject(hud);
            serializedHud.FindProperty("wall").objectReferenceValue = wall;
            serializedHud.FindProperty("stairs").objectReferenceValue = stairs;
            serializedHud.FindProperty("bastion").objectReferenceValue = bastion;
            serializedHud.FindProperty("tower").objectReferenceValue = tower;
            serializedHud.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);
            AddBastionButton(hud);
            AddTowerButton(hud);

            var dresser = world.gameObject.AddComponent<CastleKitDresser>();
            dresser.Configure(world, kit);
            world.gameObject.AddComponent<TowerOccupantBuff>().Configure(world);
            EditorUtility.SetDirty(dresser);
            world.gameObject.AddComponent<CastleKitTestKeys>().Configure(world, dresser);
            AddHint(Find<ProjectNeo.UI.SquadCommandHud>(scene).transform);
            AddHint(hud.transform);

            // 1인칭 플레이어를 계단 앞 안뜰에 둔다(계단 칸 (5, 7)의 한 칸 아래).
            GameObject player = scene.GetRootGameObjects().First(root => root.name == "Player");
            player.transform.position = world.transform.position + new Vector3(5.5f * Cell, 0.1f, 5.5f * Cell);
            player.transform.rotation = Quaternion.identity;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            // 조각 정의를 새로 만들었으므로 저장 콘텐츠 목록(모든 조각 정의를 담아야 한다)을 다시 맞춘다.
            RunSaveSetup.Apply();
        }

        // 닫힌 성벽 고리(x 3~12, z 3~10)라 안뜰이 늘 안쪽이다(안쪽 면 꾸밈·성가퀴 규칙 확인). 북쪽 줄 가운데 성문(바깥 = 북),
        // 안뜰에서 북쪽 성벽으로 오르는 계단, 북쪽 줄 동쪽 2단(자동 계단 확인), 북쪽 바깥 보루.
        static InitialBuildPiece[] Seeds(BuildPieceDefinition wall, BuildPieceDefinition stairs, BuildPieceDefinition gate,
            BuildPieceDefinition bastion, BuildPieceDefinition tower)
        {
            const int West = 3, East = 12, South = 3, North = 10, GateX = 7;
            var seeds = new List<InitialBuildPiece>();
            void Wall(int x, int z) => seeds.Add(new InitialBuildPiece { definition = wall, cell = new Vector2Int(x, z) });
            for (int x = West; x <= East; x++)
            {
                Wall(x, South);
                if (x != GateX) Wall(x, North);
            }
            for (int z = South + 1; z < North; z++) { Wall(West, z); Wall(East, z); }
            seeds.Add(new InitialBuildPiece { definition = gate, cell = new Vector2Int(GateX, North), fixedGate = true });
            // 성문 칸은 기본 2층(사용자 결정 2026-11-22): 성문 위에 고정 성벽 한 층을 얹어 올린 쇠창살이 위층 벽 속에 숨는다.
            // 고정 조각이라 철거할 수 없고(무너짐은 예외), 양옆 1층 성벽 통로에는 자동 계단이 생긴다.
            seeds.Add(new InitialBuildPiece { definition = wall, cell = new Vector2Int(GateX, North), fixedGate = true });
            // 북쪽 줄 8~11은 성문탑(7)과 이어진 2층(골짜기 금지). 서쪽 6은 직선 계단, 동쪽 끝 12는 모서리 계단 사례.
            Wall(8, North); Wall(9, North); Wall(10, North); Wall(11, North);
            seeds.Add(new InitialBuildPiece { definition = stairs, cell = new Vector2Int(5, North - 1) });
            seeds.Add(new InitialBuildPiece { definition = bastion, cell = new Vector2Int(5, North + 1) });
            // 북쪽 2층 줄(9~10) 바깥의 망루: 이웃 성벽보다 한 층 높은 3층(맨윗층은 통로 높이의 방, 지붕에 무기·궁수).
            for (int tier = 0; tier < 3; tier++) seeds.Add(new InitialBuildPiece { definition = tower, cell = new Vector2Int(9, North + 1) });
            return seeds.ToArray();
        }

        // 화면 아래 가운데에 시험 키 안내(1인칭·건설 화면 모두).
        static void AddHint(Transform canvas)
        {
            var root = new GameObject("SpikeKeysHint", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            root.transform.SetParent(canvas, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 12f);
            rect.sizeDelta = new Vector2(1100f, 32f);
            var label = root.GetComponent<TMPro.TextMeshProUGUI>();
            label.font = Required<TMPro.TMP_FontAsset>("Assets/_Project/Art/Fonts/NotoSansKR SDF.asset");
            label.text = KeysHint;
            label.fontSize = 18f;
            label.alignment = TMPro.TextAlignmentOptions.Bottom;
            label.raycastTarget = false;
        }

        // 보루 버튼 아래에 망루 버튼(시험 씬 전용).
        static void AddTowerButton(ConstructionHud hud)
        {
            Transform panel = hud.transform.Find("BuildPanel");
            Transform bastion = panel != null ? panel.Find("Button_Bastion") : null;
            if (bastion == null || panel.Find("Button_Tower") != null) return;
            GameObject button = ConstructionPrototypeSetup.Button(panel, "Tower", hud.SelectTower, "망루");
            button.transform.SetSiblingIndex(bastion.GetSiblingIndex() + 1);
            if (panel is RectTransform rect) rect.sizeDelta += new Vector2(0f, 50f);
        }

        // 건설 화면의 계단 버튼 아래에 보루 버튼(성벽 지휘 씬에는 없다).
        static void AddBastionButton(ConstructionHud hud)
        {
            Transform panel = hud.transform.Find("BuildPanel");
            Transform stairs = panel != null ? panel.Find("Button_Stairs") : null;
            if (stairs == null || panel.Find("Button_Bastion") != null) return;
            GameObject button = ConstructionPrototypeSetup.Button(panel, "Bastion", hud.SelectBastion, "보루");
            button.transform.SetSiblingIndex(stairs.GetSiblingIndex() + 1);
            if (panel is RectTransform rect) rect.sizeDelta += new Vector2(0f, 50f);
        }

        static void CreateKit()
        {
            var converted = new Dictionary<Material, Material>();
            // 시험 전용 폴더라 매번 새로 만든다(조각 이름이 바뀌어도 옛 프리팹이 남지 않게).
            AssetDatabase.DeleteAsset(KitFolder);
            CombatPrototypeSetup.EnsureFolder(KitFolder);
            var kit = Replace(CastleKitSettings.Create(), Data + "/CastleKit.asset");
            foreach ((CastleKitPiece piece, string model, Body body) in Kit)
                kit.SetPrefab(piece, KitCopy(model, piece.ToString(), body, converted));
            EditorUtility.SetDirty(kit);
        }

        // 원본 프리팹과 연결하지 않은 복사본(재질은 프로젝트 URP 복사본). Solid만 충돌체를 남기고, 없으면 메쉬 충돌체를 단다.
        static GameObject KitCopy(string model, string name, Body body, Dictionary<Material, Material> converted)
        {
            var source = Required<GameObject>(Castle + model + ".prefab");
            GameObject instance = Object.Instantiate(source);
            instance.name = name;
            try
            {
                if (body == Body.FrameOnly)
                    foreach (Transform part in instance.GetComponentsInChildren<Transform>(true))
                        if (part != instance.transform && (part.name.Contains("Door") || part.name.Contains("Portcullis")))
                            Object.DestroyImmediate(part.gameObject);
                // Solid은 모양 그대로의 메쉬 충돌체만 남긴다(상자 충돌체는 장식 기둥 사이까지 막는다).
                foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                    if (body != Body.Solid || !(collider is MeshCollider)) Object.DestroyImmediate(collider);
                if (body == Body.Solid && instance.GetComponentInChildren<MeshCollider>(true) == null)
                    foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
                        filter.gameObject.AddComponent<MeshCollider>();
                if (body == Body.Doorway) AddDoorwayColliders(instance);
                if (body == Body.Exact)
                    foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var exact = filter.gameObject.AddComponent<MeshCollider>();
                        exact.sharedMesh = filter.sharedMesh;
                        exact.convex = false;
                        // Game 성가퀴와 같은 엄폐 규칙(SIEGE-10): 아군 사격은 지나가고 적 사격만 막는다. 이동은 모두 막는다.
                        filter.gameObject.AddComponent<ProjectNeo.Combat.ShotCover>().Configure(ProjectNeo.Combat.CombatTeam.Allied);
                    }
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = ProjectCopy(materials[i], converted);
                    renderer.sharedMaterials = materials;
                }
                return PrefabUtility.SaveAsPrefabAsset(instance, KitFolder + "/" + name + ".prefab");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        // 문 벽판(Wall_Door_01: 기준점에서 로컬 −x로 5m, 두께 ±0.31m, 높이 5m, 문 구멍은 가운데). 구멍 2m × 2.6m로 넓혀
        // 양옆 벽과 문 위 인방만 막는다. 유닛은 구멍 가운데로 지나가므로 보이는 문틀과 거의 겹치지 않는다.
        static void AddDoorwayColliders(GameObject instance)
        {
            const float Half = 1f, Lintel = 2.6f, Depth = 0.62f;
            void Box(float x0, float x1, float y0, float y1)
            {
                var box = instance.AddComponent<BoxCollider>();
                box.center = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f);
                box.size = new Vector3(x1 - x0, y1 - y0, Depth);
            }
            Box(-5f, -2.5f - Half, 0f, 5f);
            Box(-2.5f + Half, 0f, 0f, 5f);
            Box(-2.5f - Half, -2.5f + Half, Lintel, 5f);
        }

        /// <summary>
        /// 프로젝트 안 URP Lit 복사본(<c>이름_URP.mat</c>, ART-06과 공유). 서브모듈 재질을 로컬에서 URP로 바꿔 둔 경우에도
        /// 서브모듈 재질을 직접 참조하지 않는다(다른 PC에서는 Built-in이라 분홍색이 된다).
        /// </summary>
        static Material ProjectCopy(Material source, Dictionary<Material, Material> converted)
        {
            if (source == null) return null;
            if (AssetDatabase.GetAssetPath(source).StartsWith("Assets/_Project/", StringComparison.Ordinal)) return source;
            if (converted.TryGetValue(source, out Material cached)) return cached;
            CombatPrototypeSetup.EnsureFolder(CastleWallArtSetup.MaterialFolder);
            string path = CastleWallArtSetup.MaterialFolder + "/" + source.name + "_URP.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = source.name + "_URP" };
                Texture texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : null;
                if (texture == null && source.HasProperty("_MainTex")) texture = source.GetTexture("_MainTex");
                Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                    : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", source.HasProperty("_Smoothness") ? source.GetFloat("_Smoothness") : 0.1f);
                material.SetFloat("_Metallic", 0f);
                // 노멀 맵도 원본 그대로(같은 UV·같은 타일링이라 기본 텍스처와 어긋나지 않는다).
                Texture normal = source.HasProperty("_BumpMap") ? source.GetTexture("_BumpMap") : null;
                if (normal != null)
                {
                    material.SetTexture("_BumpMap", normal);
                    material.SetFloat("_BumpScale", source.HasProperty("_BumpScale") ? source.GetFloat("_BumpScale") : 1f);
                    material.EnableKeyword("_NORMALMAP");
                }
                material.enableInstancing = true;
                AssetDatabase.CreateAsset(material, path);
            }
            converted[source] = material;
            return material;
        }

        // 충돌은 단위 상자(칸 크기로 늘어남), 외형은 짓는 중에만 보이는 임시 상자. 다 지으면 킷 조각이 대신 보인다.
        static GameObject KitStructurePrefab(string name, Material stone)
        {
            string path = Prefabs + "/" + name + ".prefab";
            var root = new GameObject(name);
            try
            {
                root.AddComponent<PlacedStructure>();
                var collider = root.AddComponent<BoxCollider>();
                collider.center = Vector3.up * 0.5f;
                collider.size = Vector3.one;
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                GameObject body = CombatPrototypeSetup.Box("Body", Vector3.up * 0.5f, Vector3.one, stone);
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.transform.SetParent(visual.transform, false);
                body.transform.localPosition = Vector3.up * 0.5f;
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static T Replace<T>(T value, string path) where T : Object
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(value, path);
            return value;
        }

        static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();

        static T Required<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing asset: " + path);
    }
}
