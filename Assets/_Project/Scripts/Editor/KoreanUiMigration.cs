using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// UI-04: switch authored UI text (scene and prefab TMP captions, definition display strings) to Korean
    /// through Editor APIs. Runtime-built text lives in code and is translated there.
    /// </summary>
    public static partial class KoreanUiMigration
    {
        static readonly string[] SceneFolders = { "Assets/_Project/Scenes" };
        static readonly string[] DataFolders = { "Assets/_Project/Data", "Assets/_Project/Prefabs" };

        public const string DumpPath = "Temp/KoreanUiDump.tsv";
        public const string AuditPath = "Temp/KoreanUiAudit.tsv";

        public static string Dump() => Dump(DumpPath);

        /// <summary>Writes every authored text with Latin letters to <paramref name="outputPath"/>, one "location\ttext" line each.</summary>
        public static string Dump(string outputPath)
        {
            var lines = new List<string>();
            Visit((location, text) =>
            {
                if (HasLatin(text)) lines.Add(location + "\t" + Escape(text));
                return text;
            }, false);
            File.WriteAllLines(outputPath, lines, new UTF8Encoding(false));
            return "lines=" + lines.Count;
        }

        [MenuItem("ProjectNeo/Setup/Migrate UI To Korean")]
        public static string Apply()
        {
            var unmapped = new StringBuilder();
            int changed = Visit((location, text) =>
            {
                if (!HasLatin(text)) return text;
                if (Texts.TryGetValue(text, out string korean)) return korean;
                if (!Keep.Contains(text)) unmapped.AppendLine("UNMAPPED " + location + " = " + Escape(text));
                return text;
            }, true);
            string result = "changed=" + changed + "\n" + unmapped;
            Debug.Log(result);
            return result;
        }

        public static string Audit()
        {
            var unmapped = new StringBuilder();
            Visit((location, text) =>
            {
                if (HasLatin(text) && !Texts.ContainsKey(text) && !Keep.Contains(text))
                    unmapped.AppendLine(location + "\t" + Escape(text));
                return text;
            }, false);
            return unmapped.ToString();
        }

        static bool HasLatin(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) return true;
            return false;
        }

        static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\t", "\\t");

        static int Visit(Func<string, string, string> map, bool apply)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for an idle Editor before migrating UI.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before migrating UI.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            int changed = 0;
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Scene", SceneFolders))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    bool dirty = false;
                    foreach (GameObject root in scene.GetRootGameObjects())
                        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
                        {
                            string next = map(path + ":" + HierarchyPath(label.transform), label.text);
                            if (apply && next != label.text)
                            {
                                Undo.RecordObject(label, "Korean UI");
                                label.text = next;
                                EditorUtility.SetDirty(label);
                                dirty = true;
                                changed++;
                            }
                        }
                    if (apply && dirty)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }

                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", DataFolders))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject prefab = PrefabUtility.LoadPrefabContents(path);
                    bool dirty = false;
                    try
                    {
                        foreach (TMP_Text label in prefab.GetComponentsInChildren<TMP_Text>(true))
                        {
                            string next = map(path + ":" + HierarchyPath(label.transform), label.text);
                            if (apply && next != label.text)
                            {
                                label.text = next;
                                dirty = true;
                                changed++;
                            }
                        }
                        if (dirty) PrefabUtility.SaveAsPrefabAsset(prefab, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(prefab); }
                }

                foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", DataFolders))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (asset == null) continue;
                    var data = new SerializedObject(asset);
                    SerializedProperty property = data.GetIterator();
                    bool dirty = false;
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.String) continue;
                        if (!IsDisplayField(property.name)) continue;
                        string text = property.stringValue;
                        string next = map(path + ":" + property.propertyPath, text);
                        if (apply && next != text)
                        {
                            property.stringValue = next;
                            dirty = true;
                            changed++;
                        }
                    }
                    if (dirty)
                    {
                        data.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(asset);
                    }
                }
                if (apply) AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            return changed;
        }

        /// <summary>Serialized string fields shown to players. Ids, keys and paths stay untouched.</summary>
        static bool IsDisplayField(string name)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("id") && !n.Contains("hidden")) return false;
            if (n.Contains("key") || n.Contains("path") || n == "m_name" || n.Contains("scene")) return false;
            return n.Contains("name") || n.Contains("label") || n.Contains("title") || n.Contains("description")
                || n.Contains("text") || n.Contains("hint") || n.Contains("caption") || n.Contains("message")
                || n.Contains("summary") || n.Contains("help");
        }

        static string HierarchyPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }
    }
}
