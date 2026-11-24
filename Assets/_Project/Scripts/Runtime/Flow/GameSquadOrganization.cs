using System.Collections.Generic;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.UI;
using TMPro;
using UnityEngine;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// Game 씬 준비 단계의 부대 편성 화면(D-8, UI-07). 1인칭 탐색 중 편성 키(기본 O)로 열고 Esc나 같은 키로 닫는다.
    /// 세 열이다: 부대 목록 | 고른 부대의 부대장과 병사 | 보관 중인 영웅과 병사. 줄을 누르면 바로 옮긴다(UI-07, 사용자 2026-11-23):
    /// 보관 병사는 고른 부대로 들어가고, 보관 영웅은 고른 부대의 부대장이 되며(있던 부대장은 보관으로), 가운데 줄은 보관으로 돌아간다.
    /// 가득 찬 부대에는 병사 줄이 흐려져 눌리지 않는다. 부대 목록 끝의 "새 부대"로 빈 부대를 만들고, 고른 부대의 삭제 단추로 없앤다
    /// (부대원은 모두 보관으로). 빈 부대는 남는다. 보관 인원은 씬에서 숨겨지고 전투에 나가지 않는다.
    /// <see cref="GameBattleController"/>와 같은 오브젝트에 붙어 그 런 연결을 쓴다.
    /// </summary>
    [RequireComponent(typeof(GameBattleController))]
    public sealed class GameSquadOrganization : MonoBehaviour
    {
        [SerializeField] GamePhaseView phaseView;
        [SerializeField] PreparationInteraction interaction;
        [SerializeField] GameObject panel;
        [Tooltip("1열 부대 목록(스크롤 내용).")]
        [SerializeField] RectTransform squadList;
        [Tooltip("2열 고른 부대의 부대장과 병사(스크롤 내용).")]
        [SerializeField] RectTransform memberList;
        [Tooltip("3열 보관 중인 영웅과 병사(스크롤 내용).")]
        [SerializeField] RectTransform storedList;
        [SerializeField] TMP_Text squadCount;
        [SerializeField] TMP_Text memberTitle;
        [SerializeField] TMP_Text memberCount;
        [SerializeField] TMP_Text storedCount;
        [SerializeField] OrganizationRowView rowTemplate;
        [Tooltip("고른 부대를 없애는 단추(편성 확정 왼쪽, 붉은 판). 부대가 없으면 눌리지 않는다.")]
        [SerializeField] UnityEngine.UI.Button deleteButton;
        readonly List<OrganizationRowView> rows = new List<OrganizationRowView>();
        GameBattleController battle;
        GameFlow flow;

        public bool IsOpen { get; private set; }
        /// <summary>마지막으로 거절된 이유(화면에는 보이지 않는다). 성공하면 빈 글자.</summary>
        public string Message { get; private set; } = string.Empty;
        /// <summary>고른 부대 순번. 부대가 없으면 -1.</summary>
        public int SelectedSquad { get; private set; } = -1;
        public string MemberTitleText => memberTitle != null ? memberTitle.text : string.Empty;
        public string MemberCountText => memberCount != null ? memberCount.text : string.Empty;

        public void Configure(GamePhaseView view, PreparationInteraction input, GameObject popup, RectTransform squads,
            RectTransform members, RectTransform stored, TMP_Text squadCountLabel, TMP_Text memberTitleLabel,
            TMP_Text memberCountLabel, TMP_Text storedCountLabel, OrganizationRowView row, UnityEngine.UI.Button delete)
        {
            deleteButton = delete;
            phaseView = view; interaction = input; panel = popup;
            squadList = squads; memberList = members; storedList = stored;
            squadCount = squadCountLabel; memberTitle = memberTitleLabel; memberCount = memberCountLabel; storedCount = storedCountLabel;
            rowTemplate = row;
        }

        void Awake()
        {
            battle = GetComponent<GameBattleController>();
            flow = GameSession.Flow;
            if (panel != null) panel.SetActive(false);
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (interaction == null) return;
            interaction.OrganizationRequested += OnRequested;
            interaction.OrganizationKeyInPanel += OnKeyInPanel;
        }

        void OnDisable()
        {
            if (interaction == null) return;
            interaction.OrganizationRequested -= OnRequested;
            interaction.OrganizationKeyInPanel -= OnKeyInPanel;
        }

        void OnRequested() => Open();

        // 편성 창은 연 키로 다시 닫는다(UI-07 공통 규칙). 다른 창이 열려 있을 때는 아무것도 하지 않는다.
        void OnKeyInPanel() { if (IsOpen) Close(); }

        RunState State => battle != null ? battle.RunLink.State : null;

        /// <summary>준비 중 1인칭 탐색에서 편성 화면을 연다. 전투 중·다른 화면·런이 없으면 false.</summary>
        public bool Open()
        {
            RunState state = State;
            if (IsOpen || flow.Phase != GamePhase.Preparation || phaseView == null || panel == null || state == null) return false;
            if (!phaseView.EnterPanel()) return false;
            IsOpen = true;
            Message = string.Empty;
            SelectedSquad = state.Army.Squads.Count > 0 ? 0 : -1;
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

        /// <summary>1열에서 부대를 고른다. 없는 번호면 false.</summary>
        public bool SelectSquad(int index)
        {
            RunState state = State;
            if (!IsOpen || state == null || index < 0 || index >= state.Army.Squads.Count) return false;
            SelectedSquad = index;
            Message = string.Empty;
            Rebuild();
            return true;
        }

        /// <summary>새 빈 부대를 맨 뒤에 만들고 고른다(부대 수 상한까지).</summary>
        public bool AddSquad()
        {
            if (!IsOpen || battle == null) return false;
            OrganizeCheck check = battle.RunLink.TryAddSquad(out int index);
            return Finish(check, () => SelectedSquad = index);
        }

        /// <summary>부대를 없앤다. 부대원은 모두 보관으로 간다.</summary>
        public bool RemoveSquad(int index)
        {
            if (!IsOpen || battle == null) return false;
            OrganizeCheck check = battle.RunLink.TryRemoveSquad(index);
            return Finish(check, () =>
            {
                int count = State.Army.Squads.Count;
                SelectedSquad = count == 0 ? -1 : Mathf.Clamp(SelectedSquad >= index ? SelectedSquad - 1 : SelectedSquad, 0, count - 1);
            });
        }

        /// <summary>부대 삭제 단추: 고른 부대를 없앤다.</summary>
        public bool RemoveSelectedSquad() => RemoveSquad(SelectedSquad);

        /// <summary>부대 삭제 단추에 거는 판(단추 이벤트는 값을 돌려받지 않는다).</summary>
        public void RemoveSelectedSquadFromButton() => RemoveSelectedSquad();

        /// <summary>
        /// 보관 중인 것을 고른 부대에 넣는다. 병사는 부대원으로, 영웅은 부대장으로(있던 부대장은 보관으로).
        /// </summary>
        public bool Assign(RunUnit unit)
        {
            RunState state = State;
            if (!IsOpen || state == null || SelectedSquad < 0 || SelectedSquad >= state.Army.Squads.Count) return false;
            OrganizeCheck check = unit switch
            {
                RunSoldier soldier => battle.RunLink.TryMoveSoldier(soldier, SelectedSquad),
                RunHero hero => battle.RunLink.TrySetCaptain(SelectedSquad, hero),
                _ => OrganizeCheck.NotInRun,
            };
            return Finish(check, null);
        }

        /// <summary>고른 부대의 부대원이나 부대장을 보관으로 돌린다.</summary>
        public bool Release(RunUnit unit)
        {
            RunState state = State;
            if (!IsOpen || state == null || SelectedSquad < 0 || SelectedSquad >= state.Army.Squads.Count) return false;
            Squad<RunUnit> squad = state.Army.Squads[SelectedSquad];
            OrganizeCheck check = unit switch
            {
                RunSoldier soldier => battle.RunLink.TryStoreSoldier(soldier),
                RunHero hero when squad.Leader == hero => battle.RunLink.TrySetCaptain(SelectedSquad, null),
                _ => OrganizeCheck.NotInRun,
            };
            return Finish(check, null);
        }

        bool Finish(OrganizeCheck check, System.Action onDone)
        {
            bool done = check == OrganizeCheck.Ok;
            if (done) onDone?.Invoke();
            Message = done ? string.Empty : Reason(check);
            Rebuild();
            return done;
        }

        static string Reason(OrganizeCheck check) => check switch
        {
            OrganizeCheck.BattlePending => "전투 중에는 편성할 수 없습니다.",
            OrganizeCheck.SameSquad => "이미 그 자리에 있습니다.",
            OrganizeCheck.SquadFull => "부대가 가득 찼거나 부대 수가 한도에 이르렀습니다.",
            OrganizeCheck.PlayerNotAllowed => "플레이어는 아직 부대장이 될 수 없습니다.",
            OrganizeCheck.NotReserve => "보관 중인 영웅만 부대장이 될 수 있습니다.",
            _ => "편성하지 못했습니다.",
        };

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

        /// <summary>열의 보이는 줄들(0: 부대, 1: 부대원, 2: 보관). 테스트와 확인용.</summary>
        public List<OrganizationRowView> RowsIn(int column)
        {
            RectTransform list = column == 0 ? squadList : column == 1 ? memberList : storedList;
            var found = new List<OrganizationRowView>();
            foreach (OrganizationRowView row in rows)
                if (row != null && row.gameObject.activeSelf && row.transform.parent == list) found.Add(row);
            return found;
        }

        void Rebuild()
        {
            RunState state = State;
            if (state == null) return;
            ClearRows();
            if (rowTemplate == null) return;
            IReadOnlyList<Squad<RunUnit>> squads = state.Army.Squads;
            if (SelectedSquad >= squads.Count) SelectedSquad = squads.Count - 1;
            if (SelectedSquad < 0 && squads.Count > 0) SelectedSquad = 0;

            // 1열: 부대 목록과 새 부대.
            if (squadCount != null) squadCount.text = $"{squads.Count} / {state.Army.MaxSquads}";
            for (int i = 0; i < squads.Count; i++)
            {
                int index = i;
                bool selected = i == SelectedSquad;
                AddRow(squadList).Show($"{i + 1}부대", $"{squads[i].Members.Count}/{squads[i].Capacity}", false, false, selected,
                    () => SelectSquad(index), dimWhenIdle: false);
            }
            if (deleteButton != null) deleteButton.interactable = SelectedSquad >= 0;
            if (state.Army.CanAddSquad) AddRow(squadList).Show("+ 새 부대", null, false, false, false, () => AddSquad());

            // 2열: 고른 부대의 부대장과 병사.
            Squad<RunUnit> chosen = SelectedSquad >= 0 ? squads[SelectedSquad] : null;
            if (memberTitle != null) memberTitle.text = chosen != null ? $"{SelectedSquad + 1}부대" : "부대 없음";
            if (memberCount != null) memberCount.text = chosen != null ? $"{chosen.Members.Count} / {chosen.Capacity}" : string.Empty;
            if (chosen != null)
            {
                if (chosen.Leader is RunHero leader)
                    AddRow(memberList).Show(NameOf(leader), null, true, true, false, () => Release(leader));
                else
                    AddRow(memberList).Show("부대장 공석", null, true, true, false, null);
                foreach (RunUnit member in chosen.Members)
                    if (member is RunSoldier soldier)
                        AddRow(memberList).Show(NameOf(soldier), null, true, false, false, () => Release(soldier));
            }

            // 3열: 보관 중인 영웅(부대장 후보)과 병사.
            IReadOnlyList<RunHero> heroes = state.ReserveHeroes;
            IReadOnlyList<RunSoldier> stored = state.StoredSoldiers;
            if (storedCount != null) storedCount.text = (heroes.Count + stored.Count).ToString();
            foreach (RunHero hero in heroes)
                AddRow(storedList).Show(NameOf(hero), null, true, false, false, () => Assign(hero), chosen != null);
            bool room = chosen != null && !chosen.IsFull;
            foreach (RunSoldier soldier in stored)
                AddRow(storedList).Show(NameOf(soldier), null, true, false, false, () => Assign(soldier), room);
        }

        static string NameOf(RunUnit unit) => unit switch
        {
            RunSoldier soldier => soldier.Definition.DisplayName,
            RunHero hero => hero.Definition.DisplayName,
            _ => string.Empty,
        };

        OrganizationRowView AddRow(RectTransform list)
        {
            OrganizationRowView row = Instantiate(rowTemplate, list);
            row.gameObject.SetActive(true);
            rows.Add(row);
            return row;
        }

        void ClearRows()
        {
            foreach (OrganizationRowView row in rows)
            {
                if (row == null) continue;
                // 같은 프레임에 다시 그리므로 줄 수가 바로 맞도록 먼저 끈다.
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
            rows.Clear();
        }
    }
}
