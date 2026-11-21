using System;
using System.Collections.Generic;
using ProjectNeo.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// 화살 발사체(StraightProjectile)의 임시 구체 외형을 화살 모델로 바꾸고, 박혀 남는 StuckArrow를 붙인다(ART-05).
    /// 여러 번 실행해도 같은 결과가 된다.
    /// </summary>
    public static class ArrowModelSetup
    {
        public const string ProjectilePath = "Assets/_Project/Prefabs/UnitBattlePrototype/StraightProjectile.prefab";
        public const string ArrowModelPath = "Assets/ThirdParty/PolygonFantasyKingdom/Prefabs/Weapons/SM_Prop_Arrow_01_Optimized.prefab";
        public const string SettingsPath = "Assets/_Project/Data/UnitBattlePrototype/StuckArrow.asset";
        const string VisualName = "Visual";

        [MenuItem("ProjectNeo/Setup/Apply Arrow Model")]
        public static void Apply()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowModelPath);
            if (model == null) throw new InvalidOperationException("Arrow model missing: " + ArrowModelPath + " (run git submodule update --init).");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath) == null)
                throw new InvalidOperationException("Projectile prefab missing: " + ProjectilePath);
            var settings = AssetDatabase.LoadAssetAtPath<StuckArrowSettings>(SettingsPath);
            if (settings == null) settings = CombatPrototypeSetup.Save(StuckArrowSettings.Create(), SettingsPath);

            GameObject root = PrefabUtility.LoadPrefabContents(ProjectilePath);
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = root.transform.GetChild(i);
                    if (child.name == VisualName) UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
                // 원본 프리팹과 연결하지 않은 복사본을 쓴다. 외부 자산 프리팹이 바뀌어도 발사체가 따라 바뀌지 않는다.
                var visual = UnityEngine.Object.Instantiate(model);
                visual.name = VisualName;
                visual.transform.SetParent(root.transform, false);
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                // 발사체 자신이 충돌체를 가지면 첫 프레임 겹침 검사에 스스로 걸린다.
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                var converted = new Dictionary<Material, Material>();
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = CharacterModelSetup.ToUrp(materials[i], converted);
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                // 모델은 +Z가 화살촉이다. 발사체 위치(충돌 검사 지점)가 화살촉 끝이 되도록 뒤로 민다.
                visual.transform.localPosition = new Vector3(0f, 0f, -TipForward(visual));

                var stuck = root.GetComponent<StuckArrow>();
                if (stuck == null) stuck = root.AddComponent<StuckArrow>();
                stuck.Configure(settings);
                PrefabUtility.SaveAsPrefabAsset(root, ProjectilePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        /// <summary>모델 원점에서 화살촉 끝(+Z 최댓값)까지의 거리.</summary>
        static float TipForward(GameObject visual)
        {
            float tip = 0f;
            foreach (var filter in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds bounds = filter.sharedMesh.bounds;
                Matrix4x4 toVisual = visual.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x, (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                    tip = Mathf.Max(tip, toVisual.MultiplyPoint3x4(corner).z);
                }
            }
            return tip;
        }
    }
}
