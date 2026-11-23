using System.Collections.Generic;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 성벽 위 사다리 방어 임무를 나눠 주는 곳(SIEGE-31). 켜진 사다리가 매 프레임 부르지만 한 프레임에 한 번,
    /// <see cref="SiegeSettings.LadderDefenseInterval"/>마다만 다시 계산한다. 배정 규칙은 <see cref="LadderDefensePlanner"/>,
    /// 유닛 행동은 <see cref="UnitCombatAI.TrySetHoldDuty"/>가 맡는다. 플레이어는 대상이 아니다.
    /// </summary>
    public static class LadderDefenseDirector
    {
        readonly struct Assignment
        {
            public readonly SiegeLadder Ladder;
            public readonly LadderDutyRole Role;
            public readonly int Spot;

            public Assignment(SiegeLadder ladder, LadderDutyRole role, int spot)
            {
                Ladder = ladder;
                Role = role;
                Spot = spot;
            }
        }

        static readonly LadderDefensePlanner planner = new LadderDefensePlanner();
        static readonly List<SiegeLadder> siteLadders = new List<SiegeLadder>();
        static readonly List<LadderDefenseSite> sitePool = new List<LadderDefenseSite>();
        static readonly List<LadderDefenseSite> sites = new List<LadderDefenseSite>();
        static readonly List<UnitCombatAI> units = new List<UnitCombatAI>();
        static readonly HashSet<UnitCombatAI> seen = new HashSet<UnitCombatAI>();
        static readonly List<LadderDefender> defenders = new List<LadderDefender>();
        static readonly List<LadderDuty> plan = new List<LadderDuty>();
        static readonly List<CharacterActor> nearby = new List<CharacterActor>();
        static readonly Dictionary<UnitCombatAI, Assignment> assigned = new Dictionary<UnitCombatAI, Assignment>();
        static readonly List<UnitCombatAI> stale = new List<UnitCombatAI>();
        static readonly Dictionary<(UnitCombatAI, SiegeLadder), float> rejectedUntil =
            new Dictionary<(UnitCombatAI, SiegeLadder), float>();
        static readonly List<(UnitCombatAI, SiegeLadder)> expired = new List<(UnitCombatAI, SiegeLadder)>();
        static int lastFrame = -1;
        static float nextTime;
        // 지킬 자리는 감지 반경 안이어도 지금 위치는 그보다 멀 수 있다(자리 주변 적을 쫓는 중 등). 그만큼 넓게 찾는다.
        const float QuerySlack = 1.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            assigned.Clear();
            rejectedUntil.Clear();
            lastFrame = -1;
            nextTime = 0f;
        }

        /// <summary>검증용: 유닛이 지금 맡은 사다리 임무. 없으면 false.</summary>
        public static bool TryGetDuty(UnitCombatAI unit, out SiegeLadder ladder, out LadderDutyRole role)
        {
            if (unit != null && assigned.TryGetValue(unit, out Assignment assignment) && unit.HasHoldDuty)
            {
                ladder = assignment.Ladder;
                role = assignment.Role;
                return true;
            }
            ladder = null;
            role = LadderDutyRole.None;
            return false;
        }

        /// <summary>켜진 사다리가 매 프레임 부른다. 주기가 되면 배정을 다시 계산한다.</summary>
        public static void Tick(SiegeLadder caller)
        {
            if (caller == null || caller.Settings == null || Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (Time.time < nextTime) return;
            nextTime = Time.time + caller.Settings.LadderDefenseInterval;
            Replan(caller.Settings);
        }

        /// <summary>사다리가 쓰러지거나 꺼지면 그 사다리의 임무를 바로 끝낸다.</summary>
        public static void Release(SiegeLadder ladder)
        {
            stale.Clear();
            foreach (var pair in assigned)
                if (pair.Value.Ladder == ladder) stale.Add(pair.Key);
            foreach (UnitCombatAI unit in stale) Drop(unit);
            stale.Clear();
        }

        static void Replan(SiegeSettings settings)
        {
            float radius = settings.LadderDefenseRadius;
            GatherSites(settings, radius);
            GatherUnits(radius);
            PruneRejections();
            defenders.Clear();
            for (int i = 0; i < units.Count; i++)
            {
                UnitCombatAI unit = units[i];
                LadderDuty current = LadderDuty.None;
                if (unit.HasHoldDuty && assigned.TryGetValue(unit, out Assignment held))
                {
                    int site = siteLadders.IndexOf(held.Ladder);
                    if (site >= 0) current = new LadderDuty(site, held.Role, held.Spot);
                }
                defenders.Add(new LadderDefender(unit.HoldPost, unit.transform.position, current, ExcludedMask(unit)));
            }
            planner.Plan(sites, defenders, radius, settings.LadderHeightTolerance + 0.5f,
                settings.LadderDefenseStickiness, plan);
            for (int i = 0; i < units.Count; i++) Apply(units[i], plan[i], settings);
            // 이번 계산에서 빠진(사다리가 없어졌거나, 다른 지시를 받았거나, 멀어진) 유닛의 임무는 끝낸다.
            stale.Clear();
            foreach (var pair in assigned)
                if (!seen.Contains(pair.Key)) stale.Add(pair.Key);
            foreach (UnitCombatAI unit in stale) Drop(unit);
            stale.Clear();
        }

        static void GatherSites(SiegeSettings settings, float radius)
        {
            siteLadders.Clear();
            sites.Clear();
            if (radius <= 0f) return;
            IReadOnlyList<SiegeLadder> ladders = SiegeLadder.Active;
            for (int i = 0; i < ladders.Count && sites.Count < 64; i++)
            {
                SiegeLadder ladder = ladders[i];
                if (ladder == null || !ladder.IsDefendable) continue;
                if (sitePool.Count <= sites.Count) sitePool.Add(new LadderDefenseSite());
                LadderDefenseSite site = sitePool[sites.Count];
                ladder.FillDefenseSite(site);
                sites.Add(site);
                siteLadders.Add(ladder);
            }
        }

        static void GatherUnits(float radius)
        {
            units.Clear();
            seen.Clear();
            for (int s = 0; s < sites.Count; s++)
            {
                CombatSpatialIndex.QueryTeam(CombatTeam.Allied, sites[s].Top, radius * QuerySlack, nearby);
                for (int i = 0; i < nearby.Count; i++)
                {
                    CharacterActor actor = nearby[i];
                    if (actor == null || !actor.TryGetComponent(out UnitCombatAI unit) || seen.Contains(unit)
                        || !IsEligible(unit)) continue;
                    seen.Add(unit);
                    units.Add(unit);
                }
            }
        }

        // 중지 중인 살아 있는 근접 아군 AI만. 사다리를 타는 중이면 뺀다.
        static bool IsEligible(UnitCombatAI unit)
        {
            if (unit == null || !unit.isActiveAndEnabled || unit.Directive != UnitDirective.Hold) return false;
            CharacterActor actor = unit.Actor;
            if (actor == null || actor.Team != CombatTeam.Allied || actor.Health == null || actor.Health.IsDead
                || actor.IsOnLadder || actor.Profile == null || actor.Profile.AttackKind != AttackKind.Melee) return false;
            return !unit.TryGetComponent(out LadderTraveller traveller) || !traveller.IsTraversing;
        }

        static void Apply(UnitCombatAI unit, LadderDuty duty, SiegeSettings settings)
        {
            if (duty.IsNone || duty.Spot < 0)
            {
                Drop(unit);
                return;
            }
            SiegeLadder ladder = siteLadders[duty.Site];
            LadderDefenseSite site = sites[duty.Site];
            Vector3 point = duty.Role == LadderDutyRole.Push ? site.PushSpots[duty.Spot] : site.ChokeSpots[duty.Spot];
            var holdDuty = new HoldDuty(point, ladder.EdgePoint, site.Top, settings.LadderBlockRadius,
                settings.LadderHeightTolerance);
            if (unit.TrySetHoldDuty(holdDuty))
            {
                assigned[unit] = new Assignment(ladder, duty.Role, duty.Spot);
                return;
            }
            // 길이 없으면 잠시 그 사다리를 맡기지 않는다. 그동안 다른 사다리나 다른 수비병이 맡는다.
            rejectedUntil[(unit, ladder)] = Time.time + settings.LadderDefenseRejectSeconds;
            Drop(unit);
        }

        static void Drop(UnitCombatAI unit)
        {
            if (!assigned.Remove(unit)) return;
            if (unit != null) unit.ClearHoldDuty();
        }

        static ulong ExcludedMask(UnitCombatAI unit)
        {
            if (rejectedUntil.Count == 0) return 0UL;
            ulong mask = 0UL;
            for (int s = 0; s < siteLadders.Count; s++)
                if (rejectedUntil.ContainsKey((unit, siteLadders[s]))) mask |= 1UL << s;
            return mask;
        }

        static void PruneRejections()
        {
            if (rejectedUntil.Count == 0) return;
            expired.Clear();
            float now = Time.time;
            foreach (var pair in rejectedUntil)
                if (pair.Value <= now || pair.Key.Item1 == null || pair.Key.Item2 == null) expired.Add(pair.Key);
            foreach (var key in expired) rejectedUntil.Remove(key);
            expired.Clear();
        }
    }
}
