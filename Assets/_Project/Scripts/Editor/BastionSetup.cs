using System;
using ProjectNeo.Run;
using ProjectNeo.Siege;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// 보루(SIEGE-11, 사용자 결정 2026-11-22)를 프로젝트에 적용한다. 여러 번 실행해도 결과가 같다.
    /// 보루 조각 에셋을 만들고, 성벽 조각에서 무기 설치 자리를 빼고, 대포 사격 각도를 넓히고,
    /// 건설 비용 표와 저장 콘텐츠 목록에 넣고, 건설 화면에 보루 버튼을 단다.
    /// </summary>
    public static class BastionSetup
    {
        public const string BastionPath = ConstructionPrototypeSetup.Data + "/Bastion.asset";
        const string CostsPath = "Assets/_Project/Data/GameFlow/Build/BuildCosts.asset";
        // 임시 값(2026-11-22). 플레이 시험 뒤 조정한다. 높이는 7.5m 격자의 한 층(SIEGE-23).
        public const float Height = Grid75Migration.Storey;
        public const float MaxHealth = 150f;
        public const int Gold = 25;
        public const float AutoAimHalfAngle = 90f;
        public const float ManualAimHalfAngle = 135f;

        static readonly string[] Scenes =
        {
            GameFlowSetup.GamePath, ConstructionPrototypeSetup.ScenePath, SiegePrototypeSetup.ScenePath,
        };

        [MenuItem("ProjectNeo/Setup/Apply Bastions (SIEGE-11)")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            BuildPieceDefinition bastion = CreateBastion();
            RemoveWallMountPoints();
            WidenCannonArc();
            AddCost(bastion);
            AssetDatabase.SaveAssets();
            RunSaveSetup.Apply();
            foreach (string path in Scenes)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) WireScene(path);
            AssetDatabase.SaveAssets();
            Debug.Log("SIEGE-11 bastions applied.");
        }

        /// <summary>보루 조각 에셋을 만들거나 있는 것을 돌려준다.</summary>
        public static BuildPieceDefinition CreateBastion()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>(BastionPath);
            if (existing != null) return existing;
            CombatPrototypeSetup.EnsureFolder(ConstructionPrototypeSetup.Data);
            Material stone = CombatPrototypeSetup.MaterialAsset("ConstructionBastion", new Color(0.5f, 0.57f, 0.66f));
            GameObject prefab = ConstructionPrototypeSetup.PiecePrefab("Bastion", BuildPieceKind.Bastion, stone);
            return CombatPrototypeSetup.Save(BuildPieceDefinition.Create(BuildPieceKind.Bastion, "보루", Vector2Int.one,
                Height, MaxHealth, prefab, new BuildMountPoint("top", Vector3.up)), BastionPath);
        }

        static void RemoveWallMountPoints()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(BuildPieceDefinition), new[] { "Assets/_Project" }))
            {
                var piece = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (piece == null || piece.Kind != BuildPieceKind.Wall || piece.MountPoints.Count == 0) continue;
                var data = new SerializedObject(piece);
                data.FindProperty("mountPoints").ClearArray();
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(piece);
            }
        }

        static void WidenCannonArc()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(CannonSettings), new[] { "Assets/_Project" }))
            {
                var settings = AssetDatabase.LoadAssetAtPath<CannonSettings>(AssetDatabase.GUIDToAssetPath(guid));
                if (settings == null) continue;
                var data = new SerializedObject(settings);
                data.FindProperty("aimHalfAngle").floatValue = AutoAimHalfAngle;
                data.FindProperty("manualAimHalfAngle").floatValue = ManualAimHalfAngle;
                if (data.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(settings);
            }
        }

        static void AddCost(BuildPieceDefinition bastion)
        {
            var costs = AssetDatabase.LoadAssetAtPath<BuildCostTable>(CostsPath);
            if (costs == null) { Debug.LogWarning("Build cost table is missing: " + CostsPath); return; }
            var data = new SerializedObject(costs);
            SerializedProperty pieces = data.FindProperty("pieces");
            for (int i = 0; i < pieces.arraySize; i++)
                if (pieces.GetArrayElementAtIndex(i).FindPropertyRelative("piece").objectReferenceValue == bastion) return;
            pieces.InsertArrayElementAtIndex(pieces.arraySize);
            SerializedProperty entry = pieces.GetArrayElementAtIndex(pieces.arraySize - 1);
            entry.FindPropertyRelative("piece").objectReferenceValue = bastion;
            entry.FindPropertyRelative("gold").intValue = Gold;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(costs);
        }

        static void WireScene(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            // 씬을 연 뒤에는 에셋을 경로로 다시 불러 잇는다.
            var bastion = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>(BastionPath);
            bool changed = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (ConstructionHud hud in root.GetComponentsInChildren<ConstructionHud>(true))
                {
                    changed |= Link(hud, bastion);
                    changed |= AddButton(hud.transform, hud.SelectBastion);
                }
                foreach (SiegeHud hud in root.GetComponentsInChildren<SiegeHud>(true))
                {
                    changed |= Link(hud, bastion);
                    changed |= AddButton(hud.transform, hud.SelectBastion);
                }
            }
            if (!changed) return;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static bool Link(MonoBehaviour hud, BuildPieceDefinition bastion)
        {
            var data = new SerializedObject(hud);
            SerializedProperty field = data.FindProperty("bastion");
            if (field == null || field.objectReferenceValue == bastion) return false;
            field.objectReferenceValue = bastion;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);
            return true;
        }

        // 계단 버튼 바로 아래에 보루 버튼을 넣고, 늘어난 만큼 패널을 키운다.
        static bool AddButton(Transform hud, UnityAction action)
        {
            Transform stairs = Find(hud, "Button_Stairs");
            if (stairs == null) { Debug.LogWarning("No stairs button under " + hud.name); return false; }
            Transform panel = stairs.parent;
            if (panel.Find("Button_Bastion") != null) return false;
            GameObject button = ConstructionPrototypeSetup.Button(panel, "Bastion", action, "보루");
            button.transform.SetSiblingIndex(stairs.GetSiblingIndex() + 1);
            var layout = panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            var element = button.GetComponent<UnityEngine.UI.LayoutElement>();
            if (panel is RectTransform rect && element != null)
                rect.sizeDelta += new Vector2(0f, element.preferredHeight + (layout != null ? layout.spacing : 0f));
            return true;
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = Find(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
