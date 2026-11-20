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

            string charset = File.ReadAllText(CharsetPath);
            fontAsset.TryAddCharacters(charset, out string missing);
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
