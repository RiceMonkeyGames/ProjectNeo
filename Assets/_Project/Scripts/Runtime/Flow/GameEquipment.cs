using System.Collections.Generic;
using System.Text;
using ProjectNeo.Economy;
using ProjectNeo.Run;
using ProjectNeo.Skills;
using ProjectNeo.Stats;
using ProjectNeo.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// Game 씬 준비 단계의 장비 화면(D-7). 1인칭 탐색 중 장비 키(기본 I)로 열고 Esc나 닫기 버튼으로 닫는다.
    /// 보관함의 장비를 클릭하면 런 플레이어에게 끼우고, 착용 중인 장비를 클릭하면 벗긴다. 런 플레이어는 씬 플레이어와
    /// 같은 캐릭터를 쓰므로 스탯이 씬에 바로 반영된다. 고용한 영웅이 있으면 착용 목록 맨 위 줄로 대상(플레이어·영웅)을 바꾼다(D-3, Q-48).
    /// 고용 영웅도 씬 유닛과 같은 캐릭터를 써서 바로 반영된다.
    /// 오른쪽 목록 맨 위 줄로 장비 보기와 스킬 보기(COMBAT-06)를 바꾼다. 스킬 보기에서 플레이어는 배운 스킬 중 3개를 장착하고,
    /// 영웅은 등급으로 열린 고정 스킬을 보고 자동 사용을 켜고 끈다.
    /// <see cref="GameBattleController"/>와 같은 오브젝트에 붙어 그 런 연결을 쓴다.
    /// </summary>
    [RequireComponent(typeof(GameBattleController))]
    public sealed class GameEquipment : MonoBehaviour
    {
        static readonly StatType[] ShownStats = { StatType.MaxHealth, StatType.Attack, StatType.Defense, StatType.AttackSpeed, StatType.MoveSpeed };

        [SerializeField] GamePhaseView phaseView;
        [SerializeField] PreparationInteraction interaction;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text status;
        [SerializeField] TMP_Text body;
        [SerializeField] RectTransform equippedList;
        [SerializeField] RectTransform inventoryList;
        [SerializeField] Button rowTemplate;
        // UI-07 B안(인물 칸 + 아이콘 격자). 연결돼 있으면 장비 보기는 이 화면을 쓰고, 스킬 보기만 두 목록을 쓴다.
        [SerializeField] GameObject gearView;
        [SerializeField] GameObject listView;
        [SerializeField] EquipmentSlotView slotTemplate;
        [SerializeField] RectTransform wornSlots;
        // 장비 칸 자리(EquipmentSlot 값 순서). 있으면 칸을 각 자리에 꽉 채워 놓는다(UI-07 초상화 둘레 배치).
        [SerializeField] RectTransform[] wornAnchors = new RectTransform[0];
        readonly List<EquipmentSlotView> wornViews = new List<EquipmentSlotView>();
        [SerializeField] RectTransform storageGrid;
        [SerializeField] TMP_Text stats;
        [SerializeField] TMP_Text detail;
        // 마우스를 올린 칸 옆에 뜨는 툴팁(UI-07, 사용자 2026-11-23: 고정 정보 칸 대신). detail은 그 안의 글이다.
        [SerializeField] RectTransform tooltip;
        [SerializeField] Button gearTab, skillTab, previousHero, nextHero;
        [SerializeField] TMP_Text heroName;
        [SerializeField, Min(1)] int storageColumns = 7;
        [SerializeField, Min(0)] int minStorageCells = 14;
        // 빈 장비 칸에 흐리게 띄우는 종류 그림. EquipmentSlot 값 순서(무기, 보조무기, 방어구, 장신구).
        [SerializeField] Sprite[] slotPlaceholders = new Sprite[0];
        // UI-07 스킬 탭(사용자 스케치 2026-11-23): 둥근 칸 3개를 삼각형으로, 아래에 고른 칸 설명, 위 오른쪽에 자동 사용 버튼.
        // 연결돼 있으면 스킬 보기는 두 목록 대신 이 화면을 쓴다.
        [SerializeField] GameObject skillPanel;
        [SerializeField] SkillCircleView[] skillCircles = new SkillCircleView[0];
        [SerializeField] TMP_Text skillDetail;
        // 자동 사용 상태 색 판(사용자 2026-11-23: 켜지면 초록 판, 꺼지면 회색 판, 누르면 바뀐다). 영웅만 보인다.
        [SerializeField] Button autoPlate;
        // UI-13(사용자 2026-11-24): 장비와 스킬을 탭 없이 한 창에 나란히 보인다(O·K 창과 같은 800×560).
        [SerializeField] bool sideBySide;
        int selectedSkill;
        readonly List<Button> rows = new List<Button>();
        readonly List<EquipmentSlotView> slots = new List<EquipmentSlotView>();
        ItemInstance hoveredItem;
        bool hoveredWorn;

        static readonly string Up = "#8FD16A", Down = "#E57A6A", Dim = "#9C927C";
        GameBattleController battle;
        GameFlow flow;
        TMP_Text equippedHeader, inventoryHeader;
        string equippedHeaderText, inventoryHeaderText;

        public bool IsOpen { get; private set; }
        public string Message { get; private set; } = string.Empty;
        public string StatusText => status != null ? status.text : string.Empty;
        public string BodyText => body != null ? body.text : string.Empty;
        public int EquippedRows => equippedList != null ? ActiveChildren(equippedList) : 0;
        public int InventoryRows => inventoryList != null ? ActiveChildren(inventoryList) : 0;
        /// <summary>스킬 보기인지(아니면 장비 보기).</summary>
        public bool SkillView { get; private set; }
        /// <summary>왼쪽·오른쪽 목록의 줄 글자(테스트용).</summary>
        public List<string> EquippedRowTexts => RowTexts(equippedList);
        public List<string> InventoryRowTexts => RowTexts(inventoryList);
        /// <summary>B안 화면이 연결돼 있는지.</summary>
        public bool PaperDoll => gearView != null && slotTemplate != null && wornSlots != null && storageGrid != null;
        /// <summary>B안 장비 칸·보관함 격자에 든 아이템 이름(빈 칸 제외, 테스트용).</summary>
        public List<string> WornSlotNames
        {
            get
            {
                var names = new List<string>();
                foreach (EquipmentSlotView view in wornViews)
                    if (view != null && !view.IsEmpty) names.Add(view.ItemName);
                return names;
            }
        }
        public List<string> StorageSlotNames => SlotNames(storageGrid);
        /// <summary>B안 장비 칸 수(빈 칸 포함).</summary>
        public int WornSlotCount => wornViews.Count;
        public string StatsText => stats != null ? stats.text : string.Empty;
        public string DetailText => detail != null ? detail.text : string.Empty;

        /// <summary>스킬 탭 삼각형 화면이 연결돼 있는지.</summary>
        public bool SkillTriangle => skillPanel != null && skillCircles != null && skillCircles.Length == SkillSlotRules.MaxSlots && skillDetail != null;
        /// <summary>장비와 스킬을 한 창에 나란히 보이는지(탭 없음).</summary>
        public bool SideBySide => sideBySide && PaperDoll && SkillTriangle;
        /// <summary>스킬 칸이 지금 보이는지(나란히 보기이거나 스킬 보기).</summary>
        public bool SkillsShown => SideBySide || SkillView;
        /// <summary>스킬 탭에서 고른 칸(0부터).</summary>
        public int SelectedSkillSlot => selectedSkill;
        /// <summary>스킬 탭 설명 칸 글(테스트용).</summary>
        public string SkillDetailText => skillDetail != null ? skillDetail.text : string.Empty;
        /// <summary>자동 사용 판이 보이는지와, 판이 켜짐(초록)으로 칠해졌는지(테스트용).</summary>
        public bool AutoPlateShown => autoPlate != null && autoPlate.gameObject.activeSelf;
        public bool AutoPlateShowsOn { get; private set; }
        /// <summary>스킬 칸(테스트용). 잠김 여부와 이름을 본다.</summary>
        public IReadOnlyList<SkillCircleView> SkillCircles => skillCircles;

        /// <summary>UI-07 스킬 탭 화면을 연결한다(에디터 설치 코드). 칸은 위·왼쪽 아래·오른쪽 아래 순서다.</summary>
        public void ConfigureSkillTab(GameObject view, SkillCircleView[] circles, TMP_Text detailLabel, Button auto)
        {
            skillPanel = view; skillCircles = circles ?? new SkillCircleView[0]; skillDetail = detailLabel; autoPlate = auto;
        }

        /// <summary>장비와 스킬을 나란히 보일지 정한다(에디터 설치 코드).</summary>
        public void ConfigureSideBySide(bool value) => sideBySide = value;

        /// <summary>UI-07 B안 화면을 연결한다(에디터 설치 코드).</summary>
        public void ConfigurePaperDoll(GameObject gear, GameObject lists, EquipmentSlotView template, RectTransform worn,
            RectTransform storage, TMP_Text statsLabel, TMP_Text detailLabel, Button gearButton, Button skillButton,
            Button previous, Button next, TMP_Text heroLabel, int columns)
        {
            gearView = gear; listView = lists; slotTemplate = template; wornSlots = worn; storageGrid = storage;
            stats = statsLabel; detail = detailLabel; gearTab = gearButton; skillTab = skillButton;
            previousHero = previous; nextHero = next; heroName = heroLabel; storageColumns = Mathf.Max(1, columns);
        }

        /// <summary>마우스를 올린 칸 옆에 띄울 툴팁을 연결한다. 글은 <see cref="ConfigurePaperDoll"/>의 detail이다.</summary>
        public void ConfigureTooltip(RectTransform box) => tooltip = box;

        /// <summary>툴팁이 보이는지(테스트용).</summary>
        public bool TooltipShowing => tooltip != null && tooltip.gameObject.activeSelf;

        /// <summary>빈 장비 칸 그림(EquipmentSlot 값 순서)을 연결한다.</summary>
        public void ConfigureSlotPlaceholders(Sprite[] sprites) => slotPlaceholders = sprites ?? new Sprite[0];

        /// <summary>장비 칸 자리(EquipmentSlot 값 순서)를 연결한다.</summary>
        public void ConfigureWornAnchors(RectTransform[] anchors) => wornAnchors = anchors ?? new RectTransform[0];

        public void Configure(GamePhaseView view, PreparationInteraction input, GameObject popup, TMP_Text statusLabel,
            TMP_Text bodyLabel, RectTransform equipped, RectTransform inventory, Button template)
        {
            phaseView = view; interaction = input; panel = popup; status = statusLabel; body = bodyLabel;
            equippedList = equipped; inventoryList = inventory; rowTemplate = template;
        }

        void Awake()
        {
            battle = GetComponent<GameBattleController>();
            flow = GameSession.Flow;
            if (panel != null) panel.SetActive(false);
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (slotTemplate != null) slotTemplate.gameObject.SetActive(false);
            equippedHeader = Header(equippedList);
            inventoryHeader = Header(inventoryList);
            equippedHeaderText = equippedHeader != null ? equippedHeader.text : string.Empty;
            inventoryHeaderText = inventoryHeader != null ? inventoryHeader.text : string.Empty;
        }

        // 목록 제목은 목록과 같은 열 안의 "Header" 글자다(GameFacilitySetup.Column).
        static TMP_Text Header(RectTransform list)
        {
            Transform header = list != null && list.parent != null ? list.parent.Find("Header") : null;
            return header != null ? header.GetComponent<TMP_Text>() : null;
        }

        void OnEnable()
        {
            if (interaction == null) return;
            interaction.EquipmentRequested += OnRequested;
            interaction.EquipmentKeyInPanel += OnKeyInPanel;
        }

        void OnDisable()
        {
            if (interaction == null) return;
            interaction.EquipmentRequested -= OnRequested;
            interaction.EquipmentKeyInPanel -= OnKeyInPanel;
        }

        // 장비 창을 연 키(I)를 다시 누르면 닫는다(사용자 2026-11-23). 다른 창이 열려 있을 때는 아무 일도 없다.
        void OnKeyInPanel() { if (IsOpen) Close(); }

        void OnRequested() => Open();

        RunState State => battle != null ? battle.RunLink.State : null;
        RunHero Player => State?.Player;
        RunHero selected;

        /// <summary>장비를 바꿀 대상. 고용 영웅이 없으면 늘 플레이어다.</summary>
        public RunHero SelectedHero
        {
            get
            {
                RunState state = State;
                if (state == null) return null;
                if (selected == null || state.Find(selected.Id) != selected) selected = state.Player;
                return selected;
            }
        }

        /// <summary>대상을 다음 영웅(플레이어 포함, 런에 들어온 순서)으로 바꾼다.</summary>
        public bool NextHero() => StepHero(1);

        /// <summary>대상을 이전 영웅으로 바꾼다.</summary>
        public bool PreviousHero() => StepHero(-1);

        bool StepHero(int step)
        {
            RunState state = State;
            if (!IsOpen || state == null || state.Heroes.Count == 0) return false;
            int index = 0;
            for (int i = 0; i < state.Heroes.Count; i++) if (state.Heroes[i] == SelectedHero) index = i;
            int count = state.Heroes.Count;
            selected = state.Heroes[((index + step) % count + count) % count];
            selectedSkill = 0;
            Message = string.Empty;
            Rebuild();
            return true;
        }

        // 버튼 연결용(반환값 없는 메서드만 저장된 리스너로 쓸 수 있다).
        public void NextHeroFromButton() => NextHero();
        public void PreviousHeroFromButton() => PreviousHero();
        public void ShowGearView() { if (SkillView) ToggleView(); }
        public void ShowSkillView() { if (!SkillView) ToggleView(); }
        public void ToggleHeroAutoFromButton() => ToggleHeroAuto();

        /// <summary>스킬 탭에서 칸을 고르고 아래 설명 칸에 보여 준다. 잠긴 칸도 고를 수 있다.</summary>
        public bool SelectSkill(int slot)
        {
            if (!IsOpen || !SkillsShown || slot < 0 || slot >= SkillSlotRules.MaxSlots) return false;
            selectedSkill = slot;
            Rebuild();
            return true;
        }

        /// <summary>준비 중 1인칭 탐색에서 장비 화면을 연다. 전투 중·다른 화면·런이 없으면 false.</summary>
        public bool Open()
        {
            if (IsOpen || flow.Phase != GamePhase.Preparation || phaseView == null || panel == null || Player == null) return false;
            if (!phaseView.EnterPanel()) return false;
            IsOpen = true;
            Message = string.Empty;
            selected = Player;
            SkillView = false;
            selectedSkill = 0;
            PopupBackdrop.Attach(panel);
            panel.SetActive(true);
            Rebuild();
            return true;
        }

        public void Close()
        {
            if (phaseView != null) phaseView.ExitPanel();
            Hide();
        }

        /// <summary>보관함 장비 목록의 index번째(장비만 센다)를 플레이어에게 끼운다.</summary>
        public bool TryEquip(int index)
        {
            RunState state = State;
            List<ItemInstance> gear = InventoryGear(state);
            if (!IsOpen || index < 0 || index >= gear.Count) return false;
            ItemInstance item = gear[index];
            ItemInstance replaced = SelectedHero.Character.EquippedIn(item.Definition.Slot);
            bool equipped = state.TryEquip(SelectedHero, item);
            Message = !equipped ? "이 영웅은 장비를 낄 수 없습니다."
                : replaced != null ? $"{item.Definition.DisplayName} 장착 ({replaced.Definition.DisplayName} 해제)"
                : $"{item.Definition.DisplayName} 장착";
            Rebuild();
            return equipped;
        }

        /// <summary>착용 중인 장비의 index번째를 벗긴다.</summary>
        public bool TryUnequip(int index)
        {
            RunState state = State;
            RunHero hero = SelectedHero;
            if (!IsOpen || hero == null || index < 0 || index >= hero.Character.Equipment.Count) return false;
            ItemInstance item = hero.Character.Equipment[index];
            bool removed = state.TryUnequip(hero, item);
            if (removed) Message = $"{item.Definition.DisplayName} 해제";
            Rebuild();
            return removed;
        }

        bool TryUnequipItem(ItemInstance item)
        {
            RunHero hero = SelectedHero;
            if (hero == null) return false;
            for (int i = 0; i < hero.Character.Equipment.Count; i++)
                if (hero.Character.Equipment[i] == item) return TryUnequip(i);
            return false;
        }

        static string SlotCountText(Characters.Character character) =>
            $"장비 칸 {character.Equipment.Count} / {(character.CanEquip ? EquipmentSlots.Count : 0)}";

        Sprite Placeholder(EquipmentSlot kind)
        {
            int index = (int)kind;
            return slotPlaceholders != null && index < slotPlaceholders.Length ? slotPlaceholders[index] : null;
        }

        /// <summary>장비 보기와 스킬 보기를 바꾼다.</summary>
        public bool ToggleView()
        {
            if (!IsOpen || SideBySide) return false;
            SkillView = !SkillView;
            selectedSkill = 0;
            Message = string.Empty;
            Rebuild();
            return true;
        }

        /// <summary>고른 영웅(플레이어 제외)의 스킬 자동 사용을 켜고 끈다.</summary>
        public bool ToggleHeroAuto()
        {
            RunHero hero = SelectedHero;
            if (!IsOpen || !SkillsShown || hero == null || hero.IsPlayer) return false;
            hero.SkillAuto = !hero.SkillAuto;
            Message = hero.SkillAuto ? "자동 사용 켜짐: 영웅이 알아서 스킬을 씁니다." : "자동 사용 꺼짐: 부대를 고르고 Alt + 스킬 키로 시킵니다.";
            Rebuild();
            return true;
        }

        void Hide()
        {
            IsOpen = false;
            if (panel != null) panel.SetActive(false);
        }

        void Update()
        {
            // Esc(PreparationInteraction)나 단계 전환으로 팝업 화면이 끝나면 함께 닫는다.
            if (IsOpen && (phaseView == null || !phaseView.IsPanelView)) Hide();
        }

        static List<ItemInstance> InventoryGear(RunState state)
        {
            var gear = new List<ItemInstance>();
            if (state == null) return gear;
            foreach (ItemInstance item in state.Inventory.Items)
                if (item.Definition.Kind == ItemKind.Equipment) gear.Add(item);
            return gear;
        }

        void Rebuild()
        {
            RunState state = State;
            RunHero hero = SelectedHero;
            if (hero == null) return;
            if (equippedHeader != null) equippedHeader.text = SkillView ? "스킬 칸" : equippedHeaderText;
            if (inventoryHeader != null)
                inventoryHeader.text = SkillView ? "스킬 사용" : inventoryHeaderText;
            RefreshChrome(state, hero);
            if (SideBySide)
            {
                RebuildPaperDoll(state, hero);
                RebuildSkillTab(hero, battle != null ? battle.Skills : null);
                return;
            }
            if (SkillView)
            {
                RebuildSkills(state, hero);
                return;
            }
            if (PaperDoll)
            {
                RebuildPaperDoll(state, hero);
                return;
            }
            var character = hero.Character;
            if (status != null)
            {
                var text = new StringBuilder(state.Heroes.Count > 1 ? $"{character.Definition.DisplayName} · " : string.Empty);
                text.Append(SlotCountText(character)).Append('\n');
                for (int i = 0; i < ShownStats.Length; i++)
                {
                    if (i > 0) text.Append(" · ");
                    text.Append($"{StatName(ShownStats[i])} {character.Stats.GetValue(ShownStats[i]):0.##}");
                }
                status.text = text.ToString();
            }
            if (body != null)
                body.text = (string.IsNullOrEmpty(Message) ? string.Empty : Message + "\n") + "보관함 장비를 클릭하면 장착하고, 착용 중인 장비를 클릭하면 벗습니다.";
            ClearRows();
            if (rowTemplate == null) return;
            AddHeadRows(state, hero);
            if (character.Equipment.Count == 0) AddRow(equippedList, "착용한 장비가 없습니다.", null);
            for (int i = 0; i < character.Equipment.Count; i++)
            {
                int index = i;
                ItemInstance item = character.Equipment[i];
                AddRow(equippedList, $"{item.DisplayName} · {Describe(item)}", () => TryUnequip(index));
            }
            List<ItemInstance> gear = InventoryGear(state);
            if (gear.Count == 0) AddRow(inventoryList, "보관함에 장비가 없습니다.", null);
            for (int i = 0; i < gear.Count; i++)
            {
                int index = i;
                AddRow(inventoryList, $"{gear[i].DisplayName} · {Describe(gear[i])}", () => TryEquip(index));
            }
        }

        // B안: 탭·대상 바꾸기 버튼과, 장비 보기(격자)·스킬 보기(목록) 중 하나만 보이게 한다.
        void RefreshChrome(RunState state, RunHero hero)
        {
            if (!PaperDoll) return;
            if (SideBySide)
            {
                gearView.SetActive(true);
                if (listView != null) listView.SetActive(false);
                skillPanel.SetActive(true);
                if (autoPlate != null) autoPlate.gameObject.SetActive(!hero.IsPlayer);
                if (gearTab != null) gearTab.gameObject.SetActive(false);
                if (skillTab != null) skillTab.gameObject.SetActive(false);
                if (status != null) status.gameObject.SetActive(false);
                if (body != null) body.gameObject.SetActive(false);
            }
            else RefreshViewChrome(hero);
            bool many = state.Heroes.Count > 1;
            if (previousHero != null) previousHero.gameObject.SetActive(many);
            if (nextHero != null) nextHero.gameObject.SetActive(many);
            // 이름은 고를 영웅이 여럿일 때만 보인다(사용자 2026-11-23: 플레이어 혼자면 이름 줄을 지운다).
            if (heroName != null)
            {
                heroName.gameObject.SetActive(many);
                heroName.text = hero.Definition.DisplayName;
            }
        }

        // 탭 보기: 장비 보기(격자)·스킬 보기 중 하나만 보이게 한다.
        void RefreshViewChrome(RunHero hero)
        {
            gearView.SetActive(!SkillView);
            if (listView != null) listView.SetActive(SkillView && !SkillTriangle);
            if (skillPanel != null) skillPanel.SetActive(SkillView && SkillTriangle);
            if (autoPlate != null) autoPlate.gameObject.SetActive(SkillView && SkillTriangle && !hero.IsPlayer);
            SetTab(gearTab, !SkillView);
            SetTab(skillTab, SkillView);
            // 장비 보기에서는 "장비 칸 n / 4" 줄을 감춘다(글은 테스트용으로 계속 쓴다). 스킬 보기에서는 보인다.
            if (status != null) status.gameObject.SetActive(SkillView && !SkillTriangle);
            // 장비 보기에는 "나무 방패 장착" 같은 알림 줄도 두지 않는다(사용자 2026-11-23). 스킬 보기 안내는 남긴다.
            if (body != null) body.gameObject.SetActive(SkillView && !SkillTriangle);
            if (SkillView && tooltip != null) tooltip.gameObject.SetActive(false);
        }

        // 선택된 탭은 누를 수 없고 금색이다(사용자 2026-11-23: 크기는 그대로, 색만 바꾼다). 색은 버튼 색 전환(서서히
        // 바뀜)에 맡기지 않고 글자에 바로 칠한다. 다른 탭은 흰 글자에 버튼 색(회색, 올리면 흰색)이 곱해진다.
        static readonly Color TabSelectedColor = new Color32(0xE3, 0xBB, 0x71, 0xFF);

        static void SetTab(Button tab, bool selected)
        {
            if (tab == null) return;
            tab.interactable = !selected;
            var label = tab.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            label.color = selected ? TabSelectedColor : Color.white;
        }

        void RebuildPaperDoll(RunState state, RunHero hero)
        {
            var character = hero.Character;
            ClearRows();
            ClearSlots();
            hoveredItem = null;
            if (status != null) status.text = SlotCountText(character);
            // 안내 문구 없이 방금 한 일만 보인다(사용자 2026-11-23).
            if (body != null) body.text = Message ?? string.Empty;
            wornViews.Clear();
            // 무기·보조무기·방어구·장신구 칸(UI-07). 빈 칸에는 그 종류 그림을 흐리게 띄운다.
            if (character.CanEquip)
                foreach (EquipmentSlot kind in EquipmentSlots.All)
                {
                    EquipmentSlotView slot = AddWornSlot(kind);
                    ItemInstance item = character.EquippedIn(kind);
                    if (item == null) { slot.ShowEmpty(Placeholder(kind)); continue; }
                    var wornRect = (RectTransform)slot.transform;
                    slot.Show(item.DisplayName, item.Definition.Icon, item.UpgradeLevel, () => TryUnequipItem(item),
                        () => Hover(item, true, wornRect), () => Unhover(item));
                }
            List<ItemInstance> gear = InventoryGear(state);
            int cells = Mathf.Max(minStorageCells, (gear.Count + storageColumns - 1) / storageColumns * storageColumns);
            for (int i = 0; i < cells; i++)
            {
                EquipmentSlotView slot = AddSlot(storageGrid);
                if (i >= gear.Count) { slot.ShowEmpty(null); continue; }
                int index = i;
                ItemInstance item = gear[i];
                var cellRect = (RectTransform)slot.transform;
                slot.Show(item.DisplayName, item.Definition.Icon, item.UpgradeLevel, () => TryEquip(index),
                    () => Hover(item, false, cellRect), () => Unhover(item));
            }
            RefreshPreview();
        }

        RectTransform hoveredRect;

        void Hover(ItemInstance item, bool worn, RectTransform cell)
        {
            hoveredItem = item;
            hoveredWorn = worn;
            hoveredRect = cell;
            RefreshPreview();
        }

        void Unhover(ItemInstance item)
        {
            if (hoveredItem != item) return;
            hoveredItem = null;
            RefreshPreview();
        }

        /// <summary>
        /// 마우스를 올린 아이템을 미리 본다(테스트용). worn이면 착용 중인 index번째, 아니면 보관함 장비 index번째.
        /// 범위 밖이면 미리보기를 끈다.
        /// </summary>
        public void PreviewItem(bool worn, int index)
        {
            RunHero hero = SelectedHero;
            if (!IsOpen || hero == null) return;
            IReadOnlyList<ItemInstance> source = worn ? hero.Character.Equipment : (IReadOnlyList<ItemInstance>)InventoryGear(State);
            if (index < 0 || index >= source.Count) { hoveredItem = null; RefreshPreview(); return; }
            Hover(source[index], worn, null);
        }

        // 왼쪽 스탯 목록(미리보기 변화 포함)과 격자 아래 상세 칸을 다시 쓴다.
        void RefreshPreview()
        {
            RunHero hero = SelectedHero;
            if (hero == null || !PaperDoll) return;
            var sheet = hero.Character.Stats;
            ItemInstance item = hoveredItem;
            // 보관함 장비는 같은 칸의 장비와 바꿔 끼므로 그 장비 효과를 빼고 계산한다.
            ItemInstance occupant = item != null && !hoveredWorn ? hero.Character.EquippedIn(item.Definition.Slot) : null;
            if (stats != null)
            {
                var text = new StringBuilder();
                foreach (StatType stat in ShownStats)
                {
                    float now = sheet.GetValue(stat);
                    text.Append(StatName(stat)).Append(" <pos=58%>");
                    // 미리 보기는 바뀐 뒤의 최종 값을 그 자리에 색으로 보인다(사용자 2026-11-23: "+12.35"처럼 차이를 덧붙이면
                    // 상자 밖으로 삐져나간다). 오르면 초록, 내리면 빨강.
                    float after = item == null ? now
                        : hoveredWorn ? sheet.GetValueWith(stat, null, item) : sheet.GetValueWith(stat, item.EquipModifiers, occupant);
                    float delta = after - now;
                    if (Mathf.Abs(delta) > 0.001f) text.Append($"<color={(delta > 0f ? Up : Down)}>{after:0.##}</color>");
                    else text.Append(now.ToString("0.##"));
                    text.Append('\n');
                }
                stats.text = text.ToString().TrimEnd('\n');
            }
            if (item == null)
            {
                if (detail != null) detail.text = string.Empty;
                if (tooltip != null) tooltip.gameObject.SetActive(false);
                return;
            }
            // 이름·종류 한 줄, 효과는 한 줄에 하나씩(효과가 많아도 툴팁이 늘어난다). 클릭 안내는 쓰지 않는다(사용자 2026-11-23).
            var tip = new StringBuilder($"<b>{item.DisplayName}</b>  <color={Dim}>{EquipmentSlots.DisplayName(item.Definition.Slot)}</color>");
            if (item.EquipModifiers.Count == 0) tip.Append($"\n<color={Dim}>효과 없음</color>");
            foreach (StatModifier modifier in item.EquipModifiers) tip.Append("\n").Append(DescribeModifier(modifier));
            if (detail != null) detail.text = tip.ToString();
            PlaceTooltip(hoveredRect);
        }

        // 툴팁을 마우스를 올린 칸 오른쪽(창 밖으로 나가면 왼쪽)에 붙인다. 칸을 모르면(테스트) 창 가운데에 둔다.
        void PlaceTooltip(RectTransform cell)
        {
            if (tooltip == null) return;
            tooltip.gameObject.SetActive(true);
            tooltip.SetAsLastSibling();
            var parent = (RectTransform)tooltip.parent;
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltip);
            tooltip.anchorMin = tooltip.anchorMax = new Vector2(0.5f, 0.5f);
            if (cell == null || parent == null)
            {
                tooltip.pivot = new Vector2(0.5f, 0.5f);
                tooltip.anchoredPosition = Vector2.zero;
                return;
            }
            var corners = new Vector3[4];
            cell.GetWorldCorners(corners); // 왼쪽 아래, 왼쪽 위, 오른쪽 위, 오른쪽 아래
            Vector2 topLeft = parent.InverseTransformPoint(corners[1]);
            Vector2 topRight = parent.InverseTransformPoint(corners[2]);
            float width = tooltip.rect.width;
            bool right = topRight.x + TooltipGap + width <= parent.rect.xMax - TooltipGap;
            tooltip.pivot = new Vector2(right ? 0f : 1f, 1f);
            tooltip.anchoredPosition = right ? topRight + new Vector2(TooltipGap, 0f) : topLeft - new Vector2(TooltipGap, 0f);
        }

        const float TooltipGap = 6f;

        EquipmentSlotView AddWornSlot(EquipmentSlot kind)
        {
            int index = (int)kind;
            RectTransform anchor = wornAnchors != null && index < wornAnchors.Length ? wornAnchors[index] : null;
            EquipmentSlotView slot = AddSlot(anchor != null ? anchor : wornSlots);
            if (anchor != null)
            {
                var rect = (RectTransform)slot.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            wornViews.Add(slot);
            return slot;
        }

        EquipmentSlotView AddSlot(RectTransform parent)
        {
            EquipmentSlotView slot = Instantiate(slotTemplate, parent);
            slot.gameObject.SetActive(true);
            slots.Add(slot);
            return slot;
        }

        void ClearSlots()
        {
            foreach (EquipmentSlotView slot in slots)
            {
                if (slot == null) continue;
                slot.gameObject.SetActive(false);
                Destroy(slot.gameObject);
            }
            slots.Clear();
        }

        static List<string> SlotNames(RectTransform parent)
        {
            var names = new List<string>();
            if (parent == null) return names;
            foreach (Transform child in parent)
            {
                if (!child.gameObject.activeSelf) continue;
                var slot = child.GetComponent<EquipmentSlotView>();
                if (slot != null && !slot.IsEmpty) names.Add(slot.ItemName);
            }
            return names;
        }

        // 왼쪽 맨 위: 대상 바꾸기(고용 영웅이 있을 때). 오른쪽 맨 위: 보기 바꾸기. B안에서는 탭과 위쪽 버튼이 대신한다.
        void AddHeadRows(RunState state, RunHero hero)
        {
            if (PaperDoll) return;
            if (state.Heroes.Count > 1)
                AddRow(equippedList, $"대상: {hero.Definition.DisplayName}" + (hero.IsPlayer ? " (플레이어)" : string.Empty)
                    + " - 클릭해서 바꾸기", () => NextHero());
            AddRow(inventoryList, SkillView ? "보기: 스킬 - 클릭해서 장비 보기" : "보기: 장비 - 클릭해서 스킬 보기", () => ToggleView());
        }

        void RebuildSkills(RunState state, RunHero hero)
        {
            GameSkills skills = battle != null ? battle.Skills : null;
            if (SkillTriangle)
            {
                RebuildSkillTab(hero, skills);
                return;
            }
            IReadOnlyList<SkillDefinition> fixedSkills = hero.Definition.Skills;
            List<SkillDefinition> open = RunState.OpenSkills(hero);
            if (status != null)
                status.text = $"{hero.Definition.DisplayName} · 등급 {hero.Grade} · 스킬 칸 {open.Count} / {SkillSlotRules.MaxSlots}"
                    + (hero.IsPlayer ? string.Empty : $" · 자동 사용 {(hero.SkillAuto ? "켜짐" : "꺼짐")}");
            if (body != null)
                body.text = (string.IsNullOrEmpty(Message) ? string.Empty : Message + "\n")
                    + (hero.IsPlayer ? "플레이어 스킬은 정해져 있고 처음부터 모두 열려 있습니다. 전투 중 스킬 키로 씁니다."
                        : "영웅 스킬은 정해져 있고 등급이 오르면 칸이 열립니다. 전투 중 부대를 고르고 Alt + 스킬 키로 직접 시킬 수 있습니다.");
            ClearRows();
            if (rowTemplate == null) return;
            AddHeadRows(state, hero);
            int opened = 0;
            int fixedCount = fixedSkills != null ? fixedSkills.Count : 0;
            for (int i = 0; i < fixedCount && i < SkillSlotRules.MaxSlots; i++)
            {
                SkillDefinition skill = fixedSkills[i];
                if (skill == null) continue;
                if (i < SkillSlotRules.HeroSlots(hero.Grade))
                    AddRow(equippedList, $"{CastKey(skills, hero, opened++)}: {DescribeSkill(skill)}", null);
                else AddRow(equippedList, $"잠김: {skill.DisplayName} (등급 {i + 1}에 열림)", null);
            }
            if (open.Count == 0) AddRow(equippedList, "스킬이 없습니다.", null);
            if (hero.IsPlayer) AddRow(inventoryList, "직접 사용: 전투 중 스킬 키를 누릅니다.", null);
            else AddRow(inventoryList, $"자동 사용: {(hero.SkillAuto ? "켜짐" : "꺼짐")} - 클릭해서 바꾸기", () => ToggleHeroAuto());
        }

        // 삼각형 스킬 탭: 고정 스킬 3칸(잠긴 칸은 흐리게, 배지는 열리는 등급), 고른 칸의 설명, 영웅이면 자동 사용 버튼.
        void RebuildSkillTab(RunHero hero, GameSkills skills)
        {
            IReadOnlyList<SkillDefinition> fixedSkills = hero.Definition.Skills;
            int open = SkillSlotRules.HeroSlots(hero.Grade);
            if (fixedSkills == null || selectedSkill >= fixedSkills.Count || fixedSkills[selectedSkill] == null) selectedSkill = 0;
            for (int i = 0; i < skillCircles.Length; i++)
            {
                SkillCircleView circle = skillCircles[i];
                if (circle == null) continue;
                SkillDefinition skill = fixedSkills != null && i < fixedSkills.Count ? fixedSkills[i] : null;
                if (skill == null) { circle.Hide(); continue; }
                int slot = i;
                circle.Show(skill.DisplayName, skill.Icon, i + 1, i >= open, i == selectedSkill, () => SelectSkill(slot));
            }
            SkillDefinition chosen = fixedSkills != null && selectedSkill < fixedSkills.Count ? fixedSkills[selectedSkill] : null;
            skillDetail.text = chosen == null ? "스킬이 없습니다." : SkillDetail(chosen, hero, skills, selectedSkill, selectedSkill >= open);
            if (!hero.IsPlayer)
            {
                AutoPlateShowsOn = hero.SkillAuto;
                PaintAutoPlate(hero.SkillAuto);
            }
        }

        // 켜짐: 초록 판에 밝은 글자. 꺼짐: 회색 판에 흐린 글자.
        static readonly Color AutoOnPlate = new Color32(0x2F, 0x6B, 0x3A, 0xFF), AutoOffPlate = new Color32(0x3A, 0x3D, 0x40, 0xFF);
        static readonly Color AutoOnText = new Color32(0xF2, 0xEA, 0xD8, 0xFF), AutoOffText = new Color32(0xA8, 0xA4, 0x9A, 0xFF);

        void PaintAutoPlate(bool on)
        {
            if (autoPlate == null) return;
            if (autoPlate.targetGraphic != null) autoPlate.targetGraphic.color = on ? AutoOnPlate : AutoOffPlate;
            TMP_Text label = autoPlate.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            label.text = on ? "자동 사용 켜짐" : "자동 사용 꺼짐";
            label.color = on ? AutoOnText : AutoOffText;
        }

        static string SkillDetail(SkillDefinition skill, RunHero hero, GameSkills skills, int slot, bool locked)
        {
            var text = new StringBuilder();
            text.Append($"<color=#E3BB71><size=120%>{skill.DisplayName}</size></color>");
            if (locked) text.Append($"  <color={Dim}>{slot + 1}등급에 열림</color>");
            else text.Append($"  <color={Dim}>{CastKey(skills, hero, slot).Replace("+", " + ")}</color>");
            text.Append('\n').Append($"<color={Dim}>{SkillInfo(skill)}</color>");
            if (skill.Description.Length > 0) text.Append('\n').Append(skill.Description);
            return text.ToString();
        }

        // 쿨다운 · 사거리 · 범위 · 대상 한 줄.
        static string SkillInfo(SkillDefinition skill)
        {
            var parts = new List<string> { $"쿨다운 {skill.Cooldown:0.#}초" };
            bool aimed = skill.TargetMode == SkillTargetMode.AimPoint || skill.TargetMode == SkillTargetMode.TargetUnit;
            if (aimed && skill.Range > 0f) parts.Add($"사거리 {skill.Range:0.#}m");
            if (skill.TargetMode != SkillTargetMode.TargetUnit && skill.Radius > 0f) parts.Add($"범위 {skill.Radius:0.#}m");
            parts.Add(skill.TargetMode switch
            {
                SkillTargetMode.Self => "자기 주변",
                SkillTargetMode.AimPoint => "조준 지점",
                SkillTargetMode.TargetUnit => "대상 하나",
                SkillTargetMode.OwnSquad => "자기 부대",
                _ => string.Empty,
            });
            return string.Join(" · ", parts);
        }

        // 플레이어는 스킬 키만, 영웅은 보조 키 + 스킬 키로 쓴다.
        static string CastKey(GameSkills skills, RunHero hero, int slot) =>
            hero.IsPlayer ? SlotKey(skills, slot) : $"{ModifierKey(skills)}+{SlotKey(skills, slot)}";

        static string SlotKey(GameSkills skills, int slot)
        {
            string key = skills != null ? skills.KeyOf(slot) : string.Empty;
            return string.IsNullOrEmpty(key) ? (slot + 1).ToString() : key;
        }

        static string ModifierKey(GameSkills skills) => skills != null ? skills.ModifierKey : "Alt";

        static string DescribeSkill(SkillDefinition skill) => $"{skill.DisplayName} · 쿨다운 {skill.Cooldown:0.#}초";

        static List<string> RowTexts(RectTransform list)
        {
            var texts = new List<string>();
            if (list == null) return texts;
            foreach (Transform child in list)
                if (child.gameObject.activeSelf) texts.Add(child.GetComponentInChildren<TMP_Text>(true).text);
            return texts;
        }

        void AddRow(RectTransform list, string text, UnityEngine.Events.UnityAction action)
        {
            if (list == null) return;
            Button row = Instantiate(rowTemplate, list);
            row.gameObject.SetActive(true);
            row.GetComponentInChildren<TMP_Text>(true).text = text;
            row.interactable = action != null;
            if (action != null) row.onClick.AddListener(action);
            rows.Add(row);
        }

        void ClearRows()
        {
            foreach (Button row in rows)
            {
                if (row == null) continue;
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
            rows.Clear();
        }

        static int ActiveChildren(Transform parent)
        {
            int count = 0;
            foreach (Transform child in parent) if (child.gameObject.activeSelf) count++;
            return count;
        }

        static string DescribeModifier(StatModifier modifier) =>
            StatName(modifier.stat) + " " + (modifier.kind == ModifierKind.Percent ? $"{modifier.value * 100f:+0;-0}%" : $"{modifier.value:+0.##;-0.##}");

        string Describe(ItemInstance item)
        {
            IReadOnlyList<StatModifier> modifiers = item.EquipModifiers;
            if (modifiers.Count == 0) return "효과 없음";
            var text = new StringBuilder();
            foreach (StatModifier modifier in modifiers)
            {
                if (text.Length > 0) text.Append(", ");
                text.Append(StatName(modifier.stat)).Append(' ');
                text.Append(modifier.kind == ModifierKind.Percent ? $"{modifier.value * 100f:+0;-0}%" : $"{modifier.value:+0.##;-0.##}");
            }
            return text.ToString();
        }

        static string StatName(StatType stat) => stat switch
        {
            StatType.MaxHealth => "최대 체력",
            StatType.Attack => "공격력",
            StatType.Defense => "방어력",
            StatType.AttackSpeed => "공격 속도",
            StatType.MoveSpeed => "이동 속도",
            StatType.AttackRange => "사거리",
            StatType.AimSpread => "탄퍼짐",
            _ => stat.ToString(),
        };
    }
}
