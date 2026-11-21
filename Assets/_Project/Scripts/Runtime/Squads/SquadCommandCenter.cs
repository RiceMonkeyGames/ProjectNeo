using System;
using System.Collections.Generic;
using ProjectNeo.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectNeo.Squads
{
    [Serializable]
    public struct SquadRoster
    {
        public UnitCombatAI[] members;
    }

    /// <summary>
    /// 부대 지시를 씬의 유닛 행동으로 옮긴다. 규칙은 SquadCommander, 유닛 행동은 UnitCombatAI가 맡고
    /// 여기서는 편성·대형 자리 배정·도착 확인만 한다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class SquadCommandCenter : MonoBehaviour
    {
        [SerializeField] SquadConfig config;
        [SerializeField] SquadCommandSettings settings;
        [SerializeField] SquadRoster[] rosters = new SquadRoster[0];
        Army<UnitCombatAI> army;
        readonly HashSet<UnitCombatAI> detached = new HashSet<UnitCombatAI>();
        readonly List<Vector3> slotBuffer = new List<Vector3>();
        readonly List<UnitCombatAI> commandable = new List<UnitCombatAI>();
        readonly List<UnitCombatAI> countBuffer = new List<UnitCombatAI>();
        // 부대원이 바뀐 분대를 프레임 끝에 한 번만 다시 세운다(COMBAT-08). 부대 번호가 당겨져도 따라가도록 분대 객체로 기억한다.
        readonly Dictionary<Squad<UnitCombatAI>, RosterWatch> rosterWatch = new Dictionary<Squad<UnitCombatAI>, RosterWatch>();
        readonly List<Vector3> postBuffer = new List<Vector3>();
        readonly List<bool> hasPostBuffer = new List<bool>();

        sealed class RosterWatch
        {
            public int LastCount = -1;
            public bool Dirty;
            public float RefillAt = float.PositiveInfinity;
        }

        /// <summary>
        /// 자리가 진형 간격의 이 비율보다 가까우면 그 부대원은 제자리로 본다. 도착한 유닛은 목적지 근처(0.35m 안팎)에 서므로
        /// 그만큼은 넘고, 이웃 자리와 헷갈리지 않게 간격의 절반보다는 작다.
        /// </summary>
        public const float KeepPostFraction = 0.45f;
        readonly List<int> lineSquads = new List<int>();
        bool reformSuppressed;
        public SquadCommander Commander { get; private set; }
        public SquadCommandSettings Settings => settings;
        public IReadOnlyList<Squad<UnitCombatAI>> Squads => army.Squads;
        /// <summary>전열을 끄는 동안의 자리 미리보기(COMBAT-08).</summary>
        public FormationPreview Preview { get; } = new FormationPreview();
        /// <summary>
        /// 그 지점이 성벽 위면 주어진 방향으로 잰 통로 폭(m), 아니면 +∞(COMBAT-08). 줄 수 상한으로 쓴다.
        /// 성벽을 아는 쪽(Flow)이 연결한다. 없으면 길찾기 메시로 잰 깊이만 쓴다.
        /// </summary>
        public Func<Vector3, Vector3, float> WalkwayWidth;

        public void Configure(SquadConfig squadConfig, SquadCommandSettings commandSettings, SquadRoster[] squadRosters)
        {
            config = squadConfig; settings = commandSettings; rosters = squadRosters;
        }

        public SquadConfig Config => config;

        /// <summary>실행 중에 만든 유닛으로 편성한다. 이 컴포넌트의 Awake보다 먼저 불러야 한다.</summary>
        public void ConfigureRosters(SquadRoster[] squadRosters) => rosters = squadRosters;

        void Awake()
        {
            army = new Army<UnitCombatAI>(config);
            foreach (SquadRoster roster in rosters)
            {
                Squad<UnitCombatAI> squad = army.TryAddSquad();
                if (squad == null) { Debug.LogError("Too many squads for the squad config.", this); break; }
                foreach (UnitCombatAI member in roster.members)
                    if (member != null && !squad.TryAdd(member))
                        Debug.LogError("Squad is full or has a duplicate member.", this);
            }
            Commander = new SquadCommander(army.Squads.Count);
            Commander.OrderChanged += Apply;
            Commander.FormationChanged += Reform;
        }

        // 전투 시작 때 아무 지시도 받지 않은 부대는 중지 상태다.
        void Start()
        {
            for (int i = 0; i < army.Squads.Count; i++) Apply(i, Commander.OrderOf(i));
        }

        /// <summary>
        /// 실행 중에 빈 부대를 맨 뒤에 더한다(D-0b). 부대 수 상한이면 -1. 새 부대는 중지 상태다.
        /// </summary>
        public int AddSquad()
        {
            if (army.TryAddSquad() == null) return -1;
            return Commander.AddSquad();
        }

        /// <summary>
        /// 실행 중에 만든 유닛을 부대에 넣는다(D-0b). 자리가 없거나 이미 있으면 false. 지시는 바꾸지 않으므로
        /// 부르는 쪽이 유닛을 부대 쪽으로 보낸다.
        /// </summary>
        public bool AddMember(int squad, UnitCombatAI member)
        {
            if (member == null || squad < 0 || squad >= army.Squads.Count) return false;
            foreach (Squad<UnitCombatAI> other in army.Squads)
                foreach (UnitCombatAI existing in other.Members)
                    if (existing == member) return false;
            if (!army.Squads[squad].TryAdd(member)) return false;
            MarkRosterChanged(army.Squads[squad]);
            return true;
        }

        /// <summary>
        /// 부대원 하나를 부대에서 뺀다(FLOW-02 저장된 편성 되살리기). 대장이었으면 대장 자리도 비운다.
        /// 씬 오브젝트는 건드리지 않으므로 부르는 쪽이 없앤다. 부대에 없었으면 false.
        /// </summary>
        public bool RemoveMember(UnitCombatAI member)
        {
            if (member == null) return false;
            bool removed = false;
            foreach (Squad<UnitCombatAI> squad in army.Squads)
            {
                if (squad.Leader == member) { squad.Leader = null; removed = true; }
                if (squad.Remove(member)) { removed = true; MarkRosterChanged(squad); }
            }
            detached.Remove(member);
            return removed;
        }

        /// <summary>
        /// 부대원을 다른 부대로 옮긴다(D-8 편성). 받을 부대가 꽉 찼으면 그 부대의 죽은 부대원(남아 있는 몸)을 먼저 빼서 자리를 만든다.
        /// 그래도 자리가 없거나 부대원이 아니면 false이고 바뀌지 않는다. 지시는 바꾸지 않으므로 부르는 쪽이 유닛을 부대 쪽으로 보낸다.
        /// </summary>
        public bool MoveMember(UnitCombatAI member, int squad)
        {
            if (member == null || squad < 0 || squad >= army.Squads.Count) return false;
            Squad<UnitCombatAI> source = SquadOf(member);
            Squad<UnitCombatAI> target = army.Squads[squad];
            if (source == null) return false;
            if (source == target) return true;
            if (target.IsFull) RemoveDead(target);
            if (target.IsFull) return false;
            source.Remove(member);
            target.TryAdd(member);
            MarkRosterChanged(source); MarkRosterChanged(target);
            return true;
        }

        /// <summary>
        /// 부대 하나를 없앤다(D-8 편성으로 빈 부대가 생길 때). 살아 있는 부대원이 남아 있으면 false. 남은 몸은 부대에서 빠진다.
        /// 뒤의 부대 번호는 하나씩 당겨진다.
        /// </summary>
        public bool RemoveSquad(int squad)
        {
            if (squad < 0 || squad >= army.Squads.Count) return false;
            Squad<UnitCombatAI> target = army.Squads[squad];
            foreach (UnitCombatAI member in target.Members) if (!IsDeadBody(member)) return false;
            if (target.Leader != null && !IsDeadBody(target.Leader)) return false;
            army.RemoveSquad(target);
            return Commander.RemoveSquad(squad);
        }

        /// <summary>
        /// 죽은 부대원과 죽은 부대장을 모든 부대에서 뺀다(C-3, Q-50). 몸은 씬에 그대로 남고 부대 자리만 비운다. 뺀 수를 돌려준다.
        /// </summary>
        public int RemoveFallen()
        {
            int count = 0;
            foreach (Squad<UnitCombatAI> squad in army.Squads)
            {
                count -= squad.Members.Count;
                RemoveDead(squad);
                count += squad.Members.Count;
                if (squad.Leader != null && IsDeadBody(squad.Leader)) { squad.Leader = null; count--; }
            }
            return -count;
        }

        /// <summary>부대 대장을 정한다(D-8). null이면 비운다. 없는 번호면 false.</summary>
        public bool SetLeader(int squad, UnitCombatAI leader)
        {
            if (squad < 0 || squad >= army.Squads.Count) return false;
            army.Squads[squad].Leader = leader;
            MarkRosterChanged(army.Squads[squad]);
            return true;
        }

        Squad<UnitCombatAI> SquadOf(UnitCombatAI member)
        {
            foreach (Squad<UnitCombatAI> squad in army.Squads)
                foreach (UnitCombatAI existing in squad.Members)
                    if (existing == member) return squad;
            return null;
        }

        // 준비 중에는 유닛 컴포넌트가 꺼져 있을 수 있어 켜짐 여부가 아니라 체력으로만 죽음을 본다.
        static bool IsDeadBody(UnitCombatAI member)
        {
            if (member == null) return true;
            CharacterActor actor = member.Actor != null ? member.Actor : member.GetComponent<CharacterActor>();
            return actor == null || actor.Health == null || actor.Health.IsDead;
        }

        static void RemoveDead(Squad<UnitCombatAI> squad)
        {
            var dead = new List<UnitCombatAI>();
            foreach (UnitCombatAI member in squad.Members) if (IsDeadBody(member)) dead.Add(member);
            foreach (UnitCombatAI member in dead) squad.Remove(member);
        }

        /// <summary>
        /// 부대가 모여 있는 곳. 지시한 목적지가 있으면 그곳, 없으면 살아 있는 부대원의 평균 위치다. 아무도 없으면 false.
        /// </summary>
        public bool TryGetGatherPoint(int squad, out Vector3 point)
        {
            point = default;
            if (squad < 0 || squad >= army.Squads.Count) return false;
            SquadOrder order = Commander.OrderOf(squad);
            if (order.HasDestination) { point = order.Destination; return true; }
            int count = 0;
            foreach (UnitCombatAI member in army.Squads[squad].Members)
            {
                if (!IsAlive(member) || detached.Contains(member)) continue;
                point += member.transform.position;
                count++;
            }
            if (count == 0) return false;
            point /= count;
            return true;
        }

        /// <summary>
        /// 부대원을 지시에서 뗀다(사기로 도망, B-8.2). 뗀 부대원은 부대에 남아 살아 있는 수에는 들지만
        /// 부대 지시·대형·모이는 곳 계산에서 빠진다. 이미 떼었거나 부대원이 아니면 false.
        /// </summary>
        public bool Detach(UnitCombatAI member)
        {
            if (member == null || !Contains(member)) return false;
            return detached.Add(member);
        }

        /// <summary>뗀 부대원을 다시 지시를 받게 한다. 지시는 바꾸지 않으므로 부르는 쪽이 부대 쪽으로 보낸다.</summary>
        public bool Reattach(UnitCombatAI member) => member != null && detached.Remove(member);

        public bool IsDetached(UnitCombatAI member) => member != null && detached.Contains(member);

        bool Contains(UnitCombatAI member)
        {
            foreach (Squad<UnitCombatAI> squad in army.Squads)
            {
                if (squad.Leader == member) return true;
                foreach (UnitCombatAI existing in squad.Members)
                    if (existing == member) return true;
            }
            return false;
        }

        public int AliveCount(int squad)
        {
            int count = 0;
            foreach (UnitCombatAI member in army.Squads[squad].Members) if (IsAlive(member)) count++;
            return count;
        }

        bool IsMoving(UnitCombatAI member) =>
            IsAlive(member) && !detached.Contains(member) && member.Directive == UnitDirective.Move;

        static bool IsAlive(UnitCombatAI member) => member != null && member.isActiveAndEnabled
            && member.Actor.Health != null && !member.Actor.Health.IsDead;

        // 지시를 받는 살아 있는 부대원. 대장(D-3 고용 영웅)도 부대 지시를 따르며 대형 맨 앞 자리에 선다.
        List<UnitCombatAI> CollectCommandable(int index, List<UnitCombatAI> output)
        {
            output.Clear();
            UnitCombatAI leader = army.Squads[index].Leader;
            if (IsAlive(leader) && !detached.Contains(leader)) output.Add(leader);
            foreach (UnitCombatAI member in army.Squads[index].Members)
                if (IsAlive(member) && !detached.Contains(member) && member != leader) output.Add(member);
            return output;
        }

        /// <summary>지시를 받는 살아 있는 인원(대장 포함, 도망 중 제외).</summary>
        public int CommandableCount(int squad) =>
            squad < 0 || squad >= army.Squads.Count ? 0 : CollectCommandable(squad, countBuffer).Count;

        // ── 진형(COMBAT-08) ─────────────────────────────────────

        /// <summary>
        /// 진형 <paramref name="kind"/>, 전열 폭 <paramref name="width"/>(음수면 기본)로 <paramref name="count"/>명이 설 자리.
        /// 기준점 앞뒤로 걸을 수 있는 깊이와 성벽 통로 폭에 들어가는 줄 수로 자르고, 자른 만큼 줄을 옆으로 늘린다.
        /// 걸을 수 없는 자리는 가까운 걸을 수 있는 곳으로, 그것도 없으면 기준점으로 붙인다.
        /// </summary>
        public void PlanSlots(FormationKind kind, float width, Vector3 anchor, float facing, bool frontAnchored, int count,
            List<Vector3> output)
        {
            output.Clear();
            if (count <= 0) return;
            float spacing = settings.Spacing(kind);
            int files = kind == FormationKind.Column ? settings.ColumnFiles
                : width < 0f ? settings.FormationColumns
                : FormationLayout.FilesForWidth(width, spacing, count);
            Vector3 forward = Quaternion.Euler(0f, facing, 0f) * Vector3.forward;
            float forwardRoom = Room(anchor, forward), backRoom = Room(anchor, -forward);
            float depth = forwardRoom + backRoom;
            if (WalkwayWidth != null)
                depth = Mathf.Min(depth, Mathf.Max(0f, WalkwayWidth(anchor, forward) - 2f * AgentRadius()));
            Vector2Int shape = FormationLayout.Shape(count, files, spacing, depth);
            float offset = FormationLayout.CenterOffset(shape.y, spacing, frontAnchored, forwardRoom, backRoom);
            output.AddRange(FormationLayout.Slots(anchor + forward * offset, facing, count, shape.x, spacing));
            for (int i = 0; i < output.Count; i++)
                output[i] = NavMesh.SamplePosition(output[i], out NavMeshHit hit, settings.NavigationSnapDistance, NavMesh.AllAreas)
                    && Mathf.Abs(hit.position.y - anchor.y) <= settings.VerticalSnapTolerance ? hit.position : anchor;
        }

        /// <summary>부대 하나의 지금 진형·폭으로 설 자리.</summary>
        public void PlanSlots(int squad, Vector3 anchor, float facing, bool frontAnchored, int count, List<Vector3> output) =>
            PlanSlots(Commander.FormationOf(squad), Commander.WidthOf(squad), anchor, facing, frontAnchored, count, output);

        // 기준점에서 그 방향으로 걸을 수 있는 거리. 재는 거리 안에서 막히지 않으면 +∞.
        float Room(Vector3 anchor, Vector3 direction)
        {
            if (!NavMesh.SamplePosition(anchor, out NavMeshHit start, settings.NavigationSnapDistance, NavMesh.AllAreas))
                return float.PositiveInfinity;
            float probe = settings.DepthProbeDistance;
            if (!NavMesh.Raycast(start.position, start.position + direction * probe, out NavMeshHit edge, NavMesh.AllAreas))
                return float.PositiveInfinity;
            return edge.distance;
        }

        static float AgentRadius() => NavMesh.GetSettingsCount() > 0 ? NavMesh.GetSettingsByIndex(0).agentRadius : 0.5f;

        /// <summary>
        /// 땅에 그은 선(<paramref name="start"/>→<paramref name="end"/>)에 선택한 부대를 세운다. 여러 부대면 선을 똑같이 나눠
        /// 왼쪽부터 부대 번호 순으로 나란히 선다. 선 길이가 각 부대의 전열 폭이 되고, 선에 수직으로 보는 쪽을 바라본다.
        /// <paramref name="check"/>가 거절 문구를 돌려주면 아무 부대도 움직이지 않는다. 지시받은 부대 수.
        /// </summary>
        public int IssueLine(Vector3 start, Vector3 end, Vector3 viewForward, SquadOrderKind kind,
            Func<Vector3, string> check, out string rejected)
        {
            rejected = null;
            float facing = FormationLayout.FacingFromLine(start, end, viewForward);
            CollectSelected(lineSquads);
            if (lineSquads.Count == 0) return 0;
            var anchors = new Vector3[lineSquads.Count];
            var widths = new float[lineSquads.Count];
            for (int k = 0; k < lineSquads.Count; k++)
            {
                LineSegment(start, end, facing, k, out anchors[k], out widths[k]);
                rejected = check?.Invoke(anchors[k]);
                if (!string.IsNullOrEmpty(rejected)) return 0;
            }
            int issued = 0;
            for (int k = 0; k < lineSquads.Count; k++)
            {
                // 폭을 먼저 바꿔야 새 지시의 자리가 새 폭으로 잡힌다. 지시가 거절되면 폭을 되돌린다.
                float previous = Commander.WidthOf(lineSquads[k]);
                SetWidthQuietly(lineSquads[k], widths[k]);
                if (Commander.IssueTo(lineSquads[k], new SquadOrder(kind, anchors[k], facing, true))) issued++;
                else SetWidthQuietly(lineSquads[k], previous);
            }
            return issued;
        }

        void SetWidthQuietly(int squad, float width)
        {
            reformSuppressed = true;
            try { Commander.SetWidth(squad, width); }
            finally { reformSuppressed = false; }
        }

        /// <summary>선을 긋는 동안 선택한 부대들이 설 자리를 <see cref="Preview"/>에 채운다.</summary>
        public void PreviewLine(Vector3 start, Vector3 end, Vector3 viewForward)
        {
            Preview.Begin();
            float facing = FormationLayout.FacingFromLine(start, end, viewForward);
            CollectSelected(lineSquads);
            for (int k = 0; k < lineSquads.Count; k++)
            {
                LineSegment(start, end, facing, k, out Vector3 anchor, out float width);
                PlanSlots(Commander.FormationOf(lineSquads[k]), width, anchor, facing, true,
                    CommandableCount(lineSquads[k]), slotBuffer);
                Preview.Add(slotBuffer);
            }
        }

        void CollectSelected(List<int> output)
        {
            output.Clear();
            for (int i = 0; i < Commander.SquadCount; i++) if (Commander.IsSelected(i)) output.Add(i);
        }

        void LineSegment(Vector3 start, Vector3 end, float facing, int k, out Vector3 anchor, out float width)
        {
            FormationLayout.Segment(start, end, facing, lineSquads.Count, k, out Vector3 from, out Vector3 to);
            anchor = (from + to) * 0.5f;
            Vector3 along = to - from; along.y = 0f;
            width = along.magnitude;
            if (NavMesh.SamplePosition(anchor, out NavMeshHit hit, settings.NavigationSnapDistance, NavMesh.AllAreas)
                && Mathf.Abs(hit.position.y - anchor.y) <= settings.VerticalSnapTolerance)
                anchor = hit.position;
        }

        /// <summary>선택한 부대의 진형을 다음 것으로 바꾼다. 바꾼 부대 수.</summary>
        public int CycleSelectedFormation()
        {
            int changed = 0;
            for (int i = 0; i < Commander.SquadCount; i++)
                if (Commander.IsSelected(i) && Commander.SetFormation(i, SquadCommander.Next(Commander.FormationOf(i)))) changed++;
            return changed;
        }

        // 진형이나 폭이 바뀌면 그 자리에서 다시 선다. 목적지가 없던 부대는 지금 모인 곳에서 선다.
        void Reform(int index)
        {
            if (reformSuppressed || index < 0 || index >= army.Squads.Count) return;
            SquadOrder order = Commander.OrderOf(index);
            if (order.HasDestination)
            {
                if (order.Kind == SquadOrderKind.Hold)
                    Commander.IssueTo(index, new SquadOrder(SquadOrderKind.Move, order.Destination, order.Facing, order.FrontAnchored));
                else Apply(index, order);
                return;
            }
            if (order.Kind != SquadOrderKind.Hold || !TryGetGatherPoint(index, out Vector3 point)) return;
            float facing = 0f;
            foreach (UnitCombatAI member in CollectCommandable(index, commandable)) { facing = member.transform.eulerAngles.y; break; }
            Commander.IssueTo(index, new SquadOrder(SquadOrderKind.Move, point, facing));
        }

        void Apply(int index, SquadOrder order)
        {
            List<UnitCombatAI> alive = CollectCommandable(index, new List<UnitCombatAI>());
            // 지시가 모두를 다시 세우므로 지금 인원을 기준으로 삼고, 쌓여 있던 다시 서기는 버린다.
            RosterWatch watch = WatchOf(army.Squads[index]);
            watch.LastCount = alive.Count; watch.Dirty = false; watch.RefillAt = float.PositiveInfinity;
            if (order.Kind == SquadOrderKind.Move || (order.Kind == SquadOrderKind.Attack && order.HasDestination))
            {
                UnitDirective toward = order.Kind == SquadOrderKind.Move ? UnitDirective.Move : UnitDirective.Engage;
                PlanSlots(index, order.Destination, order.Facing, order.FrontAnchored, alive.Count, slotBuffer);
                for (int i = 0; i < alive.Count; i++) alive[i].SetDirective(toward, slotBuffer[i]);
                return;
            }
            UnitDirective directive = order.Kind == SquadOrderKind.Attack ? UnitDirective.Engage : UnitDirective.Hold;
            foreach (UnitCombatAI member in alive)
                // 이동이 끝나 이미 자리를 지키는 유닛은 다시 건드리지 않는다.
                if (directive != UnitDirective.Hold || member.Directive != UnitDirective.Hold) member.SetDirective(directive);
        }

        void Update()
        {
            for (int i = 0; i < army.Squads.Count; i++)
            {
                if (Commander.OrderOf(i).Kind != SquadOrderKind.Move) continue;
                // 대장(고용 영웅)도 이동 지시를 받으므로 함께 센다. 빼면 잡졸만 도착해도 부대가 중지로 바뀌어
                // 아직 걷던 대장이 그 자리에 멈춘다(BUG-hero-move-stops-early).
                bool moving = IsMoving(army.Squads[i].Leader);
                if (!moving)
                    foreach (UnitCombatAI member in army.Squads[i].Members)
                        if (IsMoving(member)) { moving = true; break; }
                if (!moving) Commander.CompleteMove(i);
            }
        }

        // ── 부대원 변화에 맞춰 다시 서기(COMBAT-08) ─────────────────

        RosterWatch WatchOf(Squad<UnitCombatAI> squad)
        {
            if (!rosterWatch.TryGetValue(squad, out RosterWatch watch)) rosterWatch[squad] = watch = new RosterWatch();
            return watch;
        }

        void MarkRosterChanged(Squad<UnitCombatAI> squad)
        {
            if (squad != null) WatchOf(squad).Dirty = true;
        }

        /// <summary>
        /// 분대마다 지시받는 인원을 살핀다. 늘었거나(모집·편성·복귀) 편성이 바뀌면 이 프레임 끝에 바로 다시 서고,
        /// 줄었으면(전사·도망) 자리를 지키는 분대만 잠시 모았다가 뒷줄에서 빈자리를 메운다.
        /// </summary>
        void LateUpdate()
        {
            float now = Time.time;
            for (int i = 0; i < army.Squads.Count; i++)
            {
                Squad<UnitCombatAI> squad = army.Squads[i];
                RosterWatch watch = WatchOf(squad);
                int count = CollectCommandable(i, countBuffer).Count;
                if (watch.LastCount < 0) watch.LastCount = count;
                if (count > watch.LastCount) watch.Dirty = true;
                else if (count < watch.LastCount && float.IsPositiveInfinity(watch.RefillAt))
                    watch.RefillAt = now + settings.RefillDelay;
                watch.LastCount = count;
                if (!watch.Dirty && now < watch.RefillAt) continue;
                watch.Dirty = false; watch.RefillAt = float.PositiveInfinity;
                Reslot(i);
            }
        }

        /// <summary>
        /// 지금 지시 그대로 대형 자리를 다시 나눈다. 이동 중이면 모두 새 자리로 가고, 자리를 지키는 중이면 제자리인 부대원은 두고
        /// 빈자리만 뒤에서 당겨 채운다(새 부대원은 남은 자리). 공격 중이거나 목적지 없는 분대는 건드리지 않는다.
        /// </summary>
        public void Reslot(int index)
        {
            if (index < 0 || index >= army.Squads.Count) return;
            SquadOrder order = Commander.OrderOf(index);
            if (!order.HasDestination || order.Kind == SquadOrderKind.Attack) return;
            if (order.Kind == SquadOrderKind.Move) { Apply(index, order); return; }
            List<UnitCombatAI> alive = CollectCommandable(index, commandable);
            PlanSlots(index, order.Destination, order.Facing, order.FrontAnchored, alive.Count, slotBuffer);
            postBuffer.Clear(); hasPostBuffer.Clear();
            foreach (UnitCombatAI member in alive)
            {
                bool holding = member.Directive == UnitDirective.Hold;
                postBuffer.Add(holding ? member.HoldPost : member.transform.position);
                hasPostBuffer.Add(holding);
            }
            float keep = settings.Spacing(Commander.FormationOf(index)) * KeepPostFraction;
            int[] assigned = FormationLayout.AssignSlots(postBuffer, hasPostBuffer, slotBuffer, keep);
            for (int i = 0; i < alive.Count; i++)
            {
                if (assigned[i] < 0) continue;
                Vector3 slot = slotBuffer[assigned[i]];
                // 제자리인 부대원은 싸우던 대상을 잃지 않도록 다시 지시하지 않는다.
                if (hasPostBuffer[i] && (postBuffer[i] - slot).sqrMagnitude <= keep * keep) continue;
                alive[i].SetDirective(UnitDirective.Hold, slot);
            }
        }

        void OnDestroy()
        {
            if (Commander != null) { Commander.OrderChanged -= Apply; Commander.FormationChanged -= Reform; }
        }
    }
}
