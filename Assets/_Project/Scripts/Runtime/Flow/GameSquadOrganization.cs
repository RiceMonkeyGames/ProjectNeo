using System.Collections.Generic;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// Game 씬 준비 단계의 부대 편성 화면(D-8, UI-01 카드 배치 Q-49). 1인칭 탐색 중 편성 키(기본 O)로 열고 Esc나 닫기 버튼으로 닫는다.
    /// 부대마다 카드 한 장(맨 위 대장 칸, 아래 잡졸)을 가로로 나란히 두고, 옆에 "새 부대" 카드와 "대기 영웅" 카드를 둔다.
    /// 잡졸이나 영웅 줄을 눌러 고른 뒤 보낼 카드의 머리 버튼을 누르면 옮긴다: 잡졸은 그 부대(새 부대)로 한 명, 영웅은 그 부대의 대장으로,
    /// 대기 카드는 대장에서 뺀다. 같은 줄을 다시 누르면 선택을 푼다. 규칙은 D-8 그대로이며(<see cref="GameRunLink"/>가 런과 씬을 함께 바꾼다)
    /// 원래 부대가 비면 없어지고 대장은 대기로 간다. 플레이어는 대장이 될 수 없어 카드에 나오지 않는다(Q-32 임시).
    /// <see cref="GameBattleController"/>와 같은 오브젝트에 붙어 그 런 연결을 쓴다.
    /// </summary>
    [RequireComponent(typeof(GameBattleController))]
    public sealed class GameSquadOrganization : MonoBehaviour
    {
        const int NewSquad = -1;

        [SerializeField] GamePhaseView phaseView;
        [SerializeField] PreparationInteraction interaction;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text status;
        [SerializeField] TMP_Text body;
        [Tooltip("카드를 가로로 늘어놓는 곳(스크롤 내용).")]
        [SerializeField] RectTransform cardArea;
        [Tooltip("카드 한 장의 틀. 자식으로 Header 버튼과 List 세로 목록이 있다.")]
        [SerializeField] RectTransform cardTemplate;
        [SerializeField] Button rowTemplate;
        readonly List<GameObject> cards = new List<GameObject>();
        GameBattleController battle;
        GameFlow flow;

        public bool IsOpen { get; private set; }
        public string Message { get; private set; } = string.Empty;
        public string StatusText => status != null ? status.text : string.Empty;
        public string BodyText => body != null ? body.text : string.Empty;
        /// <summary>고른 잡졸이나 영웅. 없으면 null.</summary>
        public RunUnit Selected { get; private set; }
        /// <summary>지금 보이는 카드 수(부대 카드 + 새 부대 카드 + 대기 카드).</summary>
        public int CardCount { get { int count = 0; foreach (GameObject card in cards) if (card != null && card.activeSelf) count++; return count; } }

        public void Configure(GamePhaseView view, PreparationInteraction input, GameObject popup, TMP_Text statusLabel,
            TMP_Text bodyLabel, RectTransform cardsParent, RectTransform card, Button row)
        {
            phaseView = view; interaction = input; panel = popup; status = statusLabel; body = bodyLabel;
            cardArea = cardsParent; cardTemplate = card; rowTemplate = row;
        }

        void Awake()
        {
            battle = GetComponent<GameBattleController>();
            flow = GameSession.Flow;
            if (panel != null) panel.SetActive(false);
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        }

        void OnEnable() { if (interaction != null) interaction.OrganizationRequested += OnRequested; }
        void OnDisable() { if (interaction != null) interaction.OrganizationRequested -= OnRequested; }

        void OnRequested() => Open();

        RunState State => battle != null ? battle.RunLink.State : null;

        /// <summary>준비 중 1인칭 탐색에서 편성 화면을 연다. 전투 중·다른 화면·런이나 부대가 없으면 false.</summary>
        public bool Open()
        {
            RunState state = State;
            if (IsOpen || flow.Phase != GamePhase.Preparation || phaseView == null || panel == null || state == null) return false;
            if (state.Army.Squads.Count == 0 || !phaseView.EnterPanel()) return false;
            IsOpen = true;
            Message = string.Empty;
            Selected = null;
            panel.SetActive(true);
            Rebuild();
            return true;
        }

        public void Close()
        {
            if (phaseView != null) phaseView.ExitPanel();
            Hide();
        }

        /// <summary>잡졸이나 영웅을 고른다. 이미 고른 것을 다시 고르면 선택을 푼다. 이 런의 것이 아니면 false.</summary>
        public bool Select(RunUnit unit)
        {
            RunState state = State;
            if (!IsOpen || state == null || unit == null || state.Find(unit.Id) != unit) return false;
            if (unit is RunHero hero && hero.IsPlayer && !state.AllowPlayerCaptain) return false;
            Selected = Selected == unit ? null : unit;
            Message = Selected == null ? string.Empty : $"{NameOf(Selected)} selected -> Click the destination card header.";
            Rebuild();
            return true;
        }

        /// <summary>고른 것을 index번째 부대 카드에 놓는다(-1이면 새 부대 카드). 잡졸은 그 부대로, 영웅은 그 부대 대장으로.</summary>
        public bool DropOnSquad(int index)
        {
            RunState state = State;
            if (!IsOpen || state == null || Selected == null || index < NewSquad || index >= state.Army.Squads.Count) return false;
            bool done;
            if (Selected is RunSoldier soldier)
            {
                int before = state.Army.Squads.Count;
                OrganizeCheck check = battle.RunLink.TryMoveSoldier(soldier, index, out int movedTo);
                done = check == OrganizeCheck.Ok;
                bool removed = done && state.Army.Squads.Count < before + (index == NewSquad ? 1 : 0);
                Message = done ? $"{NameOf(soldier)} -> Squad {movedTo + 1}" + (removed ? " - Empty squad removed (captain in reserve)" : string.Empty)
                    : Reason(check);
            }
            else if (Selected is RunHero hero)
            {
                if (index == NewSquad) { Message = "Heroes can only captain squads with soldiers."; done = false; }
                else
                {
                    Squad<RunUnit> leading = state.LeadingSquadOf(hero);
                    OrganizeCheck check = leading == state.Army.Squads[index] ? OrganizeCheck.SameSquad : OrganizeCheck.Ok;
                    if (check == OrganizeCheck.Ok && leading != null) check = battle.RunLink.TrySetCaptain(state.IndexOf(leading), null);
                    if (check == OrganizeCheck.Ok) check = battle.RunLink.TrySetCaptain(index, hero);
                    done = check == OrganizeCheck.Ok;
                    Message = done ? $"Squad {index + 1} captain: {NameOf(hero)}" : Reason(check);
                }
            }
            else done = false;
            if (done) Selected = null;
            Rebuild();
            return done;
        }

        /// <summary>고른 영웅을 대장에서 빼 대기로 보낸다. 잡졸이나 이미 대기인 영웅이면 false.</summary>
        public bool DropOnReserve()
        {
            RunState state = State;
            if (!IsOpen || state == null || Selected == null) return false;
            bool done = false;
            if (Selected is RunHero hero && state.LeadingSquadOf(hero) is Squad<RunUnit> leading)
            {
                OrganizeCheck check = battle.RunLink.TrySetCaptain(state.IndexOf(leading), null);
                done = check == OrganizeCheck.Ok;
                Message = done ? $"{NameOf(hero)} sent to reserve." : Reason(check);
            }
            else Message = Selected is RunSoldier ? "Soldiers cannot enter reserve. Choose another squad card." : "Already in reserve.";
            if (done) Selected = null;
            Rebuild();
            return done;
        }

        static string Reason(OrganizeCheck check) => check switch
        {
            OrganizeCheck.BattlePending => "Cannot reorganize during battle.",
            OrganizeCheck.SameSquad => "Already in that squad.",
            OrganizeCheck.SquadFull => "The squad is full or the squad limit is reached.",
            OrganizeCheck.PlayerNotAllowed => "The player cannot be a captain yet.",
            OrganizeCheck.NotReserve => "Only reserve heroes can become captains.",
            _ => "Could not reorganize.",
        };

        static string NameOf(RunUnit unit) => unit switch
        {
            RunSoldier soldier => $"{soldier.Definition.DisplayName} #{soldier.Id}",
            RunHero hero => hero.Definition.DisplayName,
            _ => string.Empty,
        };

        void Hide()
        {
            IsOpen = false;
            Selected = null;
            if (panel != null) panel.SetActive(false);
        }

        void Update()
        {
            // Esc(PreparationInteraction)나 단계 전환으로 팝업 화면이 끝나면 함께 닫는다.
            if (IsOpen && (phaseView == null || !phaseView.IsPanelView)) Hide();
        }

        void Rebuild()
        {
            RunState state = State;
            if (state == null) return;
            IReadOnlyList<Squad<RunUnit>> squads = state.Army.Squads;
            if (squads.Count == 0) { Close(); return; }
            if (Selected != null && state.Find(Selected.Id) != Selected) Selected = null;
            IReadOnlyList<RunHero> reserve = state.ReserveHeroes;
            if (status != null)
                status.text = $"Squads {squads.Count} - Reserve heroes {reserve.Count} - Selected: " + (Selected != null ? NameOf(Selected) : "None");
            if (body != null)
                body.text = (string.IsNullOrEmpty(Message) ? string.Empty : Message + "\n")
                    + "Select a soldier or hero, then click the destination card header. Click again to deselect. Empty squads are removed.";
            ClearCards();
            if (cardTemplate == null || rowTemplate == null) return;

            string hint = Selected != null ? "\nSend here" : string.Empty;
            for (int i = 0; i < squads.Count; i++)
            {
                int index = i;
                Squad<RunUnit> squad = squads[i];
                RectTransform list = AddCard($"Squad {i + 1} - {squad.Members.Count}/{squad.Capacity} units" + hint, () => DropOnSquad(index));
                if (squad.Leader is RunHero leader) AddRow(list, Mark(leader) + "Captain: " + NameOf(leader), () => Select(leader));
                else AddRow(list, "Captain: None", null);
                foreach (RunUnit member in squad.Members)
                    if (member is RunSoldier soldier) AddRow(list, Mark(soldier) + NameOf(soldier), () => Select(soldier));
            }
            if (state.Army.CanAddSquad)
            {
                RectTransform list = AddCard("Create new squad" + hint, () => DropOnSquad(NewSquad));
                AddRow(list, "Select a soldier, then click here to create a squad.", null);
            }
            RectTransform reserveList = AddCard("Reserve heroes (out of battle)" + (Selected is RunHero ? "\nRemove captain" : string.Empty), () => DropOnReserve());
            if (reserve.Count == 0) AddRow(reserveList, "No reserve heroes (hire at the tavern).", null);
            foreach (RunHero hero in reserve) AddRow(reserveList, Mark(hero) + NameOf(hero), () => Select(hero));
        }

        string Mark(RunUnit unit) => unit == Selected ? "> " : string.Empty;

        RectTransform AddCard(string header, UnityEngine.Events.UnityAction onHeader)
        {
            RectTransform card = Instantiate(cardTemplate, cardArea);
            card.gameObject.SetActive(true);
            cards.Add(card.gameObject);
            Transform headerTransform = card.Find("Header");
            var button = headerTransform != null ? headerTransform.GetComponent<Button>() : null;
            if (button != null)
            {
                button.GetComponentInChildren<TMP_Text>(true).text = header;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(onHeader);
            }
            return card.Find("List") as RectTransform ?? card;
        }

        void AddRow(RectTransform list, string text, UnityEngine.Events.UnityAction action)
        {
            Button row = Instantiate(rowTemplate, list);
            row.gameObject.SetActive(true);
            row.GetComponentInChildren<TMP_Text>(true).text = text;
            row.interactable = action != null;
            if (action != null) row.onClick.AddListener(action);
        }

        void ClearCards()
        {
            foreach (GameObject card in cards)
            {
                if (card == null) continue;
                // 같은 프레임에 다시 그리므로 카드 수가 바로 맞도록 먼저 끈다.
                card.SetActive(false);
                Destroy(card);
            }
            cards.Clear();
        }
    }
}
