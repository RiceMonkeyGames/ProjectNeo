using System.Collections.Generic;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using ProjectNeo.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 퍽 창(META-09, UI-13 리메이크). 왼쪽에서 트리를 고르고, 가운데 3×3 타일에서 퍽을 고르고, 오른쪽 아래 '획득' 판 단추로 이번 런에 찍는다.
    /// 모양은 분대 편성 창(<c>SquadOrganizationSetup</c>)을 따른다(사용자 B안 2026-11-24): 1280×720 기준 800×560 금테 판, 여백 24,
    /// 칸마다 금색 머리글과 오른쪽 개수, 편성 줄과 같은 줄 바탕(고르면 금색 테두리), 가운데 남색 썸네일, 아래 이름, 얻은 퍽은 금색 딱지.
    /// 모든 퍽은 1포인트라 비용은 보이지 않는다. 썸네일은 아이콘 그림이 생기기 전까지 빈 칸이다.
    /// 판·테두리·글꼴은 <see cref="HudStyle"/>의 창 모양 값(없으면 단색 판). 규칙과 글자는 <see cref="PerkWindowModel"/>.
    /// 준비 단계 1인칭 탐색에서 K로 열고, 보상 화면에서는 'Perks' 버튼(또는 K)으로 연다.
    /// 전투 중에는 열리지 않는다(Q-06). Game.unity를 고치지 않도록 화면을 실행 중에 코드로 만든다(사용자 결정 2026-11-20).
    /// <see cref="GameBattleController"/>가 실행 중에 붙인다.
    /// </summary>
    public sealed class GamePerkWindow : MonoBehaviour
    {
        const int MaxTrees = 3;
        const int TileColumns = 3, TileRows = 3;

        // 분대 편성 창과 같은 크기·여백(SquadOrganizationSetup: 800×560, Pad 24, 제목 위 14·높이 36, 내용 위 60, 아래 단추 줄 36+12).
        const float PanelWidth = 800f, PanelHeight = 560f, Pad = 24f, TitleTop = 14f, TitleHeight = 36f, ColumnsTop = 60f;
        const float ButtonHeight = 36f, ButtonWidth = 160f, ButtonGap = 12f, ColumnsBottom = Pad + ButtonHeight + 12f;
        const float ColumnGap = 12f, TreeWidth = 150f, DetailWidth = 190f;
        // 편성 창 칸 안: 머리글(위 10, 높이 24), 목록은 안쪽 8, 위 42부터. 줄 높이 44.
        const float HeaderTop = 10f, HeaderHeight = 24f, ListInset = 8f, ListTop = HeaderTop + HeaderHeight + 8f, RowHeight = 44f;
        const float TileGap = 8f, TileThumb = 56f, DetailThumb = 72f;
        // 편성·장비 창 안쪽 칸과 같은 테두리 굵기(Box22, 1280×720 기준).
        const float BoxFramePpu = 14f, ThumbFramePpu = 20f;
        // 편성 창 판 단추와 같은 그림 배율(FW.PlateButton: 판 3, 테두리 9).
        const float PlateSpritePpu = 3f, PlateFramePpu = 9f;

        static readonly Color ShadeColor = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color FallbackPlate = new Color32(0x10, 0x16, 0x1A, 0xF2);
        static readonly Color FallbackAccent = new Color32(0xE3, 0xBB, 0x71, 0xFF);
        // 편성 창 색(SquadOrganizationSetup): 칸 바탕, 줄 바탕·고른 줄, 남색 썸네일, 옅은 테두리.
        static readonly Color BoxFill = new Color32(0x08, 0x0B, 0x0D, 0xC8);
        static readonly Color RowFill = new Color32(0x22, 0x26, 0x2A, 0xFF);
        static readonly Color RowSelected = new Color32(0x3B, 0x33, 0x22, 0xFF);
        static readonly Color RowLocked = new Color32(0x16, 0x19, 0x1C, 0xFF);
        static readonly Color ThumbFill = new Color32(0x00, 0x13, 0x35, 0xFF);
        static readonly Color ThinFrame = new Color(0.85f, 0.85f, 0.85f, 0.6f);
        static readonly Color LockedFrame = new Color(0.85f, 0.85f, 0.85f, 0.2f);
        static readonly Color PlateFill = new Color32(0x24, 0x4A, 0x66, 0xFF);
        static readonly Color PlateFrame = new Color32(0x9A, 0x74, 0x40, 0xB0);
        static readonly Color PlateOff = new Color32(0x1A, 0x22, 0x28, 0xFF);
        static readonly Color NameText = new Color32(0xF2, 0xEA, 0xD8, 0xFF);
        static readonly Color MutedText = new Color32(0xA9, 0xB8, 0xC2, 0xFF);
        static readonly Color LockedText = new Color32(0x6F, 0x7C, 0x86, 0xFF);
        static readonly Color WarningText = new Color(1f, 0.55f, 0.5f);
        static readonly Color DarkText = new Color32(0x10, 0x16, 0x1A, 0xFF);
        const float LockedAlpha = 0.55f;

        readonly List<Tile> tiles = new List<Tile>();
        readonly List<TreeRow> treeRows = new List<TreeRow>();
        GameBattleController battle;
        GamePhaseView phaseView;
        PerkTreeCatalog trees;
        HudStyle style;
        GameFlow flow;
        InputAction toggle;
        GameObject panel;
        GameObject rewardButton;
        RectTransform tileGrid;
        TMP_Text pointsLabel;
        TMP_Text statusLabel;
        TMP_Text gridTitle;
        TMP_Text gridCount;
        Image detailThumbFrame;
        TMP_Text detailTitle;
        TMP_Text detailLabel;
        TMP_Text detailInfo;
        Button acquireButton;
        Image acquireFill;
        TMP_Text acquireLabel;
        PerkWindowModel model;
        int treeIndex;

        struct TreeRow
        {
            public Button button;
            public Image fill;
            public Image frame;
            public TMP_Text name;
            public TMP_Text count;
        }

        sealed class Tile
        {
            public PerkDefinition perk;
            public Button button;
            public CanvasGroup group;
            public Image fill;
            public Image selection;
            public Image thumbFrame;
            public TMP_Text name;
            public GameObject tag;
        }

        public bool IsOpen => panel != null && panel.activeSelf;
        public bool IsRewardButtonVisible => rewardButton != null && rewardButton.activeSelf;
        public string PointsText => pointsLabel != null ? pointsLabel.text : string.Empty;
        public string StatusText => statusLabel != null ? statusLabel.text : string.Empty;
        /// <summary>오른쪽 상세의 제목과 본문을 이어 읽는다(테스트용).</summary>
        public string DetailText => detailTitle != null ? detailTitle.text + "\n" + detailLabel.text + "\n" + detailInfo.text : string.Empty;
        /// <summary>지금 고른 트리에 보이는 타일 수.</summary>
        public int RowCount => tiles.Count;
        public int TreeIndex => treeIndex;
        public PerkDefinition SelectedPerk => model?.SelectedPerk;
        public string KeyText => toggle != null ? toggle.GetBindingDisplayString() : "K";
        public PerkTreeCatalog Trees => trees;

        Color Accent => style != null ? style.WindowAccent : FallbackAccent;
        TMP_FontAsset Font => style != null ? style.WindowFont : null;

        public void Configure(GamePhaseView view, PerkTreeCatalog perkTrees, HudStyle hudStyle = null)
        {
            phaseView = view;
            trees = perkTrees;
            style = hudStyle;
            // 모양 값을 받은 뒤에 화면을 만든다(다시 부르면 새로 만든다).
            if (panel != null) Destroy(panel.transform.parent.gameObject);
            tiles.Clear(); treeRows.Clear();
            panel = null;
            Build();
        }

        void Awake()
        {
            battle = GetComponent<GameBattleController>();
            flow = GameSession.Flow;
            toggle = new InputAction("TogglePerks", InputActionType.Button, "<Keyboard>/k");
        }

        void OnEnable() => toggle?.Enable();

        void OnDisable() => toggle?.Disable();
        void OnDestroy() => toggle?.Dispose();

        RunState State => battle != null ? battle.RunLink.State : null;

        /// <summary>퍽 창을 연다. 준비 단계(1인칭 탐색)나 보상 화면에서만 열린다. 열었으면 true.</summary>
        public bool Open()
        {
            if (panel == null) Build();
            if (IsOpen || trees == null || State?.Perks == null) return false;
            if (flow.Phase == GamePhase.Preparation)
            {
                if (phaseView == null || !phaseView.IsExplorationView || !phaseView.EnterPanel()) return false;
            }
            else if (flow.Phase != GamePhase.Reward) return false;
            model = new PerkWindowModel(State, trees);
            treeIndex = 0;
            BuildTiles();
            panel.SetActive(true);
            Refresh();
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            model?.Select(null);
            panel.SetActive(false);
            if (flow.Phase == GamePhase.Preparation && phaseView != null) phaseView.ExitPanel();
        }

        /// <summary>왼쪽 트리 줄이 부른다. 타일을 그 트리로 바꾸고 고른 퍽을 비운다.</summary>
        public void SelectTree(int index)
        {
            if (model == null || trees == null || index < 0 || index >= trees.Trees.Count || trees.Trees[index] == null) return;
            treeIndex = index;
            model.Select(null);
            BuildTiles();
            Refresh();
        }

        /// <summary>타일을 고른다. 타일 버튼이 부르고 테스트도 쓴다. 고르기만 하고 얻지는 않는다.</summary>
        public void Select(PerkDefinition perk)
        {
            if (model == null) return;
            model.Select(perk);
            Refresh();
        }

        /// <summary>고른 퍽을 얻는다. 오른쪽 아래 획득 단추가 부르고 테스트도 쓴다.</summary>
        public UnlockClickResult Acquire()
        {
            if (model == null) return UnlockClickResult.Refused;
            UnlockClickResult result = model.Acquire();
            // 보상 화면의 저장은 이미 끝났으므로, 여기서 찍은 퍽도 이어 하기에 남도록 다시 저장한다.
            if (result == UnlockClickResult.Opened && flow.Phase == GamePhase.Reward) battle.SaveRun(RunSaveTrigger.RewardGranted);
            Refresh();
            return result;
        }

        /// <summary>타일 하나의 이름과 상태 글(획득함 / 필요: … / 획득 가능)을 이어 읽는다(테스트용).</summary>
        public string RowText(PerkDefinition perk)
        {
            foreach (Tile tile in tiles)
                if (tile.perk == perk) return tile.name.text + "\n" + model.StateText(perk);
            return string.Empty;
        }

        void Update()
        {
            if (panel == null) return;
            if (toggle != null && toggle.WasPressedThisFrame())
            {
                if (IsOpen) Close();
                else Open();
            }
            // Esc(준비 단계는 PreparationInteraction이 팝업 화면을 끝낸다)나 단계 전환으로 닫는다.
            if (IsOpen && flow.Phase == GamePhase.Preparation && (phaseView == null || !phaseView.IsPanelView)) panel.SetActive(false);
            if (IsOpen && flow.Phase == GamePhase.Reward && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
            if (IsOpen && flow.Phase != GamePhase.Preparation && flow.Phase != GamePhase.Reward) panel.SetActive(false);
            if (rewardButton != null)
            {
                bool show = flow.Phase == GamePhase.Reward && !IsOpen && trees != null && State?.Perks != null;
                if (rewardButton.activeSelf != show) rewardButton.SetActive(show);
            }
        }

        void Refresh()
        {
            if (model == null) return;
            pointsLabel.text = model.PointsText;
            statusLabel.text = model.Notice;
            for (int i = 0; i < treeRows.Count; i++)
            {
                PerkTreeDefinition tree = i < trees.Trees.Count ? trees.Trees[i] : null;
                treeRows[i].button.gameObject.SetActive(tree != null);
                if (tree == null) continue;
                bool on = i == treeIndex;
                int taken = TakenCount(tree);
                treeRows[i].name.text = tree.DisplayName;
                treeRows[i].count.text = $"{taken}/{tree.Perks.Count}";
                treeRows[i].count.color = taken > 0 && taken == tree.Perks.Count ? Accent : MutedText;
                treeRows[i].fill.color = on ? RowSelected : RowFill;
                SetFrame(treeRows[i].frame, on ? Accent : Color.clear);
            }
            PerkTreeDefinition current = treeIndex < trees.Trees.Count ? trees.Trees[treeIndex] : null;
            gridTitle.text = current != null ? current.DisplayName : string.Empty;
            gridCount.text = current != null ? $"{TakenCount(current)} / {current.Perks.Count}" : string.Empty;
            foreach (Tile tile in tiles)
            {
                PerkDefinition perk = tile.perk;
                bool taken = model.IsAllocated(perk);
                bool open = model.CanAllocate(perk);
                bool picked = perk == model.SelectedPerk;
                bool locked = !taken && !open;
                tile.name.text = perk.DisplayName;
                tile.name.color = picked ? Accent : NameText;
                tile.fill.color = picked ? RowSelected : locked ? RowLocked : RowFill;
                tile.group.alpha = locked && !picked ? LockedAlpha : 1f;
                SetFrame(tile.selection, picked ? Accent : Color.clear);
                SetFrame(tile.thumbFrame, taken ? Accent : locked ? LockedFrame : ThinFrame);
                tile.tag.SetActive(taken);
            }
            PerkDefinition selected = model.SelectedPerk;
            bool selectedTaken = selected != null && model.IsAllocated(selected);
            detailTitle.text = selected != null ? selected.DisplayName : "특성을 고르세요";
            detailTitle.color = selected != null ? NameText : MutedText;
            detailLabel.text = selected != null ? PerkWindowModel.Describe(selected) : "타일을 눌러 효과를 보고, 아래 획득 단추로 얻으세요.";
            detailLabel.color = MutedText;
            detailInfo.text = DetailInfo(selected);
            SetFrame(detailThumbFrame, selectedTaken ? Accent : ThinFrame);
            detailThumbFrame.transform.parent.gameObject.SetActive(selected != null);
            acquireLabel.text = model.AcquireLabel;
            bool canAcquire = selected != null && model.CanAllocate(selected);
            acquireFill.color = canAcquire ? PlateFill : PlateOff;
            acquireLabel.color = canAcquire ? NameText : selectedTaken ? Accent : LockedText;
        }

        // 상세 아래 정보 줄: 선행(아직 못 얻은 선행은 금색). 모든 퍽이 1포인트라 비용 줄은 없다.
        string DetailInfo(PerkDefinition perk)
        {
            if (perk == null || perk.Prerequisites == null || perk.Prerequisites.Count == 0) return string.Empty;
            var names = new List<string>();
            foreach (PerkDefinition need in perk.Prerequisites)
                if (need != null) names.Add(model.IsAllocated(need) ? need.DisplayName : $"<color=#{ColorUtility.ToHtmlStringRGB(Accent)}>{need.DisplayName}</color>");
            return $"<color=#{ColorUtility.ToHtmlStringRGB(MutedText)}>선행</color>  {string.Join(", ", names)}";
        }

        int TakenCount(PerkTreeDefinition tree)
        {
            int count = 0;
            foreach (PerkDefinition perk in tree.Perks) if (perk != null && model.IsAllocated(perk)) count++;
            return count;
        }

        void BuildTiles()
        {
            foreach (Tile tile in tiles) if (tile.button != null) Destroy(tile.button.gameObject);
            tiles.Clear();
            PerkTreeDefinition tree = treeIndex < trees.Trees.Count ? trees.Trees[treeIndex] : null;
            if (tree == null) return;
            // 트리 하나에 퍽이 아홉 개여도 3열 × 3줄로 칸 안에 들어간다.
            foreach (PerkDefinition perk in tree.Perks)
            {
                if (perk == null) continue;
                PerkDefinition target = perk;
                Tile tile = MakeTile(tileGrid, "Tile_" + perk.Id, () => Select(target));
                tile.perk = perk;
                tiles.Add(tile);
            }
        }

        // 화면을 코드로 만든다: 화면을 어둡게 덮고, 가운데 800×560 금테 판 안에 제목·포인트,
        // 아래로 왼쪽 트리 칸·가운데 타일 칸·오른쪽 상세 칸, 맨 아래 알림과 획득·닫기 판 단추.
        void Build()
        {
            var root = new GameObject("PerkWindowCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            panel = new GameObject("PerkWindow", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            Stretch((RectTransform)panel.transform, Vector2.zero, Vector2.zero);
            panel.GetComponent<Image>().color = ShadeColor;

            RectTransform plate = Node(panel.transform, "Plate");
            Place(plate, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelWidth, PanelHeight), new Vector2(0.5f, 0.5f));
            GoldPlate(plate);

            // 제목 줄: 왼쪽 '특성', 오른쪽 포인트(편성 창처럼 제목 아래 선은 없다).
            TMP_Text title = Label(plate, "Title", "특성", 26f, FontStyles.Normal);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(Pad, -TitleTop), new Vector2(140f, TitleHeight), new Vector2(0f, 1f));
            title.color = Accent;
            pointsLabel = Label(plate, "Points", string.Empty, 16f, FontStyles.Normal);
            pointsLabel.alignment = TextAlignmentOptions.MidlineRight;
            Place(pointsLabel.rectTransform, new Vector2(1f, 1f), new Vector2(-Pad, -TitleTop), new Vector2(420f, TitleHeight), new Vector2(1f, 1f));

            // 왼쪽 트리 칸: 편성 창 부대 목록처럼 44 높이 줄.
            RectTransform treeBox = Column(plate, "Trees", Pad, TreeWidth);
            Box(treeBox, "특성 트리", out _, out _);
            for (int i = 0; i < MaxTrees; i++)
            {
                int index = i;
                treeRows.Add(MakeTreeRow(treeBox, "Tree_" + (i + 1), i, () => SelectTree(index)));
            }

            // 가운데 타일 칸: 남는 폭과 높이를 3×3으로 나눈다.
            float gridLeft = Pad + TreeWidth + ColumnGap;
            float gridWidth = PanelWidth - 2f * Pad - TreeWidth - DetailWidth - 2f * ColumnGap;
            float gridHeight = PanelHeight - ColumnsTop - ColumnsBottom;
            RectTransform gridBox = Column(plate, "Tiles", gridLeft, gridWidth);
            Box(gridBox, string.Empty, out gridTitle, out gridCount);
            tileGrid = Node(gridBox, "Grid");
            Stretch(tileGrid, new Vector2(ListInset, ListInset), new Vector2(-ListInset, -ListTop));
            var grid = tileGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = TileColumns;
            float innerWidth = gridWidth - 2f * ListInset, innerHeight = gridHeight - ListTop - ListInset;
            grid.cellSize = new Vector2(Mathf.Floor((innerWidth - TileGap * (TileColumns - 1)) / TileColumns),
                Mathf.Floor((innerHeight - TileGap * (TileRows - 1)) / TileRows));
            grid.spacing = new Vector2(TileGap, TileGap);
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;

            // 오른쪽 상세 칸: 머리글 '상세', 큰 썸네일, 이름, 효과, 선, 선행.
            RectTransform detail = Column(plate, "Detail", PanelWidth - Pad - DetailWidth, DetailWidth);
            Box(detail, "상세", out _, out _);
            RectTransform body = Node(detail, "Body");
            Stretch(body, new Vector2(12f, ListInset), new Vector2(-12f, -ListTop - 2f));
            var bodyLayout = body.gameObject.AddComponent<VerticalLayoutGroup>();
            bodyLayout.spacing = 6f;
            bodyLayout.childControlWidth = true; bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = true; bodyLayout.childForceExpandHeight = false;
            RectTransform thumbRow = Node(body, "ThumbRow");
            thumbRow.gameObject.AddComponent<LayoutElement>().preferredHeight = DetailThumb + 6f;
            RectTransform thumb = Node(thumbRow, "Thumbnail");
            Place(thumb, new Vector2(0f, 1f), Vector2.zero, new Vector2(DetailThumb, DetailThumb), new Vector2(0f, 1f));
            detailThumbFrame = Thumbnail(thumb);
            detailTitle = LayoutLabel(body, "DetailTitle", 19f, 28f, FontStyles.Normal);
            detailTitle.overflowMode = TextOverflowModes.Ellipsis;
            detailLabel = LayoutLabel(body, "DetailBody", 15f, 66f, FontStyles.Normal);
            detailLabel.alignment = TextAlignmentOptions.TopLeft;
            detailLabel.textWrappingMode = TextWrappingModes.Normal;
            RectTransform line = Node(body, "Divider");
            line.gameObject.AddComponent<LayoutElement>().preferredHeight = 8f;
            DividerLine(line, 0.3f);
            detailInfo = LayoutLabel(body, "DetailInfo", 14f, 48f, FontStyles.Normal);
            detailInfo.alignment = TextAlignmentOptions.TopLeft;
            detailInfo.textWrappingMode = TextWrappingModes.Normal;
            detailInfo.richText = true;

            // 맨 아래 줄(획득은 못 얻을 때도 눌려 알림에 이유를 띄운다): 왼쪽 알림, 오른쪽 '획득'·'닫기' 판 단추(K로도 닫힌다)(편성 창의 '부대 삭제'·'편성 확정' 자리).
            RectTransform close = PlateButton(plate, "Button_Close", "닫기", Close, out _, out _);
            Place(close, new Vector2(1f, 0f), new Vector2(-Pad, Pad), new Vector2(ButtonWidth, ButtonHeight), new Vector2(1f, 0f));
            RectTransform acquire = PlateButton(plate, "Button_Acquire", "획득", () => Acquire(), out acquireFill, out acquireLabel);
            Place(acquire, new Vector2(1f, 0f), new Vector2(-Pad - ButtonWidth - ButtonGap, Pad), new Vector2(ButtonWidth, ButtonHeight), new Vector2(1f, 0f));
            acquireButton = acquire.GetComponent<Button>();
            statusLabel = Label(plate, "Status", string.Empty, 14f, FontStyles.Normal);
            statusLabel.color = WarningText;
            statusLabel.textWrappingMode = TextWrappingModes.Normal;
            statusLabel.overflowMode = TextOverflowModes.Ellipsis;
            float statusWidth = PanelWidth - 2f * Pad - 2f * ButtonWidth - 2f * ButtonGap;
            Place(statusLabel.rectTransform, new Vector2(0f, 0f), new Vector2(Pad, Pad), new Vector2(statusWidth, ButtonHeight), new Vector2(0f, 0f));
            panel.SetActive(false);

            rewardButton = MakeSolidButton(root.transform, "Button_OpenPerks", "특성 (K)", 44f, () => Open(), out _).gameObject;
            var rect = (RectTransform)rewardButton.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-24f, 24f);
            rect.sizeDelta = new Vector2(180f, 44f);
            rewardButton.SetActive(false);
        }

        // 제목 아래부터 아래 단추 줄 위까지 세로로 긴 칸.
        static RectTransform Column(RectTransform plate, string name, float left, float width)
        {
            RectTransform rect = Node(plate, name);
            rect.anchorMin = new Vector2(0f, 0f); rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = new Vector2(left, ColumnsBottom); rect.offsetMax = new Vector2(left + width, -ColumnsTop);
            return rect;
        }

        // Esc 메뉴·편성 창과 같은 어두운 판에 굵은 금테. 모양 값이 없으면 단색 판. 판 그림이 반투명이라 뒤 화면이 비치지 않게 바닥을 먼저 깐다.
        void GoldPlate(RectTransform parent)
        {
            Image floor = Node(parent, "Floor").gameObject.AddComponent<Image>();
            Stretch(floor.rectTransform, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            Color baseColor = style != null ? style.WindowPlate : FallbackPlate;
            floor.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
            floor.raycastTarget = true; // 판 빈 곳을 눌러도 뒤로 새지 않는다
            Image plate = Node(parent, "SPR_Background").gameObject.AddComponent<Image>();
            Stretch(plate.rectTransform, Vector2.zero, Vector2.zero);
            plate.color = baseColor;
            Skin(plate, style != null ? style.WindowPlateSprite : null, style != null ? style.WindowPlatePixelsPerUnit : 1f);
            if (style == null || style.WindowFrameSprite == null)
            {
                SetFallbackEdge(plate.gameObject, Accent, 2f);
                return;
            }
            Image frame = Node(parent, "SPR_Frame").gameObject.AddComponent<Image>();
            Stretch(frame.rectTransform, new Vector2(-2f, -2f), new Vector2(2f, 2f));
            frame.color = style.WindowFrame;
            Skin(frame, style.WindowFrameSprite, style.WindowFramePixelsPerUnit);
        }

        // 편성 창 칸: 어두운 바탕에 옅은 Box22 테두리, 위에 금색 머리글과 오른쪽 개수.
        void Box(RectTransform box, string caption, out TMP_Text title, out TMP_Text count)
        {
            Image fill = Node(box, "SPR_Background").gameObject.AddComponent<Image>();
            Stretch(fill.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            fill.color = BoxFill; fill.raycastTarget = false;
            Image frame = MakeFrame(box, BoxFramePpu);
            SetFrame(frame, ThinFrame);
            title = Label(box, "Title", caption, 17f, FontStyles.Normal);
            title.color = Accent;
            Header(title.rectTransform);
            count = Label(box, "Count", string.Empty, 15f, FontStyles.Normal);
            count.alignment = TextAlignmentOptions.MidlineRight;
            Header(count.rectTransform);
        }

        static void Header(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(10f, -HeaderTop - HeaderHeight); rect.offsetMax = new Vector2(-10f, -HeaderTop);
        }

        // 테두리 그림. 창 테두리 스프라이트(Box22)를 쓰고, 없으면 바탕 없는 단색 선 그림으로 대신한다.
        Image MakeFrame(Transform parent, float pixelsPerUnit)
        {
            Image frame = Node(parent, "SPR_Frame").gameObject.AddComponent<Image>();
            Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);
            frame.raycastTarget = false;
            Sprite sprite = style != null ? style.WindowFrameSprite : null;
            if (sprite != null)
            {
                Skin(frame, sprite, pixelsPerUnit);
                return frame;
            }
            frame.color = new Color(0f, 0f, 0f, 0f);
            SetFallbackEdge(frame.gameObject, ThinFrame, 1f);
            return frame;
        }

        static void SetFrame(Image frame, Color color)
        {
            if (frame.sprite != null) { frame.color = color; return; }
            var outline = frame.GetComponent<Outline>();
            if (outline != null) outline.effectColor = color;
        }

        static void SetFallbackEdge(GameObject go, Color color, float width)
        {
            var outline = go.GetComponent<Outline>();
            if (outline == null) outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(width, -width);
        }

        static void Skin(Image image, Sprite sprite, float pixelsPerUnit)
        {
            image.raycastTarget = false;
            if (sprite == null) return;
            image.sprite = sprite;
            image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = pixelsPerUnit;
        }

        void DividerLine(RectTransform holder, float alpha)
        {
            Image line = Node(holder, "Line").gameObject.AddComponent<Image>();
            line.raycastTarget = false;
            line.color = new Color(Accent.r, Accent.g, Accent.b, alpha);
            var rect = line.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f); rect.anchorMax = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(0f, 1.5f);
        }

        // 남색 썸네일 칸(편성 창 줄의 썸네일과 같다). 테두리를 돌려준다. 아이콘 그림이 생기기 전까지 빈 칸.
        Image Thumbnail(RectTransform thumb)
        {
            Image fill = Node(thumb, "SPR_Background").gameObject.AddComponent<Image>();
            Stretch(fill.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            fill.color = ThumbFill; fill.raycastTarget = false;
            Image frame = MakeFrame(thumb, ThumbFramePpu);
            SetFrame(frame, ThinFrame);
            return frame;
        }

        // 편성 줄 바탕(+고르면 금색 테두리)을 깐 단추. 마우스를 올리면 옅게 밝아진다.
        GameObject RowButton(Transform parent, string name, UnityEngine.Events.UnityAction onClick, out Button button, out Image fill, out Image selection)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var hit = go.GetComponent<Image>(); hit.color = new Color(1f, 1f, 1f, 0f);
            button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            fill = Node(go.transform, "SPR_Background").gameObject.AddComponent<Image>();
            Stretch(fill.rectTransform, Vector2.zero, Vector2.zero);
            fill.raycastTarget = false; fill.color = RowFill;
            HoverOverlay(go.transform, button);
            selection = MakeFrame(go.transform, BoxFramePpu);
            selection.name = "Selection";
            SetFrame(selection, Color.clear);
            return go;
        }

        // 왼쪽 트리 한 줄: 이름과 '얻은 수/전체'. 고르면 금빛 바탕과 금색 테두리.
        TreeRow MakeTreeRow(RectTransform box, string name, int index, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = RowButton(box, name, onClick, out Button button, out Image fill, out Image selection);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(0.5f, 1f);
            float top = ListTop + index * (RowHeight + 6f);
            rect.offsetMin = new Vector2(ListInset, -top - RowHeight); rect.offsetMax = new Vector2(-ListInset, -top);
            TMP_Text label = Label(go.transform, "Name", string.Empty, 16f, FontStyles.Normal);
            Stretch(label.rectTransform, new Vector2(12f, 0f), new Vector2(-44f, 0f));
            label.overflowMode = TextOverflowModes.Ellipsis;
            TMP_Text count = Label(go.transform, "Count", string.Empty, 14f, FontStyles.Normal);
            count.alignment = TextAlignmentOptions.MidlineRight;
            Stretch(count.rectTransform, new Vector2(12f, 0f), new Vector2(-10f, 0f));
            return new TreeRow { button = button, fill = fill, frame = selection, name = label, count = count };
        }

        // 특성 타일(B안): 가운데 남색 썸네일, 아래 이름, 오른쪽 위 금색 '획득' 딱지. 잠긴 타일은 흐리다.
        Tile MakeTile(Transform parent, string name, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = RowButton(parent, name, onClick, out Button button, out Image fill, out Image selection);
            var group = go.AddComponent<CanvasGroup>();
            RectTransform thumb = Node(go.transform, "Thumbnail");
            Place(thumb, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(TileThumb, TileThumb), new Vector2(0.5f, 0.5f));
            Image thumbFrame = Thumbnail(thumb);
            TMP_Text title = Label(go.transform, "Name", string.Empty, 15f, FontStyles.Normal);
            title.alignment = TextAlignmentOptions.Center;
            title.overflowMode = TextOverflowModes.Ellipsis;
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0f); titleRect.anchorMax = new Vector2(1f, 0f); titleRect.pivot = new Vector2(0.5f, 0f);
            titleRect.offsetMin = new Vector2(6f, 8f); titleRect.offsetMax = new Vector2(-6f, 30f);
            GameObject tag = Tag(go.transform, "획득");
            return new Tile { button = button, group = group, fill = fill, selection = selection, thumbFrame = thumbFrame, name = title, tag = tag };
        }

        // 편성 창 '부대장' 표시와 같은 금색 딱지(오른쪽 위).
        GameObject Tag(Transform parent, string text)
        {
            Image fill = Node(parent, "Tag").gameObject.AddComponent<Image>();
            fill.raycastTarget = false; fill.color = Accent;
            Place(fill.rectTransform, new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(40f, 20f), new Vector2(1f, 1f));
            TMP_Text label = Label(fill.transform, "Text", text, 12f, FontStyles.Normal);
            label.color = DarkText; label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
            fill.gameObject.SetActive(false);
            return fill.gameObject;
        }

        // 마우스를 올리면 옅게 밝아지는 막. 버튼 색 바꿈을 이 막에만 걸어, 고른 상태 글자색은 건드리지 않는다.
        static void HoverOverlay(Transform parent, Button button)
        {
            Image hover = Node(parent, "Hover").gameObject.AddComponent<Image>();
            Stretch(hover.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            hover.raycastTarget = false;
            hover.color = new Color(1f, 1f, 1f, 0.06f);
            button.targetGraphic = hover;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(1f, 1f, 1f, 0.6f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        // 편성 창 아래 판 단추('편성 확정', FW.PlateButton)와 같은 모양: 결 있는 푸른 판(Box_Background01)에 옅은 청동 테두리(Frame_Box12), 크림 글자.
        RectTransform PlateButton(Transform parent, string name, string text, UnityEngine.Events.UnityAction onClick, out Image fill, out TMP_Text label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var hit = go.GetComponent<Image>(); hit.color = new Color(1f, 1f, 1f, 0f);
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            fill = Node(go.transform, "SPR_Background").gameObject.AddComponent<Image>();
            Stretch(fill.rectTransform, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            fill.color = PlateFill;
            Skin(fill, style != null ? style.WindowButtonPlateSprite : null, PlateSpritePpu);
            // 편성 창 판 단추처럼 판 그림에 색 바꿈을 건다(평소 조금 어둡고, 올리면 밝다).
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            Sprite frameSprite = style != null ? style.WindowButtonFrameSprite : null;
            Image frame;
            if (frameSprite != null)
            {
                frame = Node(go.transform, "SPR_Frame").gameObject.AddComponent<Image>();
                Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);
                Skin(frame, frameSprite, PlateFramePpu);
            }
            else frame = MakeFrame(go.transform, BoxFramePpu);
            SetFrame(frame, PlateFrame);
            label = Label(go.transform, "Label", text, 17f, FontStyles.Normal);
            label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
            return (RectTransform)go.transform;
        }

        // 금색 바탕 버튼(보상 화면의 여는 버튼).
        Button MakeSolidButton(Transform parent, string name, string text, float height, UnityEngine.Events.UnityAction onClick, out Image fill)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            fill = go.GetComponent<Image>(); fill.color = Accent;
            var button = go.GetComponent<Button>(); button.targetGraphic = fill;
            var colors = button.colors; colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f); colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            button.colors = colors;
            button.onClick.AddListener(onClick);
            TMP_Text label = Label(go.transform, "Label", text, 19f, FontStyles.Normal);
            label.color = DarkText; label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
            return button;
        }

        static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = min; rect.offsetMax = max;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        TMP_Text Label(Transform parent, string name, string text, float size, FontStyles fontStyle)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            if (Font != null) label.font = Font;
            label.text = text; label.fontSize = size; label.fontStyle = fontStyle;
            label.color = NameText; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }

        TMP_Text LayoutLabel(Transform parent, string name, float size, float height, FontStyles fontStyle)
        {
            TMP_Text label = Label(parent, name, string.Empty, size, fontStyle);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return label;
        }
    }
}
