using System;
using System.Linq;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    /// <summary>Q-42: shrink the shared stair definition while retaining each initial high landing.</summary>
    public static class OneCellStairsMigration
    {
        const string DefinitionPath = "Assets/_Project/Data/ConstructionPrototype/Stairs.asset";

        [MenuItem("ProjectNeo/Setup/Migrate One Cell Stairs")]
        public static void MigrateFromMenu() => Debug.Log(Migrate());

        public static string Migrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            var definition = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>(DefinitionPath);
            if (definition == null) throw new InvalidOperationException("Stair definition not found.");
            Vector2Int oldSize = definition.Footprint;
            if (oldSize != Vector2Int.one && oldSize != new Vector2Int(1, 2))
                throw new InvalidOperationException("Unexpected stair footprint.");
            // A mathematically exact 45-degree ramp is rejected at the default 45-degree bake limit.
            // Match the navigation package's serialized project-settings API, retaining steeper custom settings.
            var navigation = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset").Single());
            var agents = navigation.FindProperty("m_Settings");
            for (int i = 0; i < agents.arraySize; i++)
            {
                var agent = agents.GetArrayElementAtIndex(i);
                if (agent.FindPropertyRelative("agentTypeID").intValue != 0) continue;
                var slope = agent.FindPropertyRelative("agentSlope");
                slope.floatValue = Mathf.Max(slope.floatValue, 46f);
            }
            navigation.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            if (oldSize == Vector2Int.one) return "Already migrated; navigation slope verified.";
            var setup = EditorSceneManager.GetSceneManagerSetup();
            int changedScenes = 0, changedStairs = 0;
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Scene scene = SceneManager.GetSceneByPath(path);
                    bool opened = !scene.IsValid() || !scene.isLoaded;
                    if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    bool changed = false;
                    foreach (var world in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ConstructionWorld>(true)))
                    {
                        var data = new SerializedObject(world);
                        var seeds = data.FindProperty("initialPieces");
                        for (int i = 0; i < seeds.arraySize; i++)
                        {
                            var seed = seeds.GetArrayElementAtIndex(i);
                            if (seed.FindPropertyRelative("definition").objectReferenceValue != definition) continue;
                            var cell = seed.FindPropertyRelative("cell");
                            int rotation = seed.FindPropertyRelative("rotation").intValue;
                            Vector2Int oldLanding = BuildLayout.Landings(oldSize, cell.vector2IntValue, rotation).Single();
                            Vector2Int newLanding = BuildLayout.Landings(Vector2Int.one, cell.vector2IntValue, rotation).Single();
                            cell.vector2IntValue += oldLanding - newLanding;
                            changed = true;
                            changedStairs++;
                        }
                        data.ApplyModifiedPropertiesWithoutUndo();
                    }
                    if (changed)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        changedScenes++;
                    }
                    if (opened) EditorSceneManager.CloseScene(scene, true);
                }
                var definitionData = new SerializedObject(definition);
                definitionData.FindProperty("footprint").vector2IntValue = Vector2Int.one;
                definitionData.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            return $"Migrated {changedStairs} stairs in {changedScenes} scenes; footprint=1x1, rise={definition.Height}m.";
        }
    }
}
