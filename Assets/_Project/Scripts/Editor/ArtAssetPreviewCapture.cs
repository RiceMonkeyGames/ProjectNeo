using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// Q-01: <c>Docs/codemap/ArtAssets.md</c>에 적힌 서드파티 프리팹 경로(<c>`Pack/Prefabs/.../Name`</c>)를 모두 찾아
    /// 미리보기 사진을 <c>Docs/codemap/art-assets/</c>에 PNG로 찍는다. 문서에 에셋을 추가한 뒤 다시 실행하면 새 사진만 늘어난다.
    /// 서브모듈 머티리얼은 Built-in 셰이더라 메모리 안에서만 URP Lit로 바꿔 찍고, 에셋은 아무것도 만들거나 고치지 않는다.
    /// </summary>
    public static class ArtAssetPreviewCapture
    {
        public const string DocPath = "Docs/codemap/ArtAssets.md";
        public const string ImageFolder = "Docs/codemap/art-assets";
        const string ThirdPartyRoot = "Assets/ThirdParty/";
        const int Size = 512;

        static readonly Regex AssetPathPattern = new Regex("`([A-Za-z0-9_ ]+(?:/[A-Za-z0-9_ ]+)*/Prefabs/[A-Za-z0-9_ /]+)`");

        /// <summary>문서 속 에셋 경로를 사진 파일 이름으로 바꾼다. 같은 이름이 여러 팩에 있어 팩 폴더 이름을 앞에 붙인다.</summary>
        public static string ImageFileName(string assetPath)
        {
            string pack = assetPath.Substring(0, assetPath.IndexOf('/'));
            string name = assetPath.Substring(assetPath.LastIndexOf('/') + 1);
            return (pack + "__" + name).Replace(' ', '_') + ".png";
        }

        [MenuItem("ProjectNeo/Docs/Capture Art Asset Previews")]
        public static void CaptureMenu() => Debug.Log(Capture(false));

        [MenuItem("ProjectNeo/Docs/Recapture All Art Asset Previews")]
        public static void RecaptureMenu() => Debug.Log(Capture(true));

        /// <summary>사진을 찍고 결과 요약을 돌려준다. <paramref name="overwrite"/>가 false면 이미 있는 사진은 건너뛴다.</summary>
        public static string Capture(bool overwrite)
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            string doc = File.ReadAllText(Path.Combine(root, DocPath));
            string folder = Path.Combine(root, ImageFolder);
            Directory.CreateDirectory(folder);

            var seen = new HashSet<string>();
            int captured = 0, skipped = 0;
            var missing = new List<string>();
            foreach (Match match in AssetPathPattern.Matches(doc))
            {
                string assetPath = match.Groups[1].Value;
                // 프로젝트 쪽 경로(Assets/_Project/...)와 폴더만 적힌 경로는 서드파티 프리팹이 아니다.
                if (assetPath.StartsWith("Assets/", StringComparison.Ordinal) || assetPath.EndsWith("/", StringComparison.Ordinal)) continue;
                if (!seen.Add(assetPath)) continue;
                string file = Path.Combine(folder, ImageFileName(assetPath));
                if (!overwrite && File.Exists(file)) { skipped++; continue; }
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThirdPartyRoot + assetPath + ".prefab");
                if (prefab == null) { missing.Add(assetPath); continue; }
                File.WriteAllBytes(file, Render(prefab));
                captured++;
            }
            return "Art asset previews: captured=" + captured + " skipped=" + skipped + " missing=" + missing.Count
                + (missing.Count > 0 ? " [" + string.Join(", ", missing) + "]" : "");
        }

        static byte[] Render(GameObject prefab)
        {
            // 인스펙터 모델 미리보기와 같은 PreviewRenderUtility를 쓴다(URP에서도 전용 미리보기 씬으로 그린다).
            var preview = new PreviewRenderUtility();
            var temporary = new List<Material>();
            Texture2D image = null;
            try
            {
                var instance = Object.Instantiate(prefab);
                preview.AddSingleGO(instance);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var renderers = instance.GetComponentsInChildren<Renderer>();
                var converted = new Dictionary<Material, Material>();
                Bounds bounds = default;
                bool hasBounds = false;
                foreach (Renderer renderer in renderers)
                {
                    if (renderer is ParticleSystemRenderer) { renderer.enabled = false; continue; }
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = ToUrp(materials[i], converted, temporary);
                    renderer.sharedMaterials = materials;
                    if (hasBounds) bounds.Encapsulate(renderer.bounds);
                    else { bounds = renderer.bounds; hasBounds = true; }
                }
                if (!hasBounds) bounds = new Bounds(Vector3.zero, Vector3.one);

                preview.lights[0].transform.rotation = Quaternion.Euler(45f, 150f, 0f);
                preview.lights[0].intensity = 1.3f;
                preview.lights[1].transform.rotation = Quaternion.Euler(20f, 320f, 0f);
                preview.lights[1].intensity = 0.5f;
                preview.ambientColor = new Color(0.35f, 0.35f, 0.38f);

                Camera camera = preview.camera;
                camera.fieldOfView = 30f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.80f, 0.82f, 0.85f);
                // Synty 모델은 +Z를 바라보므로 앞·오른쪽·위에서 비스듬히 본다.
                Vector3 direction = new Vector3(0.55f, 0.4f, 1f).normalized;
                float radius = Mathf.Max(bounds.extents.magnitude, 0.05f);
                float distance = FitDistance(bounds, -direction, camera.fieldOfView);
                camera.transform.position = bounds.center + direction * distance;
                camera.transform.LookAt(bounds.center);
                camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
                camera.farClipPlane = distance + radius * 2f;
                preview.BeginStaticPreview(new Rect(0, 0, Size, Size));
                preview.Render(true);
                image = preview.EndStaticPreview();
                return image.EncodeToPNG();
            }
            finally
            {
                preview.Cleanup();
                foreach (Material material in temporary) Object.DestroyImmediate(material);
                if (image != null) Object.DestroyImmediate(image);
            }
        }

        /// <summary>경계 상자 모서리 8개가 모두 화면 안(여백 10%)에 들어오는 가장 가까운 카메라 거리.</summary>
        static float FitDistance(Bounds bounds, Vector3 forward, float fieldOfView)
        {
            Quaternion look = Quaternion.LookRotation(forward);
            Vector3 right = look * Vector3.right, up = look * Vector3.up;
            float tan = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.9f;
            float distance = 0.05f;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? -bounds.extents.x : bounds.extents.x,
                    (i & 2) == 0 ? -bounds.extents.y : bounds.extents.y,
                    (i & 4) == 0 ? -bounds.extents.z : bounds.extents.z);
                float depth = Vector3.Dot(corner, forward);
                float side = Mathf.Max(Mathf.Abs(Vector3.Dot(corner, right)), Mathf.Abs(Vector3.Dot(corner, up)));
                distance = Mathf.Max(distance, side / tan - depth);
            }
            return distance;
        }

        /// <summary>Built-in 머티리얼을 메모리 전용 URP Lit 복사본으로 바꾼다(저장하지 않음).</summary>
        static Material ToUrp(Material source, Dictionary<Material, Material> converted, List<Material> temporary)
        {
            if (source == null || source.shader.name.StartsWith("Universal Render Pipeline", StringComparison.Ordinal)) return source;
            if (converted.TryGetValue(source, out Material cached)) return cached;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { hideFlags = HideFlags.HideAndDontSave };
            if (source.HasProperty("_MainTex")) material.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
            material.SetColor("_BaseColor", source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
            material.SetFloat("_Smoothness", 0.1f);
            material.SetFloat("_Metallic", 0f);
            converted[source] = material;
            temporary.Add(material);
            return material;
        }
    }
}
