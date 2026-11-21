using System;
using ProjectNeo.UI;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// HudStyle 에셋에 Fantasy Warrior HUD 팩 모양을 넣는다(UI-06). 팩 HUD 프리팹이 쓰는 조합을 따른다:
    /// 짙은 청회색 양피지 바탕(HUD_Box_Small_Parchment02) 위 금색 테두리(Frame_Box21_Variant01),
    /// 게이지는 마름모 끝 모양(Bar_Horizontal03_Mask)으로 자른 채움(Bar_Horizontal05).
    /// 실행 뒤 ProjectNeo/Setup/Preparation HUD (UI-06)을 다시 실행해야 씬에 반영된다.
    /// </summary>
    public static class HudStyleLook
    {
        const string Sprites = "Assets/ThirdParty/Synty/InterfaceFantasyWarriorHUD/Sprites/";
        public const string PanelPath = Sprites + "HUD/SPR_HUD_FantasyWarrior_Box_Small_Parchment02.png";
        public const string FramePath = Sprites + "FantasyWarrior/SPR_FantasyWarrior_Frame_Box21_Variant01.png";
        public const string GaugePath = Sprites + "FantasyWarrior/SPR_FantasyWarrior_Bar_Horizontal03_Mask.png";
        public const string GaugeFillPath = Sprites + "FantasyWarrior/SPR_FantasyWarrior_Bar_Horizontal05.png";

        [MenuItem("ProjectNeo/Setup/HUD Style - Fantasy Warrior Look (UI-06)")]
        public static void ApplyToAsset()
        {
            var style = AssetDatabase.LoadAssetAtPath<HudStyle>(PrepHudSetup.StylePath);
            if (style == null) throw new InvalidOperationException("Run ProjectNeo/Setup/Preparation HUD (UI-06) first.");
            ApplyFantasyWarrior(style);
        }

        public static void ApplyFantasyWarrior(HudStyle style)
        {
            var so = new SerializedObject(style);
            SetSprite(so, "panelSprite", PanelPath);
            SetSprite(so, "frameSprite", FramePath);
            SetSprite(so, "gaugeSprite", GaugePath);
            SetSprite(so, "gaugeFillSprite", GaugeFillPath);
            // 팩 HUD 프리팹의 바탕색(짙은 청회색)과 게이지 바탕색. 선택된 카드는 따뜻한 갈색 바탕과 밝은 테두리.
            so.FindProperty("panel").colorValue = new Color(0.13f, 0.16f, 0.17f, 0.9f);
            so.FindProperty("panelSelected").colorValue = new Color(0.3f, 0.22f, 0.1f, 0.94f);
            so.FindProperty("frame").colorValue = new Color(0.78f, 0.78f, 0.78f, 1f);
            so.FindProperty("frameSelected").colorValue = Color.white;
            so.FindProperty("gaugeTrack").colorValue = new Color(0.07f, 0.08f, 0.08f, 0.85f);
            // 9-슬라이스 테두리(원본 50~140px)를 1280×720 HUD 크기에 맞게 줄인다.
            so.FindProperty("panelPixelsPerUnit").floatValue = 2f;
            so.FindProperty("framePixelsPerUnit").floatValue = 8f;
            so.FindProperty("gaugePixelsPerUnit").floatValue = 6f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssets();
        }

        static void SetSprite(SerializedObject so, string field, string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new InvalidOperationException("Sprite not found: " + path + " (run git submodule update --init).");
            so.FindProperty(field).objectReferenceValue = sprite;
        }
    }
}
