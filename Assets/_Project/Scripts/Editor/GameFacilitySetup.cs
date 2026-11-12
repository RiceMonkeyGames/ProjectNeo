using System;
using System.Collections.Generic;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// D-0 시설 공통 틀. 게임용 시설 정의 5개(프로토타입 임의 값)를 만들어 시작 설정(RunStart)에 넣고,
    /// Game 씬 안뜰의 주점·상점·훈련장·대장간 건물에 F 상호작용을, 화면에 시설 팝업(uGUI+TMP 임시, Q-33)을 붙인다.
    /// 이미 있는 에셋은 덮어쓰지 않고, 여러 번 실행해도 팝업·컴포넌트를 중복으로 만들지 않는다.
    /// </summary>
    public static class GameFacilitySetup
    {
        const string Folder = "Assets/_Project/Data/GameFlow/Facilities";
        const string StartPath = "Assets/_Project/Data/GameFlow/Build/RunStart.asset";
        const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        static readonly (FacilityType type, string name, string building)[] Facilities =
        {
            (FacilityType.Tavern, "주점", "Tavern"),
            (FacilityType.Shop, "상점", "Shop"),
            (FacilityType.TrainingGround, "훈련장", "Training"),
            (FacilityType.Smithy, "대장간", "Smithy"),
            (FacilityType.Defense, "수성", null),
        };

        [MenuItem("ProjectNeo/Setup/Connect Facilities To Game Scene")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            CreateData();
            AssetDatabase.SaveAssets();
            // 씬을 연 뒤 만든 에셋은 경로로 다시 불러와 연결한다(C-4 GameRewardSetup과 같은 주의).
            var start = AssetDatabase.LoadAssetAtPath<RunConfig>(StartPath)
                ?? throw new InvalidOperationException("Run Connect Build Costs To Game Scene first: " + StartPath);
            AddFacilitiesTo(start);

            GameBattleController battle = Find<GameBattleController>(scene);
            var facilities = battle.GetComponent<GameFacilities>();
            if (facilities == null) facilities = battle.gameObject.AddComponent<GameFacilities>();
            GamePhaseView view = Find<GamePhaseView>(scene);
            GameFlowHud hud = Find<GameFlowHud>(scene);
            BuildPopup(hud, facilities, out GameObject panel, out TMP_Text title, out TMP_Text status, out TMP_Text body);
            facilities.Configure(start, view, panel, title, status, body);
            EditorUtility.SetDirty(facilities);

            ConstructionWorld world = Find<ConstructionWorld>(scene);
            foreach (var entry in Facilities)
            {
                if (entry.building == null) continue;
                Transform building = world.transform.Find("Geometry/CourtyardLayout/" + entry.building)
                    ?? throw new InvalidOperationException("Apply Default Castle first. Missing building: " + entry.building);
                var station = building.GetComponent<FacilityStation>();
                if (station == null) station = building.gameObject.AddComponent<FacilityStation>();
                station.Configure(view, facilities, entry.type);
                EditorUtility.SetDirty(station);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        static string PathOf(FacilityType type) => $"{Folder}/Facility_{type}.asset";

        /// <summary>프로토타입 시설 데이터를 만들거나 기존 것을 둔다.</summary>
        public static void CreateData()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Data/GameFlow", "Facilities");
            foreach (var entry in Facilities)
            {
                string path = PathOf(entry.type);
                if (AssetDatabase.LoadAssetAtPath<FacilityDefinition>(path) != null) continue;
                // 프로토타입 임의 값(2026-11-12). 레벨 3단계, 업그레이드 골드 60·120. 매물·풀·스탯은 각 시설 작업에서 채운다.
                AssetDatabase.CreateAsset(FacilityDefinition.Create(entry.type, entry.name,
                    Level(0, 0, 3), Level(60, 1, 4), Level(120, 2, 5)), path);
            }
        }

        static FacilityLevel Level(int gold, int tier, int offers) => new FacilityLevel
        {
            upgradeCost = new Cost(gold),
            qualityTier = tier,
            offerCount = offers,
            soldierModifiers = new List<ProjectNeo.Stats.StatModifier>(),
            recruitableSoldiers = new List<ProjectNeo.Characters.SoldierDefinition>(),
            itemPool = new List<ProjectNeo.Core.WeightedEntry<ItemDefinition>>(),
            heroPool = new List<ProjectNeo.Core.WeightedEntry<ProjectNeo.Characters.HeroDefinition>>(),
        };

        static void AddFacilitiesTo(RunConfig start)
        {
            var data = new SerializedObject(start);
            SerializedProperty list = data.FindProperty("facilities");
            list.ClearArray();
            foreach (var entry in Facilities)
            {
                var definition = AssetDatabase.LoadAssetAtPath<FacilityDefinition>(PathOf(entry.type));
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = definition;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(start);
        }

        static void BuildPopup(GameFlowHud hud, GameFacilities facilities, out GameObject panel, out TMP_Text title,
            out TMP_Text status, out TMP_Text body)
        {
            // 한글 글꼴(RuntimeSystemFont)이 실행 중에 바꿔 주는 범위 안에 둔다.
            var font = (RuntimeSystemFont)new SerializedObject(hud).FindProperty("font").objectReferenceValue;
            Transform parent = font != null && !hud.transform.IsChildOf(font.transform) ? font.transform : hud.transform;
            Transform existing = parent.Find("FacilityPanel");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            panel = new GameObject("FacilityPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(640f, 420f);
            panel.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.1f, 0.94f);

            title = Text(panel.transform, "Title", "Facility", 32f, new Vector2(24f, -20f), new Vector2(-24f, -70f));
            title.fontStyle = FontStyles.Bold;
            status = Text(panel.transform, "Status", "Level", 18f, new Vector2(24f, -76f), new Vector2(-24f, -136f));
            status.color = new Color(1f, 0.9f, 0.55f);
            body = Text(panel.transform, "Body", "", 20f, new Vector2(24f, -146f), new Vector2(-24f, -330f));

            var close = new GameObject("Button_Close", typeof(RectTransform), typeof(Image), typeof(Button));
            close.transform.SetParent(panel.transform, false);
            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 20f); closeRect.sizeDelta = new Vector2(200f, 48f);
            var image = close.GetComponent<Image>(); image.color = new Color(0.15f, 0.24f, 0.29f);
            var button = close.GetComponent<Button>(); button.targetGraphic = image;
            UnityEventTools.AddPersistentListener(button.onClick, facilities.Close);
            TMP_Text label = Text(close.transform, "Label", "닫기 (Esc)", 20f, Vector2.zero, Vector2.zero);
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            panel.SetActive(false);
        }

        /// <summary>패널 위쪽 기준으로 왼쪽 위·오른쪽 아래 여백을 받아 글 칸을 만든다.</summary>
        static TMP_Text Text(Transform parent, string name, string text, float size, Vector2 topLeft, Vector2 bottomRight)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(topLeft.x, bottomRight.y); rect.offsetMax = new Vector2(bottomRight.x, topLeft.y);
            var label = root.GetComponent<TextMeshProUGUI>();
            label.text = text; label.fontSize = size; label.color = new Color(0.92f, 0.96f, 1f); label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            return label;
        }

        static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            throw new InvalidOperationException("Game scene has no " + typeof(T).Name);
        }
    }
}
