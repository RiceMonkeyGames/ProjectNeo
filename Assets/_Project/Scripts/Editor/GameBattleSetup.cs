using System;
using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Flow;
using ProjectNeo.Siege;
using ProjectNeo.Squads;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    public static class GameBattleSetup
    {
        const string Data = "Assets/_Project/Data/GameFlow";
        // 왼쪽 위 부대 목록 높이(기준 1280×720). PREP-09: 부대 10줄 + 지휘 알림·사기 알림 2줄이 최소 글자(14)로 들어가는 높이.
        internal const float SquadListHeight = 236f;
        /// <summary>
        /// Game 씬의 사다리만 바꾼다(SIEGE-37): 깔아 둔 사다리를 지우고 운반 사다리 원본을 전투에 연결한다.
        /// 전투·공성 데이터 자산은 다시 만들지 않는다.
        /// </summary>
        [MenuItem("ProjectNeo/Setup/Replace Game Ladders With Carried Template")]
        public static void ApplyCarriedLadders()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            var world = Find<ConstructionWorld>(scene);
            var controller = Find<GameBattleController>(scene);
            SiegeLadder template = ReplaceLadders(world, controller.Settings.Siege, controller.Settings.Outward);
            var data = new SerializedObject(controller);
            data.FindProperty("ladderTemplate").objectReferenceValue = template;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // 전투 전에 깔아 두는 사다리는 없다. 사다리 적이 이 꺼진 원본의 복제를 들고 나와 빈틈에 건다(SIEGE-37).
        static SiegeLadder ReplaceLadders(ConstructionWorld world, SiegeSettings siege, Vector2Int outward)
        {
            Transform old = world.transform.Find("GameLadders");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var ladderRoot = new GameObject("GameLadders").transform;
            ladderRoot.SetParent(world.transform, false);
            return CreateLadderTemplate(ladderRoot, world, siege, outward);
        }

        /// <summary>
        /// 사다리 적이 들고 나올 사다리의 꺼진 원본(SIEGE-37). 외형은 임시 나무 막대(충돌체 없음)다.
        /// 칸·방향은 운반병이 걸 자리를 고를 때 정해진다.
        /// </summary>
        internal static SiegeLadder CreateLadderTemplate(Transform parent, ConstructionWorld world, SiegeSettings siege,
            Vector2Int outward)
        {
            var root = new GameObject("LadderTemplate");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            var ladder = root.AddComponent<SiegeLadder>();
            ladder.Configure(world, siege, Vector2Int.zero, outward);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Placeholder/SiegeLadder.mat");
            GameObject rails = CombatPrototypeSetup.Box("Rails", Vector3.zero, Vector3.one, wood);
            UnityEngine.Object.DestroyImmediate(rails.GetComponent<Collider>());
            rails.transform.SetParent(visual, false);
            visual.gameObject.AddComponent<PlaceholderLadderView>().Configure(ladder, rails.transform);
            return ladder;
        }

        [MenuItem("ProjectNeo/Setup/Connect Battle To Game Scene")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            ConfigureScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureScene(Scene scene)
        {
            var world = Find<ConstructionWorld>(scene);
            var view = Find<GamePhaseView>(scene);
            var player = Find<PlayerInputController>(scene).GetComponent<CharacterActor>();
            var center = Find<SquadCommandCenter>(scene);
            var source = AssetDatabase.LoadAssetAtPath<SiegeSettings>("Assets/_Project/Data/SiegePrototype/Siege.asset");
            var siege = CombatPrototypeSetup.Save(UnityEngine.Object.Instantiate(source), Data + "/Siege.asset");
            var settings = CombatPrototypeSetup.Save(GameBattleSettings.Create(siege,
                CreateDebugEnemies(source)), Data + "/Battle.asset");
            MoraleSetup.Connect(settings);
            var falling = AssetDatabase.LoadAssetAtPath<FallSettings>("Assets/_Project/Data/ConstructionPrototype/Fall.asset");
            var controller = Find<GameBattleController>(scene);
            if (controller == null) controller = new GameObject("GameBattleController").AddComponent<GameBattleController>();
            var interaction = Find<PreparationInteraction>(scene);
            interaction.ConfigureBattle(controller);
            EditorUtility.SetDirty(interaction);

            // 초기 성 조각에서 성문 칸을 찾으므로 이전 프로토타입의 좌표에 의존하지 않는다.
            var worldData = new SerializedObject(world);
            var seeds = worldData.FindProperty("initialPieces");
            bool hasWall = false;
            Vector2Int gateCell = default;
            for (int i = 0; i < seeds.arraySize; i++)
            {
                var seed = seeds.GetArrayElementAtIndex(i);
                var piece = (BuildPieceDefinition)seed.FindPropertyRelative("definition").objectReferenceValue;
                if (piece.Kind == BuildPieceKind.Gate) gateCell = seed.FindPropertyRelative("cell").vector2IntValue;
                hasWall |= piece.Kind == BuildPieceKind.Wall;
            }
            if (!hasWall) throw new InvalidOperationException("Castle walls are missing.");
            SiegeLadder template = ReplaceLadders(world, siege, settings.Outward);
            controller.Configure(world, settings, center, player, falling, template);
            EditorUtility.SetDirty(controller);
            // 분지(SIEGE-40)에는 전투 시작 깃발을 두지 않는다(사용자 2026-11-28). 전투 시작은 B 길게 누르기(Q-40)다.
            if (world.Grid != null && world.Grid.CastleBehindSouthEdge)
            {
                Transform stale = world.transform.Find("Geometry/BattleStartStation");
                if (stale != null) UnityEngine.Object.DestroyImmediate(stale.gameObject);
            }
            else ConfigureStartStation(world, view, controller, gateCell);
            ConfigureHudLayout(scene);
        }

        // 정식 웨이브가 들어오기 전, Game 흐름 디버깅용 소규모 편성.
        public static SiegeUnitGroup[] CreateDebugEnemies(SiegeSettings source)
        {
            var result = new List<SiegeUnitGroup>();
            var roles = new[] { SiegeAttackerRole.Ladder, SiegeAttackerRole.GateBreaker, SiegeAttackerRole.Archer };
            int[] counts = { 6, 2, 4 };
            for (int i = 0; i < roles.Length; i++)
            {
                GameObject prefab = null;
                foreach (var group in source.EnemyGroups)
                    if (group.unitPrefab != null && group.unitPrefab.GetComponent<SiegeAttacker>()?.Role == roles[i])
                    { prefab = group.unitPrefab; break; }
                if (prefab == null) throw new InvalidOperationException("Missing debug enemy role: " + roles[i]);
                result.Add(new SiegeUnitGroup { unitPrefab = prefab, count = counts[i] });
            }
            return result.ToArray();
        }

        [MenuItem("ProjectNeo/Setup/Arrange Game HUD")]
        public static void ArrangeHud()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            ConfigureHudLayout(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void ConfigureHudLayout(Scene scene)
        {
            var hud = Find<GameFlowHud>(scene);
            var panel = (RectTransform)hud.transform.Find("Panel");
            panel.anchorMin = new Vector2(0.33f, 1f); panel.anchorMax = new Vector2(0.67f, 1f);
            panel.pivot = new Vector2(0.5f, 1f); panel.anchoredPosition = new Vector2(0f, -16f);
            panel.sizeDelta = new Vector2(-16f, 0f);
            var layout = panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);

            var command = Find<SquadCommandHud>(scene);
            var data = new SerializedObject(command);
            var squads = (TMP_Text)data.FindProperty("squads").objectReferenceValue;
            var help = (TMP_Text)data.FindProperty("help").objectReferenceValue;
            RectTransform troopRect = squads.rectTransform;
            troopRect.anchorMin = new Vector2(0f, 1f); troopRect.anchorMax = new Vector2(0.3f, 1f);
            troopRect.pivot = new Vector2(0f, 1f); troopRect.anchoredPosition = new Vector2(16f, -16f);
            troopRect.sizeDelta = new Vector2(-24f, SquadListHeight);
            squads.fontSize = 18f; squads.enableAutoSizing = true; squads.fontSizeMin = 14f; squads.fontSizeMax = 18f;
            RectTransform helpRect = help.rectTransform;
            helpRect.anchorMin = Vector2.zero; helpRect.anchorMax = new Vector2(1f, 0f);
            helpRect.pivot = new Vector2(0.5f, 0f); helpRect.anchoredPosition = new Vector2(0f, 16f);
            helpRect.sizeDelta = new Vector2(-32f, 52f);
            help.alignment = TextAlignmentOptions.Center;
            help.fontSize = 16f; help.enableAutoSizing = true; help.fontSizeMin = 12f; help.fontSizeMax = 16f;
            foreach (var label in new[] { squads, help })
            {
                var backing = (RectTransform)command.transform.Find(label.name + "Backing");
                if (backing == null) continue;
                var rect = label.rectTransform;
                backing.anchorMin = rect.anchorMin; backing.anchorMax = rect.anchorMax; backing.pivot = rect.pivot;
                backing.sizeDelta = rect.sizeDelta + new Vector2(16f, 16f);
                backing.anchoredPosition = rect.anchoredPosition + new Vector2((rect.pivot.x - 0.5f) * 16f, (rect.pivot.y - 0.5f) * 16f);
                EditorUtility.SetDirty(backing);
            }
            help.text = "F1-F10: 부대 선택 - `: 전체 선택 - 1: 이동 - 2: 공격 - 3: 정지\nWASD: 이동 - 마우스: 시점 - 클릭: 공격 - Esc: 커서 해제";
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(squads); EditorUtility.SetDirty(help);
            var buildHud = Find<ConstructionHud>(scene);
            foreach (TMP_Text label in buildHud.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!label.text.StartsWith("Preparation") && !label.text.StartsWith("건설 화면")) continue;
                label.text = "건설 화면\n가운데 버튼: 이동 - 휠: 확대/축소\nQ/E: 회전 - Home: 초기화";
                label.rectTransform.anchorMin = new Vector2(0.7f, 1f); label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.pivot = Vector2.one; label.rectTransform.anchoredPosition = new Vector2(-16f, -16f);
                label.rectTransform.sizeDelta = new Vector2(-24f, 100f);
                label.alignment = TextAlignmentOptions.TopLeft;
                label.fontSize = 16f; label.enableAutoSizing = true; label.fontSizeMin = 12f; label.fontSizeMax = 16f;
                EditorUtility.SetDirty(label);
            }

            Transform statusRoot = hud.transform.Find("BattleStatus");
            TMP_Text status;
            if (statusRoot == null)
            {
                statusRoot = new GameObject("BattleStatus", typeof(RectTransform), typeof(UnityEngine.UI.Image)).transform;
                statusRoot.SetParent(hud.transform, false);
                var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(statusRoot, false);
                status = label.GetComponent<TextMeshProUGUI>();
                status.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
                status.color = Color.white; status.raycastTarget = false;
                status.rectTransform.anchorMin = Vector2.zero; status.rectTransform.anchorMax = Vector2.one;
                status.rectTransform.offsetMin = new Vector2(12f, 8f); status.rectTransform.offsetMax = new Vector2(-12f, -8f);
            }
            else status = statusRoot.GetComponentInChildren<TMP_Text>(true);
            var statusRect = (RectTransform)statusRoot;
            statusRect.anchorMin = new Vector2(0.7f, 1f); statusRect.anchorMax = Vector2.one;
            statusRect.pivot = Vector2.one; statusRect.anchoredPosition = new Vector2(-16f, -16f);
            statusRect.sizeDelta = new Vector2(-24f, 100f);
            var background = statusRoot.GetComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.05f, 0.08f, 0.1f, 0.8f); background.raycastTarget = false;
            status.fontSize = 18f; status.enableAutoSizing = true; status.fontSizeMin = 12f; status.fontSizeMax = 18f;
            status.alignment = TextAlignmentOptions.TopLeft; status.text = "전투 상황";
            hud.ConfigureBattle(Find<GameBattleController>(scene), status);
            EditorUtility.SetDirty(hud); EditorUtility.SetDirty(status);
        }

        static void ConfigureStartStation(ConstructionWorld world, GamePhaseView view, GameBattleController controller, Vector2Int gate)
        {
            Transform station = world.transform.Find("Geometry/BattleStartStation");
            if (station == null)
            {
                station = new GameObject("BattleStartStation").transform;
                station.SetParent(world.transform.Find("Geometry"), false);
                var body = station.gameObject.AddComponent<BoxCollider>();
                body.size = new Vector3(1.2f, 3f, 1.2f); body.center = Vector3.up * 1.5f;
                var visual = new GameObject("Visual").transform; visual.SetParent(station, false);
                var red = CombatPrototypeSetup.MaterialAsset("BattleStartFlag", new Color(0.7f, 0.12f, 0.08f));
                AddBox(visual, "Post", new Vector3(0f, 1.5f, 0f), new Vector3(0.2f, 3f, 0.2f), red);
                AddBox(visual, "Flag", new Vector3(0.6f, 2.4f, 0f), new Vector3(1.4f, 1f, 0.15f), red);
                var label = new GameObject("Sign").AddComponent<TextMeshPro>();
                label.transform.SetParent(visual, false);
                label.transform.localPosition = new Vector3(0f, 3.8f, 0f);
                label.rectTransform.sizeDelta = new Vector2(9f, 2f);
                label.fontSize = 14f; label.alignment = TextAlignmentOptions.Center; label.text = "전투 시작";
            }
            // 성문 안쪽 동쪽, 집결지 옆(7.5m 칸에서 성문 가운데로부터 동쪽 7.5m, 안쪽 15m).
            station.localPosition = new Vector3((gate.x + 1.5f) * world.Grid.CellSize, 0f, (gate.y - 1.5f) * world.Grid.CellSize);
            var target = station.GetComponent<BattleStartStation>();
            if (target == null) target = station.gameObject.AddComponent<BattleStartStation>();
            target.Configure(view, controller);
            EditorUtility.SetDirty(target);
        }
        static void AddBox(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject box = CombatPrototypeSetup.Box(name, Vector3.zero, size, material);
            box.transform.SetParent(parent, false); box.transform.localPosition = position;
            UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
        }
        static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            { var component = root.GetComponentInChildren<T>(true); if (component != null) return component; }
            return null;
        }
    }
}
