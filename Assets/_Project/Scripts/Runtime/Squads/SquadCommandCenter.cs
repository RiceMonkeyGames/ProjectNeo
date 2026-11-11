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
        public SquadCommander Commander { get; private set; }
        public SquadCommandSettings Settings => settings;
        public IReadOnlyList<Squad<UnitCombatAI>> Squads => army.Squads;

        public void Configure(SquadConfig squadConfig, SquadCommandSettings commandSettings, SquadRoster[] squadRosters)
        {
            config = squadConfig; settings = commandSettings; rosters = squadRosters;
        }

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
            foreach (UnitCombatAI member in army.Squads[index].Members) if (IsAlive(member)) alive.Add(member);
            if (order.Kind == SquadOrderKind.Move || (order.Kind == SquadOrderKind.Attack && order.HasDestination))
            {
                UnitDirective toward = order.Kind == SquadOrderKind.Move ? UnitDirective.Move : UnitDirective.Engage;
                List<Vector3> slots = SquadFormation.Slots(order.Destination, order.Facing, alive.Count,
                    settings.FormationColumns, settings.FormationSpacing);
                for (int i = 0; i < alive.Count; i++)
                {
                    // 대형 자리가 걸을 수 없는 곳이면 목적지 자체로 보낸다.
                    Vector3 slot = NavMesh.SamplePosition(slots[i], out NavMeshHit hit, settings.NavigationSnapDistance,
                        NavMesh.AllAreas) && Mathf.Abs(hit.position.y - order.Destination.y) <= settings.NavigationSnapDistance
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
                    if (IsAlive(member) && member.Directive == UnitDirective.Move) { moving = true; break; }
                if (!moving) Commander.CompleteMove(i);
            }
        }

        void OnDestroy()
        {
            if (Commander != null) Commander.OrderChanged -= Apply;
        }
    }
}
