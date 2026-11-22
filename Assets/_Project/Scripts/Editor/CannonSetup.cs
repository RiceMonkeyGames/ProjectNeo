using System;
using ProjectNeo.Characters;
using ProjectNeo.Siege;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// SIEGE-02 대포 사격을 에셋과 Game 씬에 연결한다. 대포 설정·포탄 프리팹을 만들고, 기존 대포 프리팹에 사격 부품을,
    /// Game 씬 플레이어에 직접 조작 부품을, 전투 화면에 대포 안내를 붙인다. 여러 번 실행해도 결과가 같다.
    /// </summary>
    public static class CannonSetup
    {
        const string Data = "Assets/_Project/Data/ConstructionPrototype";
        const string Prefabs = "Assets/_Project/Prefabs/ConstructionPrototype";
        internal const string SettingsPath = Data + "/CannonSettings.asset";
        internal const string ShellPath = Prefabs + "/CannonShell.prefab";
        internal const string CannonPrefabPath = Prefabs + "/Cannon.prefab";
        internal const string OperatorPath = "Assets/_Project/Data/GameFlow/CannonOperator.asset";

        [MenuItem("ProjectNeo/Setup/Connect Cannon Firing (SIEGE-02)")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            CannonSettings settings = CreateAssets(out CannonShell shell);
            ConfigureCannonPrefab(settings, shell);
            Scene game = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            ConfigureGame(game);
            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            AssetDatabase.SaveAssets();
        }

        internal static CannonSettings CreateAssets(out CannonShell shell)
        {
            CombatPrototypeSetup.EnsureFolder(Data);
            CombatPrototypeSetup.EnsureFolder(Prefabs);
            // Q-21 임시값. 플레이 테스트 뒤 에셋에서 조정한다.
            CannonSettings settings = CombatPrototypeSetup.Save(CannonSettings.Create(aimHalfAngle: BastionSetup.AutoAimHalfAngle, manualAimHalfAngle: BastionSetup.ManualAimHalfAngle), SettingsPath);
            shell = AssetDatabase.LoadAssetAtPath<CannonShell>(ShellPath);
            if (shell == null)
            {
                Material iron = CombatPrototypeSetup.MaterialAsset("CannonShell", new Color(0.12f, 0.12f, 0.13f));
                Material blast = CombatPrototypeSetup.MaterialAsset("CannonBlast", new Color(1f, 0.55f, 0.15f));
                var root = new GameObject("CannonShell");
                root.AddComponent<CannonShell>();
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = "Ball";
                UnityEngine.Object.DestroyImmediate(ball.GetComponent<Collider>());
                ball.GetComponent<Renderer>().sharedMaterial = iron;
                ball.transform.SetParent(visual.transform, false);
                ball.transform.localScale = Vector3.one * 0.35f;
                visual.AddComponent<PlaceholderCannonBlastView>().Configure(blast, settings.BlastRadius);
                shell = PrefabUtility.SaveAsPrefabAsset(root, ShellPath).GetComponent<CannonShell>();
                UnityEngine.Object.DestroyImmediate(root);
            }
            return settings;
        }

        internal static void ConfigureCannonPrefab(CannonSettings settings, CannonShell shell)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(CannonPrefabPath);
            try
            {
                if (contents.GetComponent<PlacedSiegeWeapon>() == null)
                    throw new InvalidOperationException("Cannon prefab needs PlacedSiegeWeapon: " + CannonPrefabPath);
                var module = contents.GetComponent<CannonModule>();
                if (module == null) module = contents.AddComponent<CannonModule>();
                module.Configure(settings, shell);
                PrefabUtility.SaveAsPrefabAsset(contents, CannonPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        internal static void ConfigureGame(Scene game)
        {
            var world = Find<ConstructionWorld>(game);
            var input = Find<PlayerInputController>(game);
            var view = Find<FirstPersonCamera>(game);
            var hud = Find<GameFlowHud>(game);
            if (world == null || input == null || view == null || hud == null)
                throw new InvalidOperationException("Game scene needs castle, player, first-person camera and flow HUD.");
            CannonOperatorSettings operatorSettings = CombatPrototypeSetup.Save(CannonOperatorSettings.Create(), OperatorPath);
            var cannonOperator = input.GetComponent<CannonOperator>();
            if (cannonOperator == null) cannonOperator = input.gameObject.AddComponent<CannonOperator>();
            cannonOperator.Configure(operatorSettings, world, view, view.GetComponent<Camera>());
            EditorUtility.SetDirty(cannonOperator);
            hud.ConfigureCannon(cannonOperator);
            EditorUtility.SetDirty(hud);
        }

        static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            { var component = root.GetComponentInChildren<T>(true); if (component != null) return component; }
            return null;
        }
    }
}
