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
            Message = Selected == null ? string.Empty : $"{NameOf(Selected)} 선택 -> 옮길 카드의 제목을 클릭하세요.";
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
                Message = done ? $"{NameOf(soldier)} -> {movedTo + 1}부대" + (removed ? " - 빈 부대 삭제 (대장은 예비로)" : string.Empty)
                    : Reason(check);
            }
            else if (Selected is RunHero hero)
            {
                if (index == NewSquad) { Message = "영웅은 병사가 있는 부대만 이끌 수 있습니다."; done = false; }
                else
                {
                    Squad<RunUnit> leading = state.LeadingSquadOf(hero);
                    OrganizeCheck check = leading == state.Army.Squads[index] ? OrganizeCheck.SameSquad : OrganizeCheck.Ok;
                    if (check == OrganizeCheck.Ok && leading != null) check = battle.RunLink.TrySetCaptain(state.IndexOf(leading), null);
                    if (check == OrganizeCheck.Ok) check = battle.RunLink.TrySetCaptain(index, hero);
                    done = check == OrganizeCheck.Ok;
                    Message = done ? $"{index + 1}부대 대장: {NameOf(hero)}" : Reason(check);
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
                Message = done ? $"{NameOf(hero)}을(를) 예비로 보냈습니다." : Reason(check);
            }
            else Message = Selected is RunSoldier ? "병사는 예비로 보낼 수 없습니다. 다른 부대 카드를 고르세요." : "이미 예비에 있습니다.";
            if (done) Selected = null;
            Rebuild();
            return done;
        }

        static string Reason(OrganizeCheck check) => check switch
        {
            OrganizeCheck.BattlePending => "전투 중에는 편성할 수 없습니다.",
            OrganizeCheck.SameSquad => "이미 그 부대에 있습니다.",
            OrganizeCheck.SquadFull => "부대가 가득 찼거나 부대 수가 한도에 이르렀습니다.",
            OrganizeCheck.PlayerNotAllowed => "플레이어는 아직 대장이 될 수 없습니다.",
            OrganizeCheck.NotReserve => "예비 영웅만 대장이 될 수 있습니다.",
            _ => "편성하지 못했습니다.",
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
                status.text = $"부대 {squads.Count} - 예비 영웅 {reserve.Count} - 선택: " + (Selected != null ? NameOf(Selected) : "없음");
            if (body != null)
                body.text = (string.IsNullOrEmpty(Message) ? string.Empty : Message + "\n")
                    + "병사나 영웅을 고른 뒤 옮길 카드의 제목을 클릭하세요. 다시 클릭하면 선택이 풀립니다. 빈 부대는 사라집니다.";
            ClearCards();
            if (cardTemplate == null || rowTemplate == null) return;

            string hint = Selected != null ? "\n여기로 보내기" : string.Empty;
            for (int i = 0; i < squads.Count; i++)
            {
                int index = i;
                Squad<RunUnit> squad = squads[i];
                RectTransform list = AddCard($"{i + 1}부대 - {squad.Members.Count}/{squad.Capacity}명" + hint, () => DropOnSquad(index));
                if (squad.Leader is RunHero leader) AddRow(list, Mark(leader) + "대장: " + NameOf(leader), () => Select(leader));
                else AddRow(list, "대장: 없음", null);
                foreach (RunUnit member in squad.Members)
                    if (member is RunSoldier soldier) AddRow(list, Mark(soldier) + NameOf(soldier), () => Select(soldier));
            }
            if (state.Army.CanAddSquad)
            {
                RectTransform list = AddCard("새 부대 만들기" + hint, () => DropOnSquad(NewSquad));
                AddRow(list, "병사를 고른 뒤 여기를 클릭하면 부대를 만듭니다.", null);
            }
            RectTransform reserveList = AddCard("예비 영웅 (전투 불참)" + (Selected is RunHero ? "\n대장 해제" : string.Empty), () => DropOnReserve());
            if (reserve.Count == 0) AddRow(reserveList, "예비 영웅이 없습니다 (주점에서 고용).", null);
            foreach (RunHero hero in reserve) AddRow(reserveList, Mark(hero) + NameOf(hero), () => Select(hero));
            FitCardHeights();
        }

        /// <summary>보이는 카드 중 index번째(부대 순서, 그다음 새 부대·대기 카드). 없으면 null.</summary>
        public RectTransform CardAt(int index)
        {
            foreach (GameObject card in cards)
            {
                if (card == null || !card.activeSelf) continue;
                if (index-- == 0) return (RectTransform)card.transform;
            }
            return null;
        }

        // PREP-09: 부대가 대장 + 20명까지라 줄이 카드 영역보다 길어질 수 있다. 카드마다 줄 수에 맞는 높이를 주면
        // 카드 영역(세로 스크롤)이 가장 긴 카드만큼 늘어난다. 짧은 카드도 보이는 영역 높이까지는 채운다.
        void FitCardHeights()
        {
            var viewport = cardArea != null ? cardArea.parent as RectTransform : null;
            var areaLayout = cardArea != null ? cardArea.GetComponent<HorizontalLayoutGroup>() : null;
            float fill = viewport != null ? viewport.rect.height - (areaLayout != null ? areaLayout.padding.vertical : 0) : 0f;
            var rowSize = rowTemplate != null ? rowTemplate.GetComponent<LayoutElement>() : null;
            float row = rowSize != null && rowSize.preferredHeight > 0f ? rowSize.preferredHeight : 26f;
            foreach (GameObject card in cards)
            {
                if (card == null || !card.activeSelf) continue;
                var size = card.GetComponent<LayoutElement>();
                var list = card.transform.Find("List") as RectTransform;
                if (size == null || list == null) continue;
                var listLayout = list.GetComponent<VerticalLayoutGroup>();
                int rows = list.childCount;
                float spacing = listLayout != null ? listLayout.spacing : 0f;
                float padding = listLayout != null ? listLayout.padding.vertical : 0f;
                // 목록은 카드 안에서 머리 버튼 자리(위)와 아래 여백만큼 작다.
                float frame = list.offsetMin.y - list.offsetMax.y;
                float needed = frame + padding + rows * row + Mathf.Max(0, rows - 1) * spacing;
                size.minHeight = Mathf.Max(needed, fill);
            }
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
