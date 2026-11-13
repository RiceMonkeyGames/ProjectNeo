using System;
using System.Collections.Generic;
using System.Linq;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Siege;
using ProjectNeo.Squads;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// 수성전 프로토타입 씬을 Unity API로 만든다. WallCommandPrototype을 복사해 준비 단계·적 침공·사다리·승패를 더한다.
    /// 원래 씬은 그대로 둔다.
    /// </summary>
    public static class SiegePrototypeSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/SiegePrototype.unity";
        const string Data = "Assets/_Project/Data/SiegePrototype";
        const string Construction = "Assets/_Project/Data/ConstructionPrototype/";
        const string Units = "Assets/_Project/Prefabs/UnitBattlePrototype/";
        const string Prefabs = "Assets/_Project/Prefabs/SiegePrototype";
        const string Font = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        // 성벽 줄의 격자 z. 이보다 작은 쪽이 성 안, 큰 쪽이 성 밖이다.
        const int WallRow = 8;
        static readonly Vector2Int Outward = new Vector2Int(0, 1);
        // 성벽 줄을 따라 5칸마다 하나. 11번 칸은 처음에 뚫려 있어 성벽을 지어야 사다리가 쓰인다.
        static readonly int[] LadderColumns = { 1, 6, 11 };
        // 처음에 뚫려 있는 칸.
        static readonly int[] Gaps = { 4, 11, 12 };

        [MenuItem("ProjectNeo/Setup/Create Siege Prototype")]
        public static void Create()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("SiegePrototype already exists; refusing to overwrite it.");
            var wall = Required<BuildPieceDefinition>(Construction + "Wall.asset");
            var stairs = Required<BuildPieceDefinition>(Construction + "Stairs.asset");
            var cannon = Required<SiegeWeaponDefinition>(Construction + "Cannon.asset");
            var fall = Required<FallSettings>(Construction + "Fall.asset");
            var gatePrefab = Required<GameObject>("Assets/_Project/Prefabs/ConstructionPrototype/MainGate.prefab");
            var projectile = Required<GameObject>(Units + "StraightProjectile.prefab");
            GameObject enemyMelee = Required<GameObject>(Units + "hostile_melee.prefab");
            GameObject enemyRanged = Required<GameObject>(Units + "hostile_ranged.prefab");
            CombatPrototypeSetup.EnsureFolder(Data);
            var grid = CombatPrototypeSetup.Save(BuildGridDefinition.Create(16, 20, 3f, 3), Data + "/Grid.asset");
            // 성문 파괴병이 바로 뚫지 못하도록 이 시험의 성문은 체력을 따로 둔다.
            var gate = CombatPrototypeSetup.Save(BuildPieceDefinition.Create(BuildPieceKind.Gate, "메인 성문",
                new Vector2Int(2, 1), maxHealth: 600f, prefab: gatePrefab), Data + "/Gate.asset");
            var settings = CombatPrototypeSetup.Save(SiegeSettings.Create(40,
                new[] { new PieceCost { piece = wall, cost = 2 }, new PieceCost { piece = stairs, cost = 3 } },
                new[] { new WeaponCost { weapon = cannon, cost = 6 } }), Data + "/Siege.asset");
            var cameraSettings = CombatPrototypeSetup.Save(WideCamera(), Data + "/Camera.asset");
            // 적 궁수는 아래에서 성벽 위를 쏘면 피해가 절반이다.
            var enemyShot = CombatPrototypeSetup.Save(CombatProfile.Create(0.3f, 55f, 0.4f, 2f, AttackKind.Projectile,
                projectile, upwardShotDamageRatio: 0.5f), Data + "/EnemyRanged.asset");
            Material wood = CombatPrototypeSetup.MaterialAsset("SiegeLadder", new Color(0.55f, 0.38f, 0.2f));
            Material iron = CombatPrototypeSetup.MaterialAsset("SiegeRam", new Color(0.12f, 0.12f, 0.14f));
            // 적은 종류별 프리팹으로 두고, 아군·적의 수는 Siege.asset의 편성에서 바꾼다.
            CombatPrototypeSetup.EnsureFolder(Prefabs);
            GameObject ladderEnemy = EnemyPrefab("enemy_ladder", enemyMelee, SiegeAttackerRole.Ladder, settings, null, wood);
            GameObject gateEnemy = EnemyPrefab("enemy_gate_breaker", enemyMelee, SiegeAttackerRole.GateBreaker, settings, null, iron);
            GameObject archerEnemy = EnemyPrefab("enemy_archer", enemyRanged, SiegeAttackerRole.Archer, settings, enemyShot, null);
            if (settings.AllySquads.Count == 0 && settings.EnemyGroups.Count == 0)
            {
                GameObject allyMelee = Required<GameObject>(Units + "allied_melee.prefab");
                GameObject allyRanged = Required<GameObject>(Units + "allied_ranged.prefab");
                settings.ConfigureRoster(
                    new[] { Group(allyMelee, 4), Group(allyMelee, 4), Group(allyRanged, 4) },
                    new[] { Group(ladderEnemy, 8), Group(gateEnemy, 4), Group(archerEnemy, 4) });
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            Scene scene = EditorSceneManager.OpenScene(WallCommandSetup.ScenePath, OpenSceneMode.Single);
            // 원래 성벽 지휘 시험은 유지하고 새 씬에서 고친다.
            EditorSceneManager.SaveScene(scene, ScenePath);
            // 씬을 열면 방금 만든 에셋이 메모리에서 내려갈 수 있다. SerializedObject로 연결할 것은 다시 불러온다.
            cameraSettings = Required<BuildCameraSettings>(Data + "/Camera.asset");
            settings = Required<SiegeSettings>(Data + "/Siege.asset");
            var allyAI = Required<UnitAISettings>("Assets/_Project/Data/SquadCommandPrototype/AllyAI.asset");
            ConstructionWorld world = Find<ConstructionWorld>(scene);
            PlacementController placement = Find<PlacementController>(scene);
            var orbit = Find<ConstructionCameraController>(scene);
            Camera buildCamera = orbit.GetComponent<Camera>();
            FirstPersonCamera firstPerson = Find<FirstPersonCamera>(scene);
            PlayerInputController playerInput = Find<PlayerInputController>(scene);
            SquadCommandCenter center = Find<SquadCommandCenter>(scene);
            SquadCommandInput commandInput = Find<SquadCommandInput>(scene);
            SquadCommandHud commandHud = Find<SquadCommandHud>(scene);
            UnityEngine.Object.DestroyImmediate(Find<WallCommandSandbox>(scene).gameObject);
            UnityEngine.Object.DestroyImmediate(Find<ConstructionSandbox>(scene).gameObject);
            UnityEngine.Object.DestroyImmediate(Find<ConstructionHud>(scene).gameObject);

            // 판을 넓히고 성벽 줄이 판의 가로 전체를 막도록 다시 놓는다.
            float width = grid.Width * grid.CellSize, depth = grid.Depth * grid.CellSize;
            NavMeshSurface surface = Find<NavMeshSurface>(scene);
            world.transform.position = new Vector3(-width * 0.5f, 0f, -depth * 0.5f);
            Transform ground = surface.transform.Find("Ground");
            ground.localScale = new Vector3(width, 1f, depth);
            ground.localPosition = new Vector3(width * 0.5f, -0.5f, depth * 0.5f);
            var seeds = new List<InitialBuildPiece>();
            for (int x = 0; x < grid.Width; x++)
                if (x != 7 && x != 8 && !Gaps.Contains(x))
                    seeds.Add(new InitialBuildPiece { definition = wall, cell = new Vector2Int(x, WallRow) });
            seeds.Add(new InitialBuildPiece { definition = gate, cell = new Vector2Int(7, WallRow), fixedGate = true });
            seeds.Add(new InitialBuildPiece { definition = stairs, cell = new Vector2Int(5, WallRow - 1) });
            seeds.Add(new InitialBuildPiece { definition = stairs, cell = new Vector2Int(10, WallRow - 1) });
            world.Configure(grid, surface.transform, surface, seeds.ToArray());
            EditorUtility.SetDirty(world);
            SetReference(orbit, "settings", cameraSettings);

            // 복사해 온 씬의 아군 유닛은 지운다. 아군과 적은 실행할 때 Siege.asset의 편성대로 만든다.
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponent<UnitCombatAI>() != null) UnityEngine.Object.DestroyImmediate(root);
            center.ConfigureRosters(new SquadRoster[0]);
            EditorUtility.SetDirty(center);
            // 부대마다 처음 서는 성 안쪽 자리.
            Vector3[] allyPoints =
            {
                new Vector3(-9f, 0f, -13f), new Vector3(9f, 0f, -13f), new Vector3(0f, 0f, -18f), new Vector3(0f, 0f, -23f),
            };
            // 성벽 위는 적 궁수의 사거리 안이라, 플레이어는 성문 뒤 땅에서 시작해 스스로 올라간다.
            GameObject player = playerInput.gameObject;
            player.transform.SetPositionAndRotation(world.transform.position
                + new Vector3(8f * grid.CellSize, 0f, (WallRow - 2.5f) * grid.CellSize), Quaternion.identity);

            // 사다리 자리.
            var ladders = new SiegeLadder[LadderColumns.Length];
            for (int i = 0; i < ladders.Length; i++)
            {
                var root = new GameObject("SiegeLadder_" + (i + 1));
                ladders[i] = root.AddComponent<SiegeLadder>();
                ladders[i].Configure(world, settings, new Vector2Int(LadderColumns[i], WallRow), Outward);
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                GameObject rails = CombatPrototypeSetup.Box("Rails", Vector3.zero, Vector3.one, wood);
                UnityEngine.Object.DestroyImmediate(rails.GetComponent<Collider>());
                rails.transform.SetParent(visual.transform, false);
                visual.AddComponent<PlaceholderLadderView>().Configure(ladders[i], rails.transform);
            }


            var prepRoot = new GameObject("SiegePrep");
            var prep = prepRoot.AddComponent<SiegePrepController>();
            prep.Configure(world, buildCamera, placement, center, settings, 0f);
            var directorRoot = new GameObject("SiegeDirector");
            var director = directorRoot.AddComponent<SiegeDirector>();
            SiegeHud hud = CreateBuildHud(director, prep, placement, settings, wall, stairs, cannon, center);
            CreateStatusHud(director, ladders);
            director.Configure(world, settings, prep, center, buildCamera, firstPerson, placement, playerInput, commandInput,
                hud.gameObject, commandHud.gameObject, player.GetComponent<CharacterActor>());
            // 적 첫 줄은 성 밖 멀리. 전투 시작 때 나타난다.
            director.ConfigureSpawns(allyAI, fall, ladders, Outward, allyPoints, new Vector3(0f, 0f, 22f));
            ApplyCommandSetup(center, commandHud);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 이미 만든 수성전 씬에 부대 10개까지의 선택 키(F1~F10)와 넓힌 부대 목록을 적용한다. 여러 번 실행해도 결과가 같다.
        /// </summary>
        [MenuItem("ProjectNeo/Setup/Upgrade Siege Prototype")]
        public static void Upgrade()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ApplyCommandSetup(Find<SquadCommandCenter>(scene), Find<SquadCommandHud>(scene));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        // 이 씬 전용 지휘 설정. 성벽 지휘 시험과 같은 대형에 부대 선택 키만 F1~F10으로 늘린다.
        static void ApplyCommandSetup(SquadCommandCenter center, SquadCommandHud commandHud)
        {
            string path = Data + "/Command.asset";
            var command = AssetDatabase.LoadAssetAtPath<SquadCommandSettings>(path);
            if (command == null)
            {
                command = SquadCommandSettings.Create(2, 1.1f);
                var keys = new string[10];
                for (int i = 0; i < keys.Length; i++) keys[i] = "<Keyboard>/f" + (i + 1);
                command.ConfigureSelectKeys(keys);
                AssetDatabase.CreateAsset(command, path);
            }
            SetReference(center, "settings", command);
            var help = new SerializedObject(commandHud);
            help.FindProperty("helpText").stringValue =
                "F1~F10 부대 선택 · ` 전체 선택\n1 이동(화면 가운데가 가리키는 곳) · 2 공격 · 3 중지\n"
                + "WASD 이동 · 마우스 시점 · 좌클릭 공격 · Esc 커서 해제\nF 누르고 있기: 가까운 사다리 밀기";
            help.ApplyModifiedPropertiesWithoutUndo();
            // 부대가 10개여도 목록이 배경 안에 들어오게 늘린다.
            var size = new Vector2(326f, 300f);
            var squads = commandHud.transform.Find("Squads").GetComponent<RectTransform>();
            squads.sizeDelta = size;
            Transform backing = commandHud.transform.Find("SquadsBacking");
            if (backing != null) backing.GetComponent<RectTransform>().sizeDelta = size + Vector2.one * 16f;
        }

        // 넓어진 판이 한 화면에 들어오도록 건설 카메라만 따로 둔다.
        static BuildCameraSettings WideCamera()
        {
            var value = BuildCameraSettings.Create();
            var serialized = new SerializedObject(value);
            serialized.FindProperty("orthographicSize").floatValue = 26f;
            serialized.FindProperty("distance").floatValue = 70f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return value;
        }

        static SiegeUnitGroup Group(GameObject prefab, int count) => new SiegeUnitGroup { unitPrefab = prefab, count = count };

        // 기존 적 프리팹에 낙하·공성 판단·종류 표시를 더한 프리팹. 성과 사다리는 실행할 때 연결한다.
        static GameObject EnemyPrefab(string name, GameObject source, SiegeAttackerRole role, SiegeSettings settings,
            CombatProfile profile, Material marker)
        {
            string path = Prefabs + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var unit = (GameObject)PrefabUtility.InstantiatePrefab(source);
            unit.SetActive(false);
            if (profile != null) SetReference(unit.GetComponent<CharacterActor>(), "profile", profile);
            unit.AddComponent<UnitFallController>();
            unit.AddComponent<SiegeAttackerBrain>().Configure(role, null, settings, new SiegeLadder[0], Outward);
            if (marker != null)
            {
                // 종류를 눈으로 구분하는 임시 표시. 사다리병은 등에 널빤지, 성문 파괴병은 어깨에 쇳덩이. 죽으면 숨긴다.
                bool ladder = role == SiegeAttackerRole.Ladder;
                GameObject mark = CombatPrototypeSetup.Box("RoleMarker", Vector3.zero,
                    ladder ? new Vector3(0.5f, 1.4f, 0.1f) : new Vector3(0.4f, 0.4f, 0.7f), marker);
                UnityEngine.Object.DestroyImmediate(mark.GetComponent<Collider>());
                mark.transform.SetParent(unit.transform.Find("Visual"), false);
                mark.transform.localPosition = ladder ? new Vector3(0f, 1.2f, -0.48f) : new Vector3(0.5f, 1.55f, 0.1f);
                mark.AddComponent<PlaceholderDeathHider>();
            }
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(unit, path);
            UnityEngine.Object.DestroyImmediate(unit);
            return prefab;
        }

        static SiegeHud CreateBuildHud(SiegeDirector director, SiegePrepController prep, PlacementController placement,
            SiegeSettings settings, BuildPieceDefinition wall, BuildPieceDefinition stairs, SiegeWeaponDefinition cannon,
            SquadCommandCenter center)
        {
            var root = new GameObject("SiegeBuildHUD", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f); scaler.matchWidthOrHeight = 0.5f;
            var hud = root.AddComponent<SiegeHud>();
            var panel = new GameObject("BuildPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.VerticalLayoutGroup));
            panel.transform.SetParent(root.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f); rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(18f, 0f); rect.sizeDelta = new Vector2(256f, 660f);
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.055f, 0.08f, 0.11f, 0.95f);
            var layout = panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12); layout.spacing = 4f;
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            var labels = new List<TMP_Text>(); var captions = new List<string>();
            Label(panel.transform, "SIEGE PREP", 30f, 22f, "수성전 준비", labels, captions);
            TMP_Text budget = Label(panel.transform, "Cost", 24f, 18f);
            Button(panel.transform, "Wall", "성벽  (코스트 " + settings.CostOf(wall) + ")", hud.SelectWall, labels, captions);
            Button(panel.transform, "Stairs", "계단  (코스트 " + settings.CostOf(stairs) + ")", hud.SelectStairs, labels, captions);
            GameObject weapon = Button(panel.transform, "Cannon", cannon.DisplayName + "  (코스트 " + settings.CostOf(cannon) + ")",
                null, labels, captions);
            UnityEventTools.AddIntPersistentListener(weapon.GetComponent<UnityEngine.UI.Button>().onClick, hud.SelectWeapon, 0);
            Button(panel.transform, "Rotate", "조각 회전  R", hud.Rotate, labels, captions);
            Button(panel.transform, "Dismantle", "철거 (전액 환불)", hud.Dismantle, labels, captions);
            Button(panel.transform, "Cancel", "취소", hud.Cancel, labels, captions);
            Label(panel.transform, "PLACE SQUADS", 22f, 15f, "부대 바로 배치", labels, captions);
            // 부대 수 상한만큼 버튼을 만들어 두고, 실행 중에 실제 편성된 부대만 보여준다.
            var squadButtons = new GameObject[center.Config.MaxSquads];
            var squadLabels = new TMP_Text[squadButtons.Length];
            for (int i = 0; i < squadButtons.Length; i++)
            {
                squadButtons[i] = Button(panel.transform, "Squad" + (i + 1), (i + 1) + "번 부대", null, labels, captions);
                squadLabels[i] = squadButtons[i].GetComponentInChildren<TMP_Text>();
                UnityEventTools.AddIntPersistentListener(squadButtons[i].GetComponent<UnityEngine.UI.Button>().onClick, hud.SelectSquad, i);
            }
            hud.ConfigureSquads(center, squadButtons, squadLabels);
            TMP_Text selection = Label(panel.transform, "Selection", 52f, 14f);
            TMP_Text enemies = Label(panel.transform, "Enemies", 44f, 14f);
            TMP_Text status = Label(panel.transform, "Status", 44f, 14f);
            GameObject start = Button(panel.transform, "Start", "전투 시작", hud.StartBattle, labels, captions);
            start.GetComponent<UnityEngine.UI.Image>().color = new Color(0.62f, 0.2f, 0.16f);
            start.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 40f;
            TMP_Text hint = Floating(root.transform, "Hint", new Vector2(1f, 1f), new Vector2(-24f, -24f),
                new Vector2(460f, 90f), 17f, TextAlignmentOptions.TopRight);
            labels.Add(hint);
            captions.Add("Q/E 시점 회전 · 좌클릭 설치·배치 · 우클릭 취소\n갈색 막대가 놓인 곳이 사다리 자리입니다\n그 칸에 성벽이 있으면 적이 사다리를 세웁니다");
            hud.Configure(director, prep, placement, settings, wall, stairs, new[] { cannon }, budget, selection, status,
                enemies, labels.ToArray(), captions.ToArray());
            return hud;
        }

        static void CreateStatusHud(SiegeDirector director, SiegeLadder[] ladders)
        {
            var root = new GameObject("SiegeStatusHUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f); scaler.matchWidthOrHeight = 0.5f;
            var backing = new GameObject("Backing", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            backing.transform.SetParent(root.transform, false);
            var rect = backing.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -12f); rect.sizeDelta = new Vector2(620f, 70f);
            var image = backing.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.03f, 0.05f, 0.07f, 0.8f); image.raycastTarget = false;
            TMP_Text label = Floating(root.transform, "Status", new Vector2(0.5f, 1f), new Vector2(0f, -18f),
                new Vector2(600f, 60f), 20f, TextAlignmentOptions.Top);
            label.text = "Siege";
            root.AddComponent<SiegeStatusHud>().Configure(director, ladders, label);
        }

        static TMP_Text Floating(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size,
            float fontSize, TextAlignmentOptions alignment)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var label = root.GetComponent<TextMeshProUGUI>();
            label.fontSize = fontSize; label.color = new Color(0.95f, 0.97f, 1f); label.raycastTarget = false;
            label.alignment = alignment;
            label.font = Required<TMP_FontAsset>(Font);
            return label;
        }

        static TMP_Text Label(Transform parent, string text, float height, float size,
            string caption = null, List<TMP_Text> labels = null, List<string> captions = null)
        {
            var root = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(UnityEngine.UI.LayoutElement));
            root.transform.SetParent(parent, false);
            root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            var label = root.GetComponent<TextMeshProUGUI>(); label.text = text; label.fontSize = size;
            label.color = new Color(0.92f, 0.96f, 1f); label.raycastTarget = false;
            label.font = Required<TMP_FontAsset>(Font);
            if (caption != null) { labels.Add(label); captions.Add(caption); }
            return label;
        }

        static GameObject Button(Transform parent, string title, string caption, UnityEngine.Events.UnityAction action,
            List<TMP_Text> labels, List<string> captions)
        {
            GameObject button = ConstructionPrototypeSetup.Button(parent, title, caption, action, labels, captions);
            button.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 30f;
            return button;
        }

        static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        static T Required<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing asset: " + path);
        static void SetReference(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
