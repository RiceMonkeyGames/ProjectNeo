using System;
using System.Collections.Generic;
using System.Linq;
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

        enum Body { None = 0, Solid = 1, FrameOnly = 2 }

        // 충돌: 튀어나온 기둥·장식 벽판과 성벽 위 계단만 남긴다(길찾기 메시에 들어간다). 벽판 몸통은 조각 상자가 맡고,
        // 성문 통로는 조각의 문 충돌체가 맡으며, 흉벽 엄폐는 아직 규칙 쪽(성가퀴) 결정 전이라 넣지 않는다.
        static readonly (CastleKitPiece piece, string model, Body body)[] Kit =
        {
            (CastleKitPiece.Panel, "SM_Bld_Castle_Wall_01", Body.None),
            (CastleKitPiece.PanelAccent, "SM_Bld_Castle_Wall_04", Body.Solid),
            (CastleKitPiece.Pillar, "SM_Bld_Castle_Pillar_Stone_05", Body.Solid),
            (CastleKitPiece.Floor, "SM_Bld_Castle_Floor_Wood_01", Body.None),
            (CastleKitPiece.Battlement, "SM_Bld_Castle_Battlements_01", Body.None),
            (CastleKitPiece.BattlementCap, "SM_Bld_Castle_Battlements_Pillar_02", Body.None),
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
        };
        const string KeysHint = "시험 키  P: 쇠창살 올림/내림 · O: 성문 열기/닫기 · B: 성문 부수기/고치기 · K: 조준한 성벽·계단 무너뜨리기";

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
                prefab: KitWallPrefab(stone)), Data + "/Wall.asset");
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
            camera = Required<BuildCameraSettings>(Data + "/Camera.asset");
            ConstructionWorld world = Find<ConstructionWorld>(scene);
            Transform geometry = world.transform.Find("Geometry");
            world.transform.position = new Vector3(-grid.Width * Cell * 0.5f, 0f, -grid.Depth * Cell * 0.5f);
            Transform ground = geometry.Find("Ground");
            ground.localScale = new Vector3(grid.Width * Cell, 1f, grid.Depth * Cell);
            ground.localPosition = new Vector3(grid.Width * Cell * 0.5f, -0.5f, grid.Depth * Cell * 0.5f);
            world.Configure(grid, geometry, geometry.GetComponent<Unity.AI.Navigation.NavMeshSurface>(), Seeds(wall, stairs, gate, bastion));
            EditorUtility.SetDirty(world);

            Find<ConstructionCameraController>(scene).Configure(world, camera);
            ConstructionHud hud = Find<ConstructionHud>(scene);
            var serializedHud = new SerializedObject(hud);
            serializedHud.FindProperty("wall").objectReferenceValue = wall;
            serializedHud.FindProperty("stairs").objectReferenceValue = stairs;
            serializedHud.FindProperty("bastion").objectReferenceValue = bastion;
            serializedHud.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);
            AddBastionButton(hud);

            var dresser = world.gameObject.AddComponent<CastleKitDresser>();
            dresser.Configure(world, kit);
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
            BuildPieceDefinition bastion)
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
                material.SetFloat("_Smoothness", 0.1f);
                material.SetFloat("_Metallic", 0f);
                material.enableInstancing = true;
                AssetDatabase.CreateAsset(material, path);
            }
            converted[source] = material;
            return material;
        }

        // 충돌은 단위 상자(칸 크기로 늘어남), 외형은 짓는 중에만 보이는 임시 상자. 다 지으면 킷 조각이 대신 보인다.
        static GameObject KitWallPrefab(Material stone)
        {
            string path = Prefabs + "/KitWall.prefab";
            var root = new GameObject("KitWall");
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
