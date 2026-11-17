using System;
using System.Collections.Generic;
using System.Linq;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Squads;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    public static class WallCommandSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/WallCommandPrototype.unity";
        const string Data = "Assets/_Project/Data/WallCommandPrototype";

        [MenuItem("ProjectNeo/Setup/Create Wall Command Prototype")]
        public static void Create()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("WallCommandPrototype already exists; refusing to overwrite it.");
            Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/ConstructionPrototype.unity", OpenSceneMode.Single);
            // 원래 건설 시험은 유지하고 새 씬에서 연결한다.
            EditorSceneManager.SaveScene(scene, ScenePath);
            ConstructionWorld world = Find<ConstructionWorld>(scene);
            ConstructionSandbox construction = Find<ConstructionSandbox>(scene);
            PlacementController placement = Find<PlacementController>(scene);
            Camera buildCamera = Find<ConstructionCameraController>(scene).GetComponent<Camera>();
            ConstructionHud buildHud = Find<ConstructionHud>(scene);
            GameObject oldUnit = construction.Unit.gameObject;
            var fall = Required<FallSettings>("Assets/_Project/Data/ConstructionPrototype/Fall.asset");
            var aiSettings = Required<UnitAISettings>("Assets/_Project/Data/SquadCommandPrototype/AllyAI.asset");
            CombatPrototypeSetup.EnsureFolder(Data);
            var commandSettings = CombatPrototypeSetup.Save(SquadCommandSettings.Create(2, 1.1f), Data + "/Command.asset");
            var config = Required<SquadConfig>("Assets/_Project/Data/SquadCommandPrototype/SquadConfig.asset");
            var actors = new List<GameObject>();
            var rosters = new SquadRoster[3];
            for (int squad = 0; squad < rosters.Length; squad++)
            {
                string prefab = squad == 2 ? "allied_ranged" : "allied_melee";
                var source = Required<GameObject>("Assets/_Project/Prefabs/UnitBattlePrototype/" + prefab + ".prefab");
                rosters[squad].members = new UnitCombatAI[4];
                Vector3 origin = squad == 0 ? new Vector3(-7.5f, 0f, -8f)
                    : squad == 1 ? new Vector3(6f, 0f, -8f) : new Vector3(0f, 0f, -14f);
                for (int i = 0; i < 4; i++)
                {
                    var unit = (GameObject)PrefabUtility.InstantiatePrefab(source);
                    unit.SetActive(false);
                    unit.name = "Squad" + (squad + 1) + "_" + (i + 1);
                    unit.transform.position = origin + new Vector3((i % 2 - 0.5f) * 1.1f, 0f, -(i / 2) * 1.1f);
                    var ai = unit.GetComponent<UnitCombatAI>();
                    SetReference(ai, "settings", aiSettings);
                    unit.AddComponent<UnitFallController>().Configure(world, fall);
                    rosters[squad].members[i] = ai;
                    actors.Add(unit);
                }
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(
                Required<GameObject>("Assets/_Project/Prefabs/CombatPrototype/Player.prefab"));
            player.name = "Player"; player.SetActive(false);
            player.transform.position = new Vector3(0f, 3f, 4.5f);
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var controls = Required<PlayerControlSettings>("Assets/_Project/Data/CombatPrototype/PlayerControls.asset");
            var cameraRoot = new GameObject("FirstPersonCamera");
            cameraRoot.SetActive(false); cameraRoot.tag = "MainCamera";
            var camera = cameraRoot.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f; camera.farClipPlane = 200f;
            cameraRoot.AddComponent<AudioListener>();
            var firstPerson = cameraRoot.AddComponent<FirstPersonCamera>();
            firstPerson.Configure(player.transform, controls);
            var playerInput = player.AddComponent<PlayerInputController>();
            playerInput.Configure(Required<InputActionAsset>("Assets/_Project/Input/InputSystem_Actions.inputactions"), firstPerson, controls);
            playerInput.enabled = false;
            player.AddComponent<UnitFallController>().Configure(world, fall);
            actors.Add(player);

            var commandRoot = new GameObject("SquadCommand");
            commandRoot.SetActive(false);
            var center = commandRoot.AddComponent<SquadCommandCenter>();
            center.Configure(config, commandSettings, rosters);
            var input = commandRoot.AddComponent<SquadCommandInput>();
            input.Configure(center, camera); input.enabled = false;
            SquadCommandSetup.CreateHud(center, input, "\nTab: construction view");
            SquadCommandSetup.AddMarkers(center);
            GameObject commandHud = Find<SquadCommandHud>(scene).gameObject;
            AddHudBacking(commandHud.GetComponent<SquadCommandHud>());
            commandHud.SetActive(false);
            commandRoot.SetActive(true);
            construction.Configure(world, rosters[0].members[0].GetComponent<CharacterActor>());
            construction.ConfigureCommands(center);
            UnityEngine.Object.DestroyImmediate(oldUnit);
            AddBuildHint(buildHud);
            var modeRoot = new GameObject("WallCommandSandbox");
            modeRoot.AddComponent<WallCommandSandbox>().Configure(buildCamera, firstPerson, placement,
                playerInput, input, buildHud.gameObject, commandHud, actors.ToArray());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }

        static void AddBuildHint(ConstructionHud hud)
        {
            var root = new GameObject("ViewHint", typeof(RectTransform), typeof(TextMeshProUGUI));
            root.transform.SetParent(hud.transform, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -24f); rect.sizeDelta = new Vector2(440f, 64f);
            var label = root.GetComponent<TextMeshProUGUI>();
            label.font = Required<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            label.text = "Tab: first-person commands"; label.fontSize = 22f;
            label.alignment = TextAlignmentOptions.TopRight; label.raycastTarget = false;
            hud.ConfigureWeapons(new List<SiegeWeaponDefinition>(hud.Weapons).ToArray());
        }

        public static void AddHudBacking(SquadCommandHud hud)
        {
            foreach (string name in new[] { "Squads", "Help" })
            {
                if (hud.transform.Find(name + "Backing") != null) continue;
                var label = hud.transform.Find(name).GetComponent<RectTransform>();
                var root = new GameObject(name + "Backing", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                root.transform.SetParent(hud.transform, false);
                var rect = root.GetComponent<RectTransform>();
                rect.anchorMin = label.anchorMin; rect.anchorMax = label.anchorMax; rect.pivot = label.pivot;
                rect.anchoredPosition = label.anchoredPosition - new Vector2(8f, name == "Squads" ? -8f : 8f);
                rect.sizeDelta = label.sizeDelta + Vector2.one * 16f;
                var image = root.GetComponent<UnityEngine.UI.Image>();
                image.color = new Color(0.03f, 0.05f, 0.07f, 0.8f); image.raycastTarget = false;
                root.transform.SetAsFirstSibling();
            }
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
