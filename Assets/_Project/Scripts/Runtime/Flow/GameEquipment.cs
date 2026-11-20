using System.Collections.Generic;
using System.Text;
using ProjectNeo.Economy;
using ProjectNeo.Run;
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

        public bool IsOpen { get; private set; }
        public string Message { get; private set; } = string.Empty;
        public string StatusText => status != null ? status.text : string.Empty;
        public string BodyText => body != null ? body.text : string.Empty;
        public int EquippedRows => equippedList != null ? ActiveChildren(equippedList) : 0;
        public int InventoryRows => inventoryList != null ? ActiveChildren(inventoryList) : 0;

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
            if (state.Heroes.Count > 1)
                AddRow(equippedList, $"대상: {character.Definition.DisplayName}" + (hero.IsPlayer ? " (플레이어)" : string.Empty)
                    + " - 클릭해서 바꾸기", () => NextHero());
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
            _ => stat.ToString(),
        };
    }
}
