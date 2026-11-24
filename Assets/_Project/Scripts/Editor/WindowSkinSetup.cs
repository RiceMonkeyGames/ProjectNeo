using ProjectNeo.UI;
using UnityEditor;
using UnityEngine;
using FW = ProjectNeo.Editor.FantasyHudStyle;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// 실행 중에 코드로 만드는 창(Esc 메뉴 등)의 모양 값을 HudStyle 에셋에 채운다(UI-07). 사이드바와 같은 어두운 판
    /// (Box_White01)과 굵은 금테(Box22), 한글 글꼴(Noto), 판 단추 그림(Box_Background01·Frame_Box12, UI-13). 다시 실행해도 된다.
    /// </summary>
    public static class WindowSkinSetup
    {
        [MenuItem("ProjectNeo/Setup/Window Skin (UI-07)")]
        public static void Apply()
        {
            var style = AssetDatabase.LoadAssetAtPath<HudStyle>(PrepHudSetup.StylePath);
            if (style == null)
            {
                Debug.LogError("HudStyle asset is missing: " + PrepHudSetup.StylePath);
                return;
            }
            var so = new SerializedObject(style);
            so.FindProperty("windowPlateSprite").objectReferenceValue = FW.Sprite("SPR_HUD_FantasyWarrior_Box_White01");
            so.FindProperty("windowFrameSprite").objectReferenceValue = FW.Sprite("SPR_FantasyWarrior_Frame_Box22_Variant01");
            so.FindProperty("windowFont").objectReferenceValue = FW.Noto;
            // 판 단추(UI-13): 편성 창 FW.PlateButton과 같은 판·테두리 그림.
            so.FindProperty("windowButtonPlateSprite").objectReferenceValue = FW.Sprite("SPR_HUD_FantasyWarrior_Box_Background01");
            so.FindProperty("windowButtonFrameSprite").objectReferenceValue = FW.Sprite("SPR_FantasyWarrior_Frame_Box12");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssets();
            Debug.Log("Window skin set on " + PrepHudSetup.StylePath);
        }
    }
}
