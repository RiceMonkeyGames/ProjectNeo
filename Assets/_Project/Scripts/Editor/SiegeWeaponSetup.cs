using System;
using System.Collections.Generic;
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
    /// <summary>건설 시험 씬에 수성 무기 에셋과 시험 버튼을 추가한다. 새 무기는 Weapon(...) 한 줄로 늘린다.</summary>
    public static class SiegeWeaponSetup
    {
        const string Data = "Assets/_Project/Data/ConstructionPrototype";
        const string Prefabs = "Assets/_Project/Prefabs/ConstructionPrototype";
        // Q-21 임시값. 성벽 기본 체력 100 기준.
        internal const float CannonMaxHealth = 150f;
        internal const string CannonAssetPath = Data + "/Cannon.asset";

        /// <summary>이미 만든 대포 에셋의 체력을 Q-21 값으로 맞춘다. 여러 번 실행해도 같다.</summary>
        [MenuItem("ProjectNeo/Setup/Apply Cannon Health (Q-21)")]
        public static void ApplyCannonHealth()
        {
            var cannon = AssetDatabase.LoadAssetAtPath<SiegeWeaponDefinition>(CannonAssetPath);
            if (cannon == null) throw new InvalidOperationException("Cannon asset not found: " + CannonAssetPath);
            var serialized = new SerializedObject(cannon);
            serialized.FindProperty("maxHealth").floatValue = CannonMaxHealth;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cannon);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("ProjectNeo/Setup/Add Siege Weapons To Construction Prototype")]
        public static void AddToConstructionPrototype()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            Scene scene = EditorSceneManager.OpenScene(ConstructionPrototypeSetup.ScenePath, OpenSceneMode.Single);
            AddToOpenScene(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        internal static void AddToOpenScene(Scene scene)
        {
            ConstructionHud hud = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (hud == null) hud = root.GetComponentInChildren<ConstructionHud>(true);
            if (hud == null) throw new InvalidOperationException("ConstructionHud not found.");
            if (hud.Weapons.Count > 0) throw new InvalidOperationException("Siege weapons are already added.");
            CombatPrototypeSetup.EnsureFolder(Data); CombatPrototypeSetup.EnsureFolder(Prefabs);
            Material metal = CombatPrototypeSetup.MaterialAsset("ConstructionSiegeWeapon", new Color(0.2f, 0.22f, 0.26f));
            var weapons = new[] { Weapon("cannon", "Cannon", "Cannon", new Vector3(1.2f, 1.1f, 1.8f), metal, CannonMaxHealth) };

            Transform panel = hud.transform.Find("BuildPanel");
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(248f, 690f);
            panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing = 6f;

            int index = panel.Find("Button_Stairs").GetSiblingIndex() + 1;
            for (int i = 0; i < weapons.Length; i++)
            {
                GameObject choice = ConstructionPrototypeSetup.Button(panel, weapons[i].name, null);
                UnityEventTools.AddIntPersistentListener(choice.GetComponent<UnityEngine.UI.Button>().onClick, hud.SelectWeapon, i);
                choice.transform.SetSiblingIndex(index + i);
            }
            GameObject collapse = ConstructionPrototypeSetup.Button(panel, "Break weapon support", hud.CollapseUnderWeapon);
            collapse.transform.SetSiblingIndex(panel.Find("Button_Break support").GetSiblingIndex() + 1);
            foreach (Transform child in panel)
                if (child.name.StartsWith("Button_"))
                    child.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 34f;
            hud.ConfigureWeapons(weapons);
            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        // 임시 외형의 수성 무기 하나. 충돌체를 두지 않아 성벽 위 통로를 막지 않는다.
        static SiegeWeaponDefinition Weapon(string id, string assetName, string displayName, Vector3 size, Material material,
            float maxHealth)
        {
            string path = Data + "/" + assetName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<SiegeWeaponDefinition>(path);
            if (existing != null) return existing;
            string prefabPath = Prefabs + "/" + assetName + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                var root = new GameObject(assetName); root.AddComponent<PlacedSiegeWeapon>();
                var visual = new GameObject("Visual"); visual.transform.SetParent(root.transform, false);
                VisualPart(visual.transform, "Base", new Vector3(0f, 0.2f, 0f), new Vector3(1f, 0.4f, 0.6f), material);
                VisualPart(visual.transform, "Barrel", new Vector3(0f, 0.65f, 0.15f), new Vector3(0.4f, 0.45f, 1f), material);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath); UnityEngine.Object.DestroyImmediate(root);
            }
            var definition = SiegeWeaponDefinition.Create(id, displayName, size, prefab, maxHealth);
            definition.name = assetName;
            return CombatPrototypeSetup.Save(definition, path);
        }

        static void VisualPart(Transform visual, string name, Vector3 center, Vector3 size, Material material)
        {
            var body = CombatPrototypeSetup.Box(name, Vector3.zero, size, material);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(visual, false); body.transform.localPosition = center;
        }
    }
}
