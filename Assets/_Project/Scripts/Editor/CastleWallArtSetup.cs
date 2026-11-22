using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// 성벽·계단 조각의 임시 외형(Visual)을 PolygonFantasyKingdom 성 모델로 바꾼다(ART-06).
    /// 조각 프리팹은 1×1×1 단위 크기이고 배치할 때 칸 크기로 늘어나므로, 모델을 그 단위 상자에 꼭 맞춘다.
    /// 충돌·길찾기·규칙은 루트에 그대로 두고 외형만 바꾼다. 여러 번 실행해도 같은 결과가 된다.
    /// </summary>
    public static class CastleWallArtSetup
    {
        public const string WallPath = "Assets/_Project/Prefabs/ConstructionPrototype/Wall.prefab";
        public const string StairsPath = "Assets/_Project/Prefabs/ConstructionPrototype/Stairs.prefab";
        public const string MaterialFolder = "Assets/_Project/Art/Castle/Materials";
        const string Castle = "Assets/ThirdParty/PolygonFantasyKingdom/Prefabs/Castle/";
        public const string WallModelPath = Castle + "SM_Bld_Castle_Wall_Block_01.prefab";
        public const string StairsModelPath = Castle + "SM_Bld_Stairs_Straight_Wide_01.prefab";
        const string VisualName = "Visual";
        public const string ModelName = "Model";

        [MenuItem("ProjectNeo/Setup/Apply Castle Wall Art")]
        public static void Apply()
        {
            var converted = new Dictionary<Material, Material>();
            // 성벽 블록은 그대로 단위 상자를 채운다.
            Replace(WallPath, WallModelPath, 0f, converted);
            // 계단 모델은 -X 쪽이 높다. 조각의 경사(UnitRamp)는 +Z 쪽이 높으므로 Y축으로 90도 돌린다.
            Replace(StairsPath, StairsModelPath, 90f, converted);
            ApplyBattlementStone(converted);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 성가퀴(모양은 그대로)에 성벽과 같은 돌 재질을 입힌다. 반복 길이는 성벽 블록 한 면(칸 3m)에 텍스처가 걸리는 길이와 같게 해
        /// 벽과 성가퀴의 돌 크기가 맞는다.
        /// </summary>
        static void ApplyBattlementStone(Dictionary<Material, Material> converted)
        {
            var settings = AssetDatabase.LoadAssetAtPath<ProjectNeo.Walls.BattlementSettings>(BattlementSetup.SettingsPath);
            if (settings == null) return; // 성가퀴가 없는 프로젝트 상태(설정 메뉴 미실행)면 건너뛴다.
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(WallModelPath);
            var filter = model.GetComponentInChildren<MeshFilter>();
            var renderer = model.GetComponentInChildren<MeshRenderer>();
            float span = UvSpanY(filter.sharedMesh);
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("material").objectReferenceValue =
                CharacterModelSetup.ToUrp(renderer.sharedMaterial, converted, MaterialFolder);
            serialized.FindProperty("textureTileMeters").floatValue = span > 1e-4f ? CellMeters / span : 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>격자 한 칸 크기(m). 지도 규격(3m 칸)과 같다. 성벽 블록 모델이 이 크기로 늘어난다.</summary>
        const float CellMeters = 3f;

        static float UvSpanY(Mesh mesh)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (Vector2 uv in mesh.uv) { min = Mathf.Min(min, uv.y); max = Mathf.Max(max, uv.y); }
            return max > min ? max - min : 0f;
        }

        static void Replace(string prefabPath, string modelPath, float yaw, Dictionary<Material, Material> converted)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) throw new InvalidOperationException("Castle model missing: " + modelPath + " (run git submodule update --init).");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                throw new InvalidOperationException("Piece prefab missing: " + prefabPath);

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform visual = root.transform.Find(VisualName);
                if (visual == null)
                {
                    visual = new GameObject(VisualName).transform;
                    visual.SetParent(root.transform, false);
                }
                // 예전 외형(임시 상자·경사 메시)을 걷어낸다. Visual 자신에 붙은 렌더러도 지운다.
                for (int i = visual.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(visual.GetChild(i).gameObject);
                if (visual.TryGetComponent(out MeshRenderer oldRenderer)) UnityEngine.Object.DestroyImmediate(oldRenderer);
                if (visual.TryGetComponent(out MeshFilter oldFilter)) UnityEngine.Object.DestroyImmediate(oldFilter);
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;

                // 원본 프리팹과 연결하지 않은 복사본을 쓴다. 외부 자산이 바뀌어도 조각이 따라 바뀌지 않는다.
                var instance = UnityEngine.Object.Instantiate(model);
                instance.name = ModelName;
                instance.transform.SetParent(visual, false);
                instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                // 충돌은 조각 루트의 상자·경사 충돌체가 맡는다. 모델 충돌체가 남으면 길찾기 메시가 바뀐다.
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = CharacterModelSetup.ToUrp(materials[i], converted, MaterialFolder);
                    renderer.sharedMaterials = materials;
                }
                FitUnitBox(instance.transform, visual);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>모델이 Visual 공간에서 x·z −0.5~0.5, y 0~1 상자를 꼭 채우도록 크기와 위치를 맞춘다.</summary>
        static void FitUnitBox(Transform model, Transform space)
        {
            model.localScale = Vector3.one;
            model.localPosition = Vector3.zero;
            Bounds bounds = MeshBounds(model, space);
            Vector3 size = bounds.size;
            // 90도 단위 회전이라 Visual 축과 모델 축이 맞물린다. 회전 뒤의 크기를 모델 축으로 되돌려 배율을 정한다.
            Vector3 axisScale = new Vector3(1f / Mathf.Max(1e-4f, size.x), 1f / Mathf.Max(1e-4f, size.y), 1f / Mathf.Max(1e-4f, size.z));
            Vector3 local = Quaternion.Inverse(model.localRotation) * axisScale;
            model.localScale = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
            bounds = MeshBounds(model, space);
            model.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
        }

        static Bounds MeshBounds(Transform model, Transform space)
        {
            var result = new Bounds();
            bool first = true;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds local = filter.sharedMesh.bounds;
                Matrix4x4 toSpace = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? local.min.x : local.max.x, (i & 2) == 0 ? local.min.y : local.max.y,
                        (i & 4) == 0 ? local.min.z : local.max.z);
                    Vector3 point = toSpace.MultiplyPoint3x4(corner);
                    if (first) { result = new Bounds(point, Vector3.zero); first = false; }
                    else result.Encapsulate(point);
                }
            }
            return result;
        }
    }
}
