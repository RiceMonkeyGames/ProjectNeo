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
        public SquadCommander Commander { get; private set; }
        public SquadCommandSettings Settings => settings;
        public IReadOnlyList<Squad<UnitCombatAI>> Squads => army.Squads;

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
            return army.Squads[squad].TryAdd(member);
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

        /// <summary>부대 대장을 정한다(D-8). null이면 비운다. 없는 번호면 false.</summary>
        public bool SetLeader(int squad, UnitCombatAI leader)
        {
            if (squad < 0 || squad >= army.Squads.Count) return false;
            army.Squads[squad].Leader = leader;
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
                foreach (UnitCombatAI existing in squad.Members)
                    if (existing == member) return true;
            return false;
        }

        public int AliveCount(int squad)
        {
            int count = 0;
            foreach (UnitCombatAI member in army.Squads[squad].Members) if (IsAlive(member)) count++;
            return count;
        }

        static bool IsAlive(UnitCombatAI member) => member != null && member.isActiveAndEnabled
            && member.Actor.Health != null && !member.Actor.Health.IsDead;

        void Apply(int index, SquadOrder order)
        {
            var alive = new List<UnitCombatAI>();
            // 대장(D-3 고용 영웅)도 부대 지시를 따른다. 대형 맨 앞 자리에 선다.
            UnitCombatAI leader = army.Squads[index].Leader;
            if (IsAlive(leader) && !detached.Contains(leader)) alive.Add(leader);
            foreach (UnitCombatAI member in army.Squads[index].Members)
                if (IsAlive(member) && !detached.Contains(member) && member != leader) alive.Add(member);
            if (order.Kind == SquadOrderKind.Move || (order.Kind == SquadOrderKind.Attack && order.HasDestination))
            {
                UnitDirective toward = order.Kind == SquadOrderKind.Move ? UnitDirective.Move : UnitDirective.Engage;
                List<Vector3> slots = SquadFormation.Slots(order.Destination, order.Facing, alive.Count,
                    settings.FormationColumns, settings.FormationSpacing);
                for (int i = 0; i < alive.Count; i++)
                {
                    // 대형 자리가 걸을 수 없는 곳이면 목적지 자체로 보낸다.
                    Vector3 slot = NavMesh.SamplePosition(slots[i], out NavMeshHit hit, settings.NavigationSnapDistance,
                        NavMesh.AllAreas) && Mathf.Abs(hit.position.y - order.Destination.y) <= settings.VerticalSnapTolerance
                        ? hit.position : order.Destination;
                    alive[i].SetDirective(toward, slot);
                }
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
                bool moving = false;
                foreach (UnitCombatAI member in army.Squads[i].Members)
                    if (IsAlive(member) && !detached.Contains(member) && member.Directive == UnitDirective.Move)
                    { moving = true; break; }
                if (!moving) Commander.CompleteMove(i);
            }
        }

        void OnDestroy()
        {
            if (Commander != null) Commander.OrderChanged -= Apply;
        }
    }
}
