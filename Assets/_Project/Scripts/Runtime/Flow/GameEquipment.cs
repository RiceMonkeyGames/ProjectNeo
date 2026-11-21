using System.Collections.Generic;
using System.Text;
using ProjectNeo.Economy;
using ProjectNeo.Run;
using ProjectNeo.Skills;
using ProjectNeo.Stats;
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
        readonly List<Button> rows = new List<Button>();
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

        void OnEnable() { if (interaction != null) interaction.EquipmentRequested += OnRequested; }
        void OnDisable() { if (interaction != null) interaction.EquipmentRequested -= OnRequested; }

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
        public bool NextHero()
        {
            RunState state = State;
            if (!IsOpen || state == null || state.Heroes.Count == 0) return false;
            int index = -1;
            for (int i = 0; i < state.Heroes.Count; i++) if (state.Heroes[i] == SelectedHero) index = i;
            selected = state.Heroes[(index + 1) % state.Heroes.Count];
            Message = string.Empty;
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
            // 퍽을 바꿨을 수 있으니 배운 스킬에 맞춰 장착 칸을 정리한다(빈 칸은 배운 순서로 채운다).
            State.PlayerSkillLoadout.Refresh(State.LearnedPlayerSkills());
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
            bool equipped = state.TryEquip(SelectedHero, item);
            Message = equipped ? $"{item.Definition.DisplayName} 장착" : "빈 장비 칸이 없습니다. 먼저 장비를 벗으세요.";
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

        /// <summary>장비 보기와 스킬 보기를 바꾼다.</summary>
        public bool ToggleView()
        {
            if (!IsOpen) return false;
            SkillView = !SkillView;
            Message = string.Empty;
            Rebuild();
            return true;
        }

        /// <summary>플레이어의 배운 스킬 중 장착하지 않은 것의 index번째를 첫 빈 칸에 장착한다.</summary>
        public bool TryEquipSkill(int index)
        {
            RunState state = State;
            List<SkillDefinition> spare = SpareSkills(state);
            if (!IsOpen || !SkillView || SelectedHero == null || !SelectedHero.IsPlayer || index < 0 || index >= spare.Count) return false;
            bool equipped = state.TryEquipPlayerSkill(spare[index].Id);
            Message = equipped ? $"{spare[index].DisplayName} 장착" : "빈 스킬 칸이 없습니다. 먼저 스킬을 빼세요.";
            Rebuild();
            return equipped;
        }

        /// <summary>플레이어 스킬 칸의 스킬을 뺀다.</summary>
        public bool TryUnequipSkill(int slot)
        {
            RunState state = State;
            if (!IsOpen || !SkillView || SelectedHero == null || !SelectedHero.IsPlayer) return false;
            SkillDefinition skill = SkillById(state.LearnedPlayerSkills(), state.PlayerSkillLoadout.EquippedId(slot));
            bool removed = state.UnequipPlayerSkill(slot);
            if (removed) Message = $"{(skill != null ? skill.DisplayName : "스킬")} 해제. 빈 칸은 전투 시작 때 남은 배운 스킬로 채워집니다.";
            Rebuild();
            return removed;
        }

        /// <summary>고른 영웅(플레이어 제외)의 스킬 자동 사용을 켜고 끈다.</summary>
        public bool ToggleHeroAuto()
        {
            RunHero hero = SelectedHero;
            if (!IsOpen || !SkillView || hero == null || hero.IsPlayer) return false;
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

        static List<SkillDefinition> SpareSkills(RunState state)
        {
            var spare = new List<SkillDefinition>();
            if (state == null) return spare;
            foreach (SkillDefinition skill in state.LearnedPlayerSkills())
                if (skill != null && !state.PlayerSkillLoadout.IsEquipped(skill.Id)) spare.Add(skill);
            return spare;
        }

        static SkillDefinition SkillById(IReadOnlyList<SkillDefinition> skills, string id)
        {
            if (skills == null || string.IsNullOrEmpty(id)) return null;
            foreach (SkillDefinition skill in skills)
                if (skill != null && skill.Id == id) return skill;
            return null;
        }

        void Rebuild()
        {
            RunState state = State;
            RunHero hero = SelectedHero;
            if (hero == null) return;
            if (equippedHeader != null) equippedHeader.text = SkillView ? "스킬 칸" : equippedHeaderText;
            if (inventoryHeader != null)
                inventoryHeader.text = SkillView ? (hero.IsPlayer ? "배운 스킬 (클릭해서 장착)" : "스킬 사용") : inventoryHeaderText;
            if (SkillView)
            {
                RebuildSkills(state, hero);
                return;
            }
            var character = hero.Character;
            if (status != null)
            {
                var text = new StringBuilder(state.Heroes.Count > 1 ? $"{character.Definition.DisplayName} · " : string.Empty);
                text.Append($"장비 칸 {character.Equipment.Count} / {character.Definition.EquipmentSlots}\n");
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

        // 왼쪽 맨 위: 대상 바꾸기(고용 영웅이 있을 때). 오른쪽 맨 위: 보기 바꾸기.
        void AddHeadRows(RunState state, RunHero hero)
        {
            if (state.Heroes.Count > 1)
                AddRow(equippedList, $"대상: {hero.Definition.DisplayName}" + (hero.IsPlayer ? " (플레이어)" : string.Empty)
                    + " - 클릭해서 바꾸기", () => NextHero());
            AddRow(inventoryList, SkillView ? "보기: 스킬 - 클릭해서 장비 보기" : "보기: 장비 - 클릭해서 스킬 보기", () => ToggleView());
        }

        void RebuildSkills(RunState state, RunHero hero)
        {
            GameSkills skills = battle != null ? battle.Skills : null;
            if (hero.IsPlayer)
            {
                List<SkillDefinition> learned = state.LearnedPlayerSkills();
                int count = state.PlayerSkillLoadout.EquippedSkills(learned).Count;
                if (status != null)
                    status.text = (state.Heroes.Count > 1 ? $"{hero.Definition.DisplayName} · " : string.Empty)
                        + $"스킬 칸 {count} / {state.PlayerSkillLoadout.SlotCount} · 배운 스킬 {learned.Count}";
                if (body != null)
                    body.text = (string.IsNullOrEmpty(Message) ? string.Empty : Message + "\n")
                        + (learned.Count == 0 ? "배운 스킬이 없습니다. 퍽 창의 전투 트리에서 스킬 퍽을 찍으면 배웁니다."
                            : "배운 스킬을 클릭하면 빈 칸에 장착하고, 칸을 클릭하면 뺍니다.");
                ClearRows();
                if (rowTemplate == null) return;
                AddHeadRows(state, hero);
                for (int i = 0; i < state.PlayerSkillLoadout.SlotCount; i++)
                {
                    int slot = i;
                    SkillDefinition skill = SkillById(learned, state.PlayerSkillLoadout.EquippedId(i));
                    string key = SlotKey(skills, i);
                    if (skill == null) AddRow(equippedList, $"{key}: 비어 있음", null);
                    else AddRow(equippedList, $"{key}: {DescribeSkill(skill)}", () => TryUnequipSkill(slot));
                }
                List<SkillDefinition> spare = SpareSkills(state);
                if (spare.Count == 0) AddRow(inventoryList, learned.Count == 0 ? "배운 스킬이 없습니다." : "배운 스킬을 모두 장착했습니다.", null);
                for (int i = 0; i < spare.Count; i++)
                {
                    int index = i;
                    AddRow(inventoryList, DescribeSkill(spare[i]), () => TryEquipSkill(index));
                }
                return;
            }

            IReadOnlyList<SkillDefinition> fixedSkills = hero.Definition.Skills;
            List<SkillDefinition> open = SkillSlotRules.OpenHeroSkills(fixedSkills, hero.Grade);
            if (status != null)
                status.text = $"{hero.Definition.DisplayName} · 등급 {hero.Grade} · 스킬 칸 {open.Count} / {SkillSlotRules.MaxSlots}"
                    + $" · 자동 사용 {(hero.SkillAuto ? "켜짐" : "꺼짐")}";
            if (body != null)
                body.text = (string.IsNullOrEmpty(Message) ? string.Empty : Message + "\n")
                    + "영웅 스킬은 정해져 있고 등급이 오르면 칸이 열립니다. 전투 중 부대를 고르고 Alt + 스킬 키로 직접 시킬 수 있습니다.";
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
                    AddRow(equippedList, $"{ModifierKey(skills)}+{SlotKey(skills, opened++)}: {DescribeSkill(skill)}", null);
                else AddRow(equippedList, $"잠김: {skill.DisplayName} (등급 {i + 1}에 열림)", null);
            }
            if (open.Count == 0) AddRow(equippedList, "스킬이 없습니다.", null);
            AddRow(inventoryList, $"자동 사용: {(hero.SkillAuto ? "켜짐" : "꺼짐")} - 클릭해서 바꾸기", () => ToggleHeroAuto());
        }

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
