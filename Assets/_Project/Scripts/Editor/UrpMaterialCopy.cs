using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// Synty Built-in(Standard) 머티리얼을 프로젝트 안 URP Lit 복사본(<c>이름_URP.mat</c>)으로 옮길 때 쓰는 공용 규칙.
    /// 기본 텍스처·색뿐 아니라 원본에 있는 노멀·발광·금속·차폐 맵과 그 세기도 그대로 옮긴다(사용자 요청 2026-11-27: 디테일 우선).
    /// 맵은 원본과 같은 UV·타일링을 쓰므로 기본 텍스처와 어긋나지 않는다.
    /// </summary>
    public static class UrpMaterialCopy
    {
        /// <summary>원본의 텍스처·색·맵을 대상 URP Lit 머티리얼에 쓴다. 이미 있는 복사본에도 다시 써서 빠진 맵을 채운다.</summary>
        public static void CopyFrom(Material source, Material target)
        {
            if (source == null || target == null) return;
            Texture baseMap = Tex(source, "_BaseMap") ?? Tex(source, "_MainTex");
            target.SetTexture("_BaseMap", baseMap);
            target.SetTextureScale("_BaseMap", source.HasProperty("_MainTex") ? source.GetTextureScale("_MainTex") : Vector2.one);
            target.SetTextureOffset("_BaseMap", source.HasProperty("_MainTex") ? source.GetTextureOffset("_MainTex") : Vector2.zero);
            target.SetColor("_BaseColor", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);

            // 노멀 맵.
            Texture normal = Tex(source, "_BumpMap") ?? Tex(source, "_NormalMap") ?? Tex(source, "_Normal");
            target.SetTexture("_BumpMap", normal);
            target.SetFloat("_BumpScale", source.HasProperty("_BumpScale") ? source.GetFloat("_BumpScale") : 1f);
            SetKeyword(target, "_NORMALMAP", normal != null);

            // 금속·매끄러움. 금속 맵이 있으면 알파가 매끄러움이다(Standard와 같은 규칙).
            Texture metallic = Tex(source, "_MetallicGlossMap");
            target.SetTexture("_MetallicGlossMap", metallic);
            target.SetFloat("_Metallic", source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0f);
            float smoothness = metallic != null && source.HasProperty("_GlossMapScale") ? source.GetFloat("_GlossMapScale")
                : source.HasProperty("_Smoothness") ? source.GetFloat("_Smoothness")
                : source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : 0.1f;
            target.SetFloat("_Smoothness", smoothness);
            SetKeyword(target, "_METALLICSPECGLOSSMAP", metallic != null);

            // 차폐 맵.
            Texture occlusion = Tex(source, "_OcclusionMap");
            target.SetTexture("_OcclusionMap", occlusion);
            target.SetFloat("_OcclusionStrength", source.HasProperty("_OcclusionStrength") ? source.GetFloat("_OcclusionStrength") : 1f);
            SetKeyword(target, "_OCCLUSIONMAP", occlusion != null);

            // 발광. 원본에서 발광을 켠 경우만 옮긴다(맵만 있고 꺼 둔 머티리얼이 있다).
            Texture emission = Tex(source, "_EmissionMap") ?? Tex(source, "_Emission");
            Color emissionColor = source.HasProperty("_EmissionColor") ? source.GetColor("_EmissionColor") : Color.black;
            bool emissive = source.IsKeywordEnabled("_EMISSION") && (emission != null || emissionColor.maxColorComponent > 0f);
            target.SetTexture("_EmissionMap", emissive ? emission : null);
            target.SetColor("_EmissionColor", emissive ? emissionColor : Color.black);
            SetKeyword(target, "_EMISSION", emissive);
            target.globalIlluminationFlags = emissive ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            target.enableInstancing = true;
            EditorUtility.SetDirty(target);
        }

        /// <summary>
        /// 프로젝트 안의 모든 <c>이름_URP.mat</c> 복사본을 같은 이름의 서브모듈 원본에서 다시 채운다.
        /// 맵 복사 전에 만든 복사본에 노멀·발광·금속 맵을 채울 때 쓴다. 원본을 못 찾거나 이름이 둘 이상인 복사본은 건너뛰고 알린다.
        /// </summary>
        [MenuItem("ProjectNeo/Setup/Refresh URP Material Copies From Synty Sources")]
        public static string RefreshAll()
        {
            var sources = new Dictionary<string, List<Material>>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/ThirdParty" }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null) continue;
                if (!sources.TryGetValue(material.name, out List<Material> list)) sources[material.name] = list = new List<Material>();
                list.Add(material);
            }
            int refreshed = 0, missing = 0, ambiguous = 0;
            var skipped = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (copy == null || !copy.name.EndsWith("_URP", StringComparison.Ordinal)) continue;
                string sourceName = copy.name.Substring(0, copy.name.Length - "_URP".Length);
                if (!sources.TryGetValue(sourceName, out List<Material> found)) { missing++; skipped.Add(copy.name + " (no source)"); continue; }
                Material source = Pick(found, copy);
                if (source == null) { ambiguous++; skipped.Add(copy.name + " (" + found.Count + " sources)"); continue; }
                CopyFrom(source, copy);
                // 서브모듈 원본은 불러오면서 다시 직렬화될 수 있으므로 전체 저장 대신 복사본만 저장한다.
                AssetDatabase.SaveAssetIfDirty(copy);
                refreshed++;
            }
            string result = $"refreshed={refreshed} missing={missing} ambiguous={ambiguous}";
            if (skipped.Count > 0) result += "\n" + string.Join("\n", skipped);
            Debug.Log(result);
            return result;
        }

        // 같은 이름이 여러 팩에 있으면 기본 텍스처가 같은 원본을 고른다. 그래도 하나로 좁혀지지 않으면 고르지 않는다.
        static Material Pick(List<Material> found, Material copy)
        {
            if (found.Count == 1) return found[0];
            Texture baseMap = copy.HasProperty("_BaseMap") ? copy.GetTexture("_BaseMap") : null;
            Material match = null;
            foreach (Material candidate in found)
            {
                Texture candidateBase = Tex(candidate, "_BaseMap") ?? Tex(candidate, "_MainTex");
                if (baseMap == null || candidateBase != baseMap) continue;
                if (match != null) return null;
                match = candidate;
            }
            return match;
        }

        static Texture Tex(Material material, string property) => material.HasProperty(property) ? material.GetTexture(property) : null;

        static void SetKeyword(Material material, string keyword, bool on)
        {
            if (on) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
        }
    }
}
