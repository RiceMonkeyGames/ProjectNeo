using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using ProjectNeo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    /// <summary>UI-02: migrate authored UI to English without rebuilding scenes or changing gameplay data.</summary>
    public static class EnglishUiMigration
    {
        static readonly Regex Hangul = new Regex("[\\uAC00-\\uD7A3]");
        static readonly Dictionary<string, string> Texts = new Dictionary<string, string>
        {
            { "주점", "Tavern" }, { "상점", "Shop" }, { "훈련장", "Training ground" },
            { "대장간", "Smithy" }, { "수성", "Defense" }, { "성벽", "Wall" },
            { "계단", "Stairs" }, { "메인 성문", "Main gate" }, { "대포", "Cannon" },
            { "회복약", "Healing potion" }, { "철검", "Iron sword" }, { "강철검", "Steel sword" },
            { "나무 방패", "Wooden shield" }, { "가죽 갑옷", "Leather armor" }, { "사슬 갑옷", "Chain armor" },
            { "뼈 조각", "Bone shard" }, { "질긴 가죽", "Tough hide" }, { "깃털", "Feather" },
            { "근접 잡졸", "Melee soldier" }, { "원거리 잡졸", "Ranged soldier" },
            { "용병 검사", "Mercenary swordsman" }, { "용병 궁수", "Mercenary archer" },
            { "기사", "Knight" }, { "성기사", "Paladin" },
            { "닫기 (Esc)", "Close (Esc)" }, { "리롤", "Reroll" },
            { "장비 · 플레이어", "Equipment - Player" }, { "착용 중 (클릭해서 벗기)", "Equipped (click to unequip)" },
            { "보관함 장비 (클릭해서 착용)", "Stash equipment (click to equip)" },
            { "매물 (클릭해서 구매)", "Offers (click to buy)" }, { "보관함 (클릭해서 판매)", "Stash (click to sell)" },
            { "부대 편성", "Squad organization" }, { "부대", "Squad" }
        };

        public static string Audit() => Visit(false);

        [MenuItem("ProjectNeo/Setup/Migrate UI To English")]
        public static void Apply()
        {
            string audit = Audit();
            if (audit.Contains("UNMAPPED")) throw new InvalidOperationException(audit);
            Debug.Log(Visit(true));
        }

        static string Visit(bool apply)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for an idle Editor before migrating UI.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before migrating UI.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var report = new StringBuilder();
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    bool changed = false;
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
                        {
                            string translated = Translate(label.text, path + ":" + label.name, report);
                            if (apply && translated != label.text)
                            {
                                label.text = translated;
                                EditorUtility.SetDirty(label);
                                changed = true;
                            }
                        }
                        foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                        {
                            if (component == null) throw new InvalidOperationException("Missing script in " + path);
                            if (component.GetType().Name == "RuntimeSystemFont")
                            {
                                if (apply) UnityEngine.Object.DestroyImmediate(component);
                                changed = true;
                            }
                            // Re-save components whose obsolete serialized caption/font fields were removed.
                            else if (component is ConstructionHud || component is SiegeHud || component is TitleScreen || component is GameFlowHud || component is SquadCommandHud)
                                changed = true;
                        }
                    }
                    if (apply && changed)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        report.AppendLine("SAVED " + path);
                    }
                }
                foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project/Data" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (asset == null) continue;
                    var data = new SerializedObject(asset);
                    SerializedProperty name = data.FindProperty("displayName");
                    if (name == null || name.propertyType != SerializedPropertyType.String) continue;
                    string translated = Translate(name.stringValue, path + ":displayName", report);
                    if (apply && translated != name.stringValue)
                    {
                        name.stringValue = translated;
                        data.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(asset);
                        report.AppendLine("SAVED " + path);
                    }
                }
                if (apply) AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            return report.ToString();
        }

        static string Translate(string text, string location, StringBuilder report)
        {
            if (string.IsNullOrEmpty(text) || !Hangul.IsMatch(text)) return text;
            if (Texts.TryGetValue(text, out string english)) return english;
            if (location.EndsWith(":Help", StringComparison.Ordinal))
            {
                string help = text.Replace("부대 선택", "select squad").Replace("전체 선택", "select all")
                    .Replace("전체", "select all").Replace("이동(화면 가운데가 가리키는 곳)", "move to aim point")
                    .Replace("이동", "move").Replace("공격", "attack").Replace("중지", "hold")
                    .Replace("마우스 시점", "Mouse: look").Replace("좌클릭", "Click:")
                    .Replace("커서 해제", "release cursor").Replace("F 누르고 있기: 가까운 사다리 밀기", "Hold F: push nearby ladder")
                    .Replace("Tab 건설 시점으로 전환", "Tab: construction view");
                if (!Hangul.IsMatch(help)) return help;
            }
            report.AppendLine("UNMAPPED " + location + " = " + text.Replace("\n", "\\n"));
            return text;
        }
    }
}
