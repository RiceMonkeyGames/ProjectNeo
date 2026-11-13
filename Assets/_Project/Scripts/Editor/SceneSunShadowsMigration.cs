using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    /// <summary>ART-02: enable soft shadows on every directional light in project scenes (setup scripts created them with LightShadows.None).</summary>
    public static class SceneSunShadowsMigration
    {
        const string ScenesFolder = "Assets/_Project/Scenes";

        [MenuItem("ProjectNeo/Setup/Enable Scene Sun Shadows")]
        public static void MigrateFromMenu() => Debug.Log(Migrate());

        public static string Migrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            int scenes = 0, lights = 0, already = 0;
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { ScenesFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Scene scene = SceneManager.GetSceneByPath(path);
                    bool opened = !scene.IsValid() || !scene.isLoaded;
                    if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    bool changed = false;
                    foreach (var light in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>(true)))
                    {
                        if (light.type != LightType.Directional) continue;
                        if (light.shadows == LightShadows.Soft) { already++; continue; }
                        var data = new SerializedObject(light);
                        data.FindProperty("m_Shadows.m_Type").intValue = (int)LightShadows.Soft;
                        data.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                        lights++;
                    }
                    if (changed)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        scenes++;
                    }
                    if (opened) EditorSceneManager.CloseScene(scene, true);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            return $"Enabled soft shadows on {lights} directional lights in {scenes} scenes; already soft={already}.";
        }
    }
}
