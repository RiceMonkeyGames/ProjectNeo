using System;
using ProjectNeo.Flow;
using ProjectNeo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FW = ProjectNeo.Editor.FantasyHudStyle;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// 부대 편성 화면(O 키)을 Game 씬에 붙인다(D-8, UI-07). UI-12와 같은 어두운 판과 굵은 금테(Box22) 안에 세 열을 둔다:
    /// 부대 목록 | 고른 부대의 부대장과 병사 | 보관 중인 영웅과 병사. 열마다 제목·수와 세로 스크롤 목록이 있고, 줄 틀
    /// (<see cref="OrganizationRowView"/>: 썸네일 칸·이름·수·부대장 표시·선택 테두리·삭제 단추)을 실행 중에 복제한다.
    /// 다시 실행하면 <c>OrganizationPanel</c> 오브젝트는 그대로 두고(다른 설정이 건 참조가 남는다) 안만 새로 만든다.
    /// </summary>
    public static class SquadOrganizationSetup
    {
        const string PanelName = "OrganizationPanel";
        const float Pad = 24f, TitleTop = 14f, TitleHeight = 36f, ColumnsTop = 60f;
        const float CloseHeight = 36f, CloseWidth = 160f, ColumnsBottom = Pad + CloseHeight + 12f;
        // 0.8배 사이드바(오른쪽 끝 220)와 20쯤 떨어지는 가장 넓은 너비(1280 캔버스 가운데, 사용자 2026-11-23).
        const float PanelWidth = 800f, PanelHeight = 560f, ColumnGap = 12f;
        const float HeaderTop = 10f, HeaderHeight = 24f, ListInset = 8f, ListTop = HeaderTop + HeaderHeight + 8f;
        const float RowHeight = 44f, Thumb = 36f, ButtonGap = 12f;
        // 세 열은 너비가 같다(사용자 2026-11-23).
        static readonly float[] ColumnShares = { 1f, 1f, 1f };
        static readonly Color DeletePlate = new Color32(0x7A, 0x24, 0x1E, 0xFF);
        static readonly Color Plate = new Color32(0x10, 0x16, 0x1A, 0xEE), BoxFill = new Color32(0x08, 0x0B, 0x0D, 0xC8);
        static readonly Color RowFill = new Color32(0x22, 0x26, 0x2A, 0xFF), RowHover = new Color32(0x3B, 0x33, 0x22, 0xFF);
        static readonly Color ThumbFill = new Color32(0x00, 0x13, 0x35, 0xFF), ThinFrame = new Color(0.85f, 0.85f, 0.85f, 0.6f);

        [MenuItem("ProjectNeo/Setup/Connect Squad Organization To Game Scene")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            GameBattleController battle = GameFacilitySetup.Find<GameBattleController>(scene);
            GamePhaseView view = GameFacilitySetup.Find<GamePhaseView>(scene);
            GameFlowHud hud = GameFacilitySetup.Find<GameFlowHud>(scene);
            var interaction = GameFacilitySetup.Find<PreparationInteraction>(scene);
            var interactionSettings = (PreparationInteractionSettings)new SerializedObject(interaction)
                .FindProperty("settings").objectReferenceValue;
            if (interactionSettings != null) EditorUtility.SetDirty(interactionSettings);

            var organization = battle.GetComponent<GameSquadOrganization>();
            if (organization == null) organization = battle.gameObject.AddComponent<GameSquadOrganization>();
            Build(hud, organization, view, interaction);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        static void Build(GameFlowHud hud, GameSquadOrganization organization, GamePhaseView view, PreparationInteraction interaction)
        {
            TMP_FontAsset noto = FW.Noto;
            Transform existing = hud.transform.Find(PanelName);
            RectTransform panel;
            if (existing != null)
            {
                panel = (RectTransform)existing;
                for (int i = panel.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(panel.GetChild(i).gameObject);
            }
            else
            {
                panel = FW.Node(hud.transform, PanelName);
                panel.gameObject.AddComponent<Image>();
            }
            panel.gameObject.SetActive(true);
            // 장비 창처럼 화면 가운데(UI-07). 780 너비는 0.8배 사이드바를 덮지 않는다.
            FW.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelWidth, PanelHeight));
            var hit = panel.GetComponent<Image>();
            hit.sprite = null; hit.color = Color.clear; hit.raycastTarget = true; // 뒤 세계를 누르지 않게 막는다
            RectTransform visual = FW.FreshVisual(panel);
            PrepHudSidebarLayout.GoldPlate(visual, Plate);

            var title = FW.NewLabel(panel, "Title", "부대 편성", noto, 26f, FW.Gold, TextAlignmentOptions.MidlineLeft);
            FW.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(Pad, -TitleTop), new Vector2(300f, TitleHeight), new Vector2(0f, 1f));

            float inner = PanelWidth - 2f * Pad - (ColumnShares.Length - 1) * ColumnGap;
            float shares = 0f;
            foreach (float share in ColumnShares) shares += share;
            float x = Pad;
            var lists = new RectTransform[3];
            var titles = new TMP_Text[3];
            var counts = new TMP_Text[3];
            string[] captions = { "부대", "부대", "보관 중" };
            for (int i = 0; i < 3; i++)
            {
                float width = Mathf.Floor(inner * ColumnShares[i] / shares);
                lists[i] = Column(panel, "Column" + (i + 1), captions[i], x, width, noto, out titles[i], out counts[i]);
                x += width + ColumnGap;
            }

            Button close = GameFacilitySetup.ButtonAt(panel, "Button_Close", "편성 확정", new Vector2(1f, 0f), Vector2.zero,
                new Vector2(CloseWidth, CloseHeight), organization.Close, out _);
            FW.PlateButton((RectTransform)close.transform, "편성 확정", noto, 17f); // Esc와 O로도 닫힌다
            FW.Place((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(-Pad, Pad), new Vector2(CloseWidth, CloseHeight), new Vector2(1f, 0f));

            // 부대 삭제: 편성 확정 왼쪽의 붉은 판 단추(사용자 2026-11-23). 고른 부대를 없앤다.
            Button delete = GameFacilitySetup.ButtonAt(panel, "Button_DeleteSquad", "부대 삭제", new Vector2(1f, 0f), Vector2.zero,
                new Vector2(CloseWidth, CloseHeight), organization.RemoveSelectedSquadFromButton, out _);
            FW.PlateButton((RectTransform)delete.transform, "부대 삭제", noto, 17f);
            var deletePlate = delete.transform.Find("Visual/SPR_Background").GetComponent<Image>();
            deletePlate.color = DeletePlate;
            FW.Place((RectTransform)delete.transform, new Vector2(1f, 0f), new Vector2(-Pad - CloseWidth - ButtonGap, Pad),
                new Vector2(CloseWidth, CloseHeight), new Vector2(1f, 0f));

            OrganizationRowView row = RowTemplate(panel, noto);
            organization.Configure(view, interaction, panel.gameObject, lists[0], lists[1], lists[2],
                counts[0], titles[1], counts[1], counts[2], row, delete);
            KeepHudVisible(hud);
            EditorUtility.SetDirty(organization);
            panel.gameObject.SetActive(false);
        }

        // 장비 창처럼 편성 창이 열려도 자원 바와 시계가 남는다(사용자 2026-11-23). 예전 설정이 걸어 둔 숨김 목록을 비운다.
        static void KeepHudVisible(GameFlowHud hud)
        {
            foreach (MonoBehaviour target in new MonoBehaviour[] { hud.GetComponent<PrepClockHud>(), hud.GetComponent<PrepTopBarHud>() })
            {
                if (target == null) continue;
                var data = new SerializedObject(target);
                data.FindProperty("hideWhileOpen").arraySize = 0;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }
        }

        // 열 상자: 어두운 바탕과 얇은 금테, 위에 제목과 수, 그 아래 세로 스크롤 목록. 스크롤 내용을 돌려준다.
        static RectTransform Column(RectTransform panel, string name, string caption, float x, float width, TMP_FontAsset noto,
            out TMP_Text title, out TMP_Text count)
        {
            var box = FW.Node(panel, name);
            box.anchorMin = new Vector2(0f, 0f); box.anchorMax = new Vector2(0f, 1f); box.pivot = new Vector2(0f, 0.5f);
            box.offsetMin = new Vector2(x, ColumnsBottom); box.offsetMax = new Vector2(x + width, -ColumnsTop);
            var fill = FW.Image(box, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", BoxFill, 4f);
            FW.Stretch(fill.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            var frame = FW.Image(box, "SPR_Frame", "SPR_FantasyWarrior_Frame_Box22_Variant01", ThinFrame, 14f);
            FW.Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);

            title = FW.NewLabel(box, "Title", caption, noto, 17f, FW.Gold, TextAlignmentOptions.MidlineLeft);
            TopRow(title.rectTransform, 0.62f, true);
            count = FW.NewLabel(box, "Count", "", noto, 15f, FW.Cream, TextAlignmentOptions.MidlineRight);
            TopRow(count.rectTransform, 0.38f, false);

            var scroll = FW.Node(box, "Scroll");
            FW.Stretch(scroll, new Vector2(ListInset, ListInset), new Vector2(-ListInset, -ListTop));
            var viewport = FW.Node(scroll, "Viewport");
            FW.Stretch(viewport, Vector2.zero, new Vector2(-10f, 0f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = FW.Node(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 가는 금색 세로 막대. 줄이 넘칠 때만 보인다.
            var bar = FW.Node(scroll, "Scrollbar");
            bar.anchorMin = new Vector2(1f, 0f); bar.anchorMax = Vector2.one; bar.pivot = new Vector2(1f, 0.5f);
            bar.offsetMin = new Vector2(-4f, 0f); bar.offsetMax = Vector2.zero;
            var track = bar.gameObject.AddComponent<Image>();
            track.color = new Color(0f, 0f, 0f, 0.35f);
            var area = FW.Node(bar, "SlidingArea");
            FW.Stretch(area, Vector2.zero, Vector2.zero);
            var handle = FW.Node(area, "Handle");
            FW.Stretch(handle, Vector2.zero, Vector2.zero);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = FW.Gold;
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle; scrollbar.targetGraphic = handleImage;

            var view = scroll.gameObject.AddComponent<ScrollRect>();
            view.viewport = viewport; view.content = content;
            view.horizontal = false; view.vertical = true;
            view.movementType = ScrollRect.MovementType.Clamped; view.scrollSensitivity = 30f;
            view.verticalScrollbar = scrollbar;
            view.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        static void TopRow(RectTransform rect, float share, bool left)
        {
            rect.anchorMin = new Vector2(left ? 0f : 1f - share, 1f); rect.anchorMax = new Vector2(left ? share : 1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left ? 12f : 0f, -HeaderTop - HeaderHeight);
            rect.offsetMax = new Vector2(left ? 0f : -12f, -HeaderTop);
        }

        // 줄 틀: 바탕(마우스를 올리면 금빛으로 밝아진다), 썸네일 칸, 이름, 부대장 표시, 수, 선택 금테. 부대장 줄도 같은 크기다.
        static OrganizationRowView RowTemplate(RectTransform panel, TMP_FontAsset noto)
        {
            var root = FW.Node(panel, "RowTemplate");
            root.sizeDelta = new Vector2(200f, RowHeight);
            var background = root.gameObject.AddComponent<Image>();
            background.sprite = FW.Sprite("SPR_HUD_FantasyWarrior_Box_White01");
            background.type = Image.Type.Sliced; background.pixelsPerUnitMultiplier = 4f;
            background.color = Color.white;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = FW.Tint(RowFill, RowHover, new Color32(0x2A, 0x24, 0x18, 0xFF));
            colors.disabledColor = RowFill;
            button.colors = colors;
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var size = root.gameObject.AddComponent<LayoutElement>();
            size.preferredHeight = size.minHeight = RowHeight;
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 8, 4, 4);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var thumb = FW.Node(root, "Thumbnail");
            var thumbSize = thumb.gameObject.AddComponent<LayoutElement>();
            thumbSize.preferredWidth = thumbSize.minWidth = thumbSize.preferredHeight = thumbSize.minHeight = Thumb;
            var thumbFill = FW.Image(thumb, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", ThumbFill, 4f);
            FW.Stretch(thumbFill.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            var thumbFrame = FW.Image(thumb, "SPR_Frame", "SPR_FantasyWarrior_Frame_Box22_Variant01", ThinFrame, 20f);
            FW.Stretch(thumbFrame.rectTransform, Vector2.zero, Vector2.zero);

            var label = FW.NewLabel(root, "Name", "이름", noto, 16f, FW.Cream, TextAlignmentOptions.MidlineLeft);
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var tag = FW.Node(root, "LeaderTag");
            var tagSize = tag.gameObject.AddComponent<LayoutElement>();
            tagSize.preferredWidth = tagSize.minWidth = 50f; tagSize.preferredHeight = tagSize.minHeight = 20f;
            var tagFill = FW.Image(tag, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", FW.Gold, 6f);
            FW.Stretch(tagFill.rectTransform, Vector2.zero, Vector2.zero);
            var tagText = FW.NewLabel(tag, "Text", "부대장", noto, 12f, FW.Ink, TextAlignmentOptions.Center);
            FW.Stretch(tagText.rectTransform, Vector2.zero, Vector2.zero);

            var count = FW.NewLabel(root, "Count", "0/0", noto, 14f, FW.Muted, TextAlignmentOptions.MidlineRight);
            var countSize = count.gameObject.AddComponent<LayoutElement>();
            countSize.preferredWidth = countSize.minWidth = 46f;

            var selection = FW.Image(root, "Selection", "SPR_FantasyWarrior_Frame_Box22_Variant01", FW.Gold, 14f);
            FW.Stretch(selection.rectTransform, Vector2.zero, Vector2.zero);
            selection.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var view = root.gameObject.AddComponent<OrganizationRowView>();
            view.Configure(button, group, thumb, thumbFrame, label, count, tag.gameObject, selection.gameObject);
            EditorUtility.SetDirty(view);
            root.gameObject.SetActive(false);
            return view;
        }
    }
}
