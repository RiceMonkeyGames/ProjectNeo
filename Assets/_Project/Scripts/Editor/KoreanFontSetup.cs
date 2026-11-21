using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ProjectNeo.Editor
{
    /// <summary>UI-04: create the Noto Sans KR TMP font and register it as the global TMP fallback.</summary>
    public static class KoreanFontSetup
    {
        public const string SourceFontPath = "Assets/_Project/Art/Fonts/NotoSansKR-Regular.otf";
        public const string CharsetPath = "Assets/_Project/Art/Fonts/KoreanCharset.txt";
        public const string FontAssetPath = "Assets/_Project/Art/Fonts/NotoSansKR SDF.asset";

        [MenuItem("ProjectNeo/Setup/Korean Font")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for an idle Editor before creating the Korean font.");
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null) throw new InvalidOperationException("Missing " + SourceFontPath);

            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(source, 36, 4, GlyphRenderMode.SDFAA, 2048, 2048,
                    AtlasPopulationMode.Dynamic, true);
                fontAsset.name = "NotoSansKR SDF";
                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
                Texture2D atlas = fontAsset.atlasTextures[0];
                atlas.name = "NotoSansKR SDF Atlas";
                AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                Material material = fontAsset.material;
                material.name = "NotoSansKR SDF Material";
                AssetDatabase.AddObjectToAsset(material, fontAsset);
            }

            // Fill the atlas while dynamic, then freeze it (UI-05): a dynamic atlas keeps growing in Play Mode, which
            // dirties the asset and broke Hangul once its glyph table and atlas got out of sync. KoreanCharset.txt
            // (ASCII + KS X 1001 Hangul) covers every Hangul character the UI uses; add characters there and re-run.
            string charset = File.ReadAllText(CharsetPath);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.ClearFontAssetData(true);
            fontAsset.TryAddCharacters(charset, out string missing);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            // TMP fills the material's _ScaleRatioA/B/C the first time it draws text with it, which left the saved asset
            // showing as changed after every test or Play session (UI-10). Compute them now so the saved values match.
            ShaderUtilities.UpdateShaderRatios(fontAsset.material);
            EditorUtility.SetDirty(fontAsset.material);
            EditorUtility.SetDirty(fontAsset);

            var fallbacks = TMP_Settings.fallbackFontAssets;
            if (!fallbacks.Contains(fontAsset)) fallbacks.Add(fontAsset);
            EditorUtility.SetDirty(TMP_Settings.instance);
            AssetDatabase.SaveAssets();
            return "glyphs=" + fontAsset.characterTable.Count + " atlases=" + fontAsset.atlasTextures.Length
                + " missing=" + (string.IsNullOrEmpty(missing) ? 0 : missing.Length)
                + " fallbacks=" + fallbacks.Count;
        }
    }
}
