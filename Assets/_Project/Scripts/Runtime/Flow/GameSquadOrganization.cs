using System.Collections.Generic;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// Game 씬 준비 단계의 부대 편성 화면(D-8, Q-45·Q-47). 1인칭 탐색 중 편성 키(기본 O)로 열고 Esc나 닫기 버튼으로 닫는다.
    /// 왼쪽 열은 보낼 부대(맨 위 줄을 눌러 바꾼다)의 잡졸이고, 잡졸을 누르면 오른쪽 열에서 고른 받을 부대로 한 명 옮긴다.
    /// 오른쪽 열은 받을 부대(또는 새 부대) 선택과 보낼 부대의 대장 지정·해제다. 대장은 대기 영웅만 맡는다(플레이어 제외, Q-32 임시).
    /// 원래 부대가 비면 그 부대는 없어지고 대장은 대기로 돌아간다. 런과 씬 부대는 <see cref="GameRunLink"/>가 함께 바꾼다.
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
        [SerializeField] RectTransform sourceList;
        [SerializeField] RectTransform targetList;
        [SerializeField] Button rowTemplate;
        readonly List<Button> rows = new List<Button>();
        GameBattleController battle;
        GameFlow flow;

        public bool IsOpen { get; private set; }
        public string Message { get; private set; } = string.Empty;
        public string StatusText => status != null ? status.text : string.Empty;
        public string BodyText => body != null ? body.text : string.Empty;
        /// <summary>보낼 부대 순번.</summary>
        public int Source { get; private set; }
        /// <summary>받을 부대 순번. -1이면 새 부대.</summary>
        public int Target { get; private set; }
        public int SourceRows => sourceList != null ? ActiveChildren(sourceList) : 0;
        public int TargetRows => targetList != null ? ActiveChildren(targetList) : 0;

        public void Configure(GamePhaseView view, PreparationInteraction input, GameObject popup, TMP_Text statusLabel,
            TMP_Text bodyLabel, RectTransform source, RectTransform target, Button template)
        {
            phaseView = view; interaction = input; panel = popup; status = statusLabel; body = bodyLabel;
            sourceList = source; targetList = target; rowTemplate = template;
        }

        void Awake()
        {
            battle = GetComponent<GameBattleController>();
            flow = GameSession.Flow;
            if (panel != null) panel.SetActive(false);
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
            Source = 0;
            Target = state.Army.Squads.Count > 1 ? 1 : NewSquad;
            panel.SetActive(true);
            Rebuild();
            return true;
        }

        public void Close()
        {
            if (phaseView != null) phaseView.ExitPanel();
            Hide();
        }

        /// <summary>보낼 부대를 고른다. 받을 부대와 같으면 받을 부대를 다른 곳으로 바꾼다.</summary>
        public bool SelectSource(int squad)
        {
            RunState state = State;
            if (!IsOpen || state == null || squad < 0 || squad >= state.Army.Squads.Count) return false;
            Source = squad;
            if (Target == Source) Target = FirstOther(state, Source);
            Message = string.Empty;
            Rebuild();
            return true;
        }

        /// <summary>보낼 부대를 다음 부대로 바꾼다(맨 위 줄).</summary>
        public bool NextSource()
        {
            RunState state = State;
            return state != null && state.Army.Squads.Count > 0 && SelectSource((Source + 1) % state.Army.Squads.Count);
        }

        /// <summary>받을 부대를 고른다. -1이면 새 부대. 보낼 부대와 같으면 false.</summary>
        public bool SelectTarget(int squad)
        {
            RunState state = State;
            if (!IsOpen || state == null || squad < NewSquad || squad >= state.Army.Squads.Count || squad == Source) return false;
            Target = squad;
            Message = string.Empty;
            Rebuild();
            return true;
        }

        /// <summary>보낼 부대의 index번째 잡졸을 받을 부대로 옮긴다.</summary>
        public bool TryMove(int index)
        {
            RunState state = State;
            if (!IsOpen || state == null || Source >= state.Army.Squads.Count) return false;
            List<RunSoldier> soldiers = SoldiersOf(state.Army.Squads[Source]);
            if (index < 0 || index >= soldiers.Count) return false;
            RunSoldier soldier = soldiers[index];
            int sourceCount = state.Army.Squads.Count;
            OrganizeCheck check = battle.RunLink.TryMoveSoldier(soldier, Target, out int movedTo);
            if (check == OrganizeCheck.Ok)
            {
                bool removed = state.Army.Squads.Count < sourceCount + (Target == NewSquad ? 1 : 0);
                Message = $"{soldier.Definition.DisplayName} → 부대 {movedTo + 1}" + (removed ? " · 빈 부대를 없앴습니다(대장은 대기)" : string.Empty);
                if (removed)
                {
                    Source = Mathf.Clamp(movedTo, 0, state.Army.Squads.Count - 1);
                    Target = FirstOther(state, Source);
                }
                else Target = movedTo;
            }
            else Message = Reason(check);
            Rebuild();
            return check == OrganizeCheck.Ok;
        }

        /// <summary>대기 영웅의 index번째를 보낼 부대의 대장으로 정한다. 원래 대장은 대기로 돌아간다.</summary>
        public bool TryAssignCaptain(int index)
        {
            RunState state = State;
            if (!IsOpen || state == null) return false;
            IReadOnlyList<RunHero> reserve = state.ReserveHeroes;
            if (index < 0 || index >= reserve.Count) return false;
            OrganizeCheck check = battle.RunLink.TrySetCaptain(Source, reserve[index]);
            Message = check == OrganizeCheck.Ok ? $"부대 {Source + 1} 대장: {reserve[index].Character.Definition.DisplayName}" : Reason(check);
            Rebuild();
            return check == OrganizeCheck.Ok;
        }

        /// <summary>보낼 부대의 대장을 비운다. 대장은 대기 영웅이 된다.</summary>
        public bool TryClearCaptain()
        {
            if (!IsOpen || State == null) return false;
            OrganizeCheck check = battle.RunLink.TrySetCaptain(Source, null);
            Message = check == OrganizeCheck.Ok ? $"부대 {Source + 1} 대장을 대기로 돌렸습니다." : Reason(check);
            Rebuild();
            return check == OrganizeCheck.Ok;
        }

        static string Reason(OrganizeCheck check) => check switch
        {
            OrganizeCheck.BattlePending => "전투 중에는 편성을 바꿀 수 없습니다.",
            OrganizeCheck.SameSquad => "이미 그 부대에 있습니다.",
            OrganizeCheck.SquadFull => "받을 부대가 꽉 찼거나 부대 수 상한입니다.",
            OrganizeCheck.PlayerNotAllowed => "플레이어는 아직 대장을 맡을 수 없습니다.",
            OrganizeCheck.NotReserve => "대기 중인 영웅만 대장이 될 수 있습니다.",
            _ => "편성을 바꾸지 못했습니다.",
        };

        static int FirstOther(RunState state, int source)
        {
            for (int i = 0; i < state.Army.Squads.Count; i++) if (i != source) return i;
            return NewSquad;
        }

        static List<RunSoldier> SoldiersOf(Squad<RunUnit> squad)
        {
            var soldiers = new List<RunSoldier>();
            foreach (RunUnit member in squad.Members) if (member is RunSoldier soldier) soldiers.Add(soldier);
            return soldiers;
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

        void Rebuild()
        {
            RunState state = State;
            if (state == null) return;
            IReadOnlyList<Squad<RunUnit>> squads = state.Army.Squads;
            if (squads.Count == 0) { Close(); return; }
            Source = Mathf.Clamp(Source, 0, squads.Count - 1);
            if (Target >= squads.Count || Target == Source) Target = FirstOther(state, Source);
            if (Target == NewSquad && !state.Army.CanAddSquad && squads.Count > 1) Target = FirstOther(state, Source);
            IReadOnlyList<RunHero> reserve = state.ReserveHeroes;
            if (status != null)
                status.text = $"부대 {squads.Count}개 · 대기 영웅 {reserve.Count}명\n보내는 부대 {Source + 1} → 받는 부대 "
                    + (Target == NewSquad ? "새 부대" : (Target + 1).ToString());
            if (body != null)
                body.text = (string.IsNullOrEmpty(Message) ? string.Empty : Message + "\n")
                    + "왼쪽 잡졸을 누르면 오른쪽에서 고른 부대로 한 명씩 옮깁니다. 빈 부대는 없어집니다.";
            ClearRows();
            if (rowTemplate == null) return;

            Squad<RunUnit> source = squads[Source];
            AddRow(sourceList, $"▶ 보내는 부대 {Source + 1} · {source.Members.Count}/{source.Capacity}명 (눌러서 바꾸기)", () => NextSource());
            List<RunSoldier> soldiers = SoldiersOf(source);
            if (soldiers.Count == 0) AddRow(sourceList, "잡졸이 없습니다.", null);
            for (int i = 0; i < soldiers.Count; i++)
            {
                int index = i;
                AddRow(sourceList, $"{soldiers[i].Definition.DisplayName} #{soldiers[i].Id} 옮기기", () => TryMove(index));
            }

            for (int i = 0; i < squads.Count; i++)
            {
                if (i == Source) continue;
                int index = i;
                string mark = i == Target ? "[받음] " : string.Empty;
                AddRow(targetList, $"{mark}부대 {i + 1} · {squads[i].Members.Count}/{squads[i].Capacity}명 · 대장 {LeaderName(squads[i])}",
                    () => SelectTarget(index));
            }
            if (state.Army.CanAddSquad)
                AddRow(targetList, (Target == NewSquad ? "[받음] " : string.Empty) + "새 부대 만들기", () => SelectTarget(NewSquad));
            AddRow(targetList, $"부대 {Source + 1} 대장: {LeaderName(source)}" + (source.Leader != null ? " (눌러서 대기로)" : string.Empty),
                source.Leader != null ? () => TryClearCaptain() : (UnityEngine.Events.UnityAction)null);
            if (reserve.Count == 0) AddRow(targetList, "대기 영웅이 없습니다(주점에서 고용).", null);
            for (int i = 0; i < reserve.Count; i++)
            {
                int index = i;
                AddRow(targetList, $"대장으로 지정: {reserve[i].Character.Definition.DisplayName}", () => TryAssignCaptain(index));
            }
        }

        static string LeaderName(Squad<RunUnit> squad) =>
            squad.Leader is RunHero hero ? hero.Character.Definition.DisplayName : "없음";

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
    }
}
