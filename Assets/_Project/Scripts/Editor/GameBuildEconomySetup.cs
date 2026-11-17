using System;
using System.Collections.Generic;
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

namespace ProjectNeo.Editor
{
    /// <summary>
    /// D-2 건설·수리 비용의 프로토타입 데이터(건설 비용 표·시작 골드)를 만들고, Game 씬 전투 오브젝트에
    /// <see cref="GameBuildEconomy"/>를 붙여 건설 화면에 철거·모두 수리 버튼과 골드 안내를 더한다.
    /// 이미 있는 에셋은 덮어쓰지 않고, 여러 번 실행해도 버튼을 중복으로 만들지 않는다.
    /// </summary>
    public static class GameBuildEconomySetup
    {
        const string Folder = "Assets/_Project/Data/GameFlow/Build";
        const string CostsPath = Folder + "/BuildCosts.asset";
        const string StartPath = Folder + "/RunStart.asset";

        [MenuItem("ProjectNeo/Setup/Connect Build Costs To Game Scene")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            ConstructionHud hud = Find<ConstructionHud>(scene);
            var hudData = new SerializedObject(hud);
            var wall = (BuildPieceDefinition)hudData.FindProperty("wall").objectReferenceValue;
            var stairs = (BuildPieceDefinition)hudData.FindProperty("stairs").objectReferenceValue;
            var weapons = new List<SiegeWeaponDefinition>(hud.Weapons);
            CreateData(wall, stairs, weapons);
            AssetDatabase.SaveAssets();
            // 씬을 연 뒤 만든 에셋은 경로로 다시 불러와 연결한다(C-4 GameRewardSetup과 같은 주의).
            var costs = AssetDatabase.LoadAssetAtPath<BuildCostTable>(CostsPath);
            var start = AssetDatabase.LoadAssetAtPath<RunConfig>(StartPath);
            if (costs == null || start == null) throw new InvalidOperationException("Build cost assets were not created.");

            GameBattleController battle = Find<GameBattleController>(scene);
            var economy = battle.GetComponent<GameBuildEconomy>();
            if (economy == null) economy = battle.gameObject.AddComponent<GameBuildEconomy>();
            GamePhaseView view = Find<GamePhaseView>(scene);
            var camera = (Camera)new SerializedObject(view).FindProperty("constructionCamera").objectReferenceValue;
            TMP_Text label = EnsureUi(hud, economy);
            economy.Configure(costs, start, Find<ConstructionWorld>(scene), Find<PlacementController>(scene), view, camera, label);
            EditorUtility.SetDirty(economy);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>프로토타입 비용 데이터를 만들거나 기존 것을 둔다.</summary>
        public static void CreateData(BuildPieceDefinition wall, BuildPieceDefinition stairs, IReadOnlyList<SiegeWeaponDefinition> weapons)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Data/GameFlow", "Build");
            if (AssetDatabase.LoadAssetAtPath<BuildCostTable>(CostsPath) == null)
            {
                // 프로토타입 임의 값(2026-11-12). 성문은 짓는 조각이 아니라 표에 없다.
                var weaponCosts = new List<WeaponGoldCost>();
                foreach (SiegeWeaponDefinition weapon in weapons) if (weapon != null) weaponCosts.Add(new WeaponGoldCost(weapon, 30));
                AssetDatabase.CreateAsset(BuildCostTable.Create(
                    new[] { new PieceGoldCost(wall, 10), new PieceGoldCost(stairs, 15) }, weaponCosts.ToArray(), 0.5f, 0.2f), CostsPath);
            }
            if (AssetDatabase.LoadAssetAtPath<RunConfig>(StartPath) == null)
                AssetDatabase.CreateAsset(RunConfig.Create(null, null, 100), StartPath);
        }

        static TMP_Text EnsureUi(ConstructionHud hud, GameBuildEconomy economy)
        {
            Transform panel = hud.transform.Find("BuildPanel");
            if (panel == null) throw new InvalidOperationException("ConstructionHud has no BuildPanel.");
            Transform dismantle = panel.Find("Button_Dismantle");
            if (dismantle == null)
                ConstructionPrototypeSetup.Button(panel, "Dismantle", economy.ToggleDismantle);
            else Rewire(dismantle, economy.ToggleDismantle);
            Transform repair = panel.Find("Button_Repair all");
            if (repair == null)
                ConstructionPrototypeSetup.Button(panel, "Repair all", economy.RepairAllFromButton);
            else Rewire(repair, economy.RepairAllFromButton);

            Transform existing = panel.Find("EconomyStatus");
            if (existing != null) return existing.GetComponent<TMP_Text>();
            var root = new GameObject("EconomyStatus", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(UnityEngine.UI.LayoutElement));
            root.transform.SetParent(panel, false);
            root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 70f;
            var text = root.GetComponent<TextMeshProUGUI>();
            text.text = "Gold"; text.fontSize = 14f; text.enableAutoSizing = true; text.fontSizeMin = 11f; text.fontSizeMax = 14f;
            text.color = new Color(1f, 0.9f, 0.55f); text.raycastTarget = false;
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            return text;
        }

        static void Rewire(Transform buttonRoot, UnityEngine.Events.UnityAction action)
        {
            var button = buttonRoot.GetComponent<UnityEngine.UI.Button>();
            while (button.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(button.onClick, 0);
            UnityEventTools.AddPersistentListener(button.onClick, action);
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
