using System;
using ProjectNeo.Combat;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// SIEGE-12 성문 위 마치콜레이션. 치수 에셋, 화살 공격 방식 모듈, 구멍 표시 재질을 만들고 Game 씬 성에 붙인다.
    /// 여러 번 실행해도 컴포넌트와 에셋을 중복으로 만들지 않는다.
    /// </summary>
    public static class MachicolationSetup
    {
        public const string SettingsPath = "Assets/_Project/Data/GameFlow/Build/Machicolations.asset";
        public const string ArrowPath = "Assets/_Project/Data/GameFlow/Build/MachicolationArrow.asset";
        const string HolePath = "Assets/_Project/Art/Placeholder/MachicolationHole.mat";

        [MenuItem("ProjectNeo/Setup/Add Machicolations To Game Scene")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            CreateAssets();
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            // 씬을 열면 앞서 잡은 에셋 참조가 끊길 수 있어 다시 읽는다(BattlementSetup과 같다).
            var settings = AssetDatabase.LoadAssetAtPath<MachicolationSettings>(SettingsPath)
                ?? throw new InvalidOperationException("Machicolation settings asset was not created: " + SettingsPath);
            ConstructionWorld world = GameFacilitySetup.Find<ConstructionWorld>(scene);
            var gate = world.GetComponent<GateMachicolations>();
            if (gate == null) gate = world.gameObject.AddComponent<GateMachicolations>();
            gate.Configure(world, settings);
            EditorUtility.SetDirty(gate);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>에셋을 만들거나 기존 것을 다시 채운다. 테스트와 다른 씬 설정에서도 쓴다.</summary>
        public static MachicolationSettings CreateAssets()
        {
            var arrow = AssetDatabase.LoadAssetAtPath<ArrowMachicolationAttack>(ArrowPath);
            if (arrow == null)
            {
                arrow = ArrowMachicolationAttack.Create();
                AssetDatabase.CreateAsset(arrow, ArrowPath);
            }

            var hole = AssetDatabase.LoadAssetAtPath<Material>(HolePath);
            if (hole == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                hole = new Material(shader) { name = "MachicolationHole" };
                Color dark = new Color(0.06f, 0.05f, 0.05f);
                if (hole.HasProperty("_BaseColor")) hole.SetColor("_BaseColor", dark);
                if (hole.HasProperty("_Color")) hole.SetColor("_Color", dark);
                AssetDatabase.CreateAsset(hole, HolePath);
            }

            var settings = AssetDatabase.LoadAssetAtPath<MachicolationSettings>(SettingsPath);
            if (settings == null)
            {
                settings = MachicolationSettings.Create(new MachicolationAttack[] { arrow });
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            settings.SetAttacks(new MachicolationAttack[] { arrow });
            // 몸체 재질은 킷 벽판 돌 재질(SIEGE-22, Game 성문은 킷으로 꾸민다). 킷이 없으면 비워 두어 받치는 성문의 재질을 쓴다.
            settings.SetMaterials(KitStone(), hole);
            ApplySizes(settings);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        /// <summary>7.5m 칸·5m 층 치수(SIEGE-22). 이미 있는 에셋도 이 값으로 맞춘다.</summary>
        [MenuItem("ProjectNeo/Setup/Resize Machicolations For 7.5m Cells (SIEGE-22)")]
        public static void Resize()
        {
            CreateAssets();
        }

        static void ApplySizes(MachicolationSettings settings)
        {
            var data = new SerializedObject(settings);
            data.FindProperty("standInset").floatValue = 0.85f;
            data.FindProperty("standRadius").floatValue = 0.6f;
            data.FindProperty("shaftWidth").floatValue = 7.5f;
            data.FindProperty("shaftInward").floatValue = 1.2f;
            data.FindProperty("shaftOutward").floatValue = 2f;
            data.FindProperty("searchRadius").floatValue = 15f;
            data.FindProperty("overhangDepth").floatValue = 0.9f;
            data.FindProperty("overhangHeight").floatValue = 0.8f;
            data.FindProperty("overhangWidth").floatValue = 4.3f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static Material KitStone()
        {
            var panel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/CastleKit/Panel.prefab");
            Renderer renderer = panel != null ? panel.GetComponentInChildren<Renderer>() : null;
            return renderer != null ? renderer.sharedMaterial : null;
        }
    }
}
