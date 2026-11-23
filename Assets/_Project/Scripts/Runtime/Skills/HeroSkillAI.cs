using System;
using System.Collections.Generic;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Skills
{
    /// <summary>
    /// 영웅 AI 자동 시전(Q-63, COMBAT-06, COMBAT-13, COMBAT-18). <see cref="Auto"/>가 켜져 있으면 정해진 간격마다 준비된 칸마다
    /// 긴급도(<see cref="SkillAutoUrgency"/>)를 매기고 가장 높은 스킬 하나를 쓴다. 같으면 앞 칸이 먼저다(<see cref="SkillAutoRules.MostUrgent"/>).
    /// 이동·공격 판단(<see cref="UnitCombatAI"/>)은 읽기만 하고 바꾸지 않는다.
    /// 현재 대상이 없거나 스킬 사거리 밖이면 스킬 사거리 안에서 가장 가까운 적을 직접 찾는다(COMBAT-13).
    /// 경직 스킬은 고가치 적(보스·공성 파괴·사다리)을 먼저 노리고, 범위 회복은 여럿이 다친 곳을 덮는다(COMBAT-18).
    /// 끄면 플레이어의 직접 명령(Alt + 스킬 키)으로만 쓴다.
    /// </summary>
    [RequireComponent(typeof(SkillCaster))]
    public sealed class HeroSkillAI : MonoBehaviour
    {
        readonly List<CharacterActor> scratch = new List<CharacterActor>();
        readonly List<Vector3> candidates = new List<Vector3>();
        readonly List<Vector3> points = new List<Vector3>();
        readonly List<float> bodies = new List<float>();
        readonly List<float> ratios = new List<float>();
        readonly List<SkillAutoUrgency> urgencies = new List<SkillAutoUrgency>();
        readonly List<SkillAim> aims = new List<SkillAim>();
        readonly HashSet<int> squadsSeen = new HashSet<int>();
        SkillCaster caster;
        UnitCombatAI ai;
        SkillAutoSettings settings;
        float thinkRemaining;

        /// <summary>자동 시전 켜짐(기본값). 영웅마다 준비 단계 장비 창(I)에서 바꾼다.</summary>
        public bool Auto { get; set; } = true;
        /// <summary>자동 시전을 해도 되는지(전투 중). 비어 있으면 항상 된다.</summary>
        public Func<bool> Gate { get; set; }
        /// <summary>지금 싸우는 적. 비어 있으면 <see cref="UnitCombatAI.Target"/>을 쓴다(테스트에서 바꿔 끼운다).</summary>
        public Func<CharacterActor> TargetSource { get; set; }
        /// <summary>자동으로 시전한 횟수(테스트용).</summary>
        public int AutoCasts { get; private set; }

        public static HeroSkillAI On(SkillCaster target)
        {
            if (target == null) return null;
            HeroSkillAI brain = target.GetComponent<HeroSkillAI>();
            return brain != null ? brain : target.gameObject.AddComponent<HeroSkillAI>();
        }

        public void Configure(SkillAutoSettings autoSettings)
        {
            settings = autoSettings;
            thinkRemaining = 0f;
        }

        void Awake()
        {
            caster = GetComponent<SkillCaster>();
            ai = GetComponent<UnitCombatAI>();
        }

        void Update()
        {
            if (!Auto || settings == null || (Gate != null && !Gate())) return;
            thinkRemaining -= Time.deltaTime;
            if (thinkRemaining > 0f) return;
            thinkRemaining = settings.ThinkInterval;
            Think();
        }

        /// <summary>한 번 판단한다. 쓴 칸 번호, 쓰지 않았으면 -1. 가장 급한 칸 시전이 실패하면 다음으로 급한 칸을 본다.</summary>
        public int Think()
        {
            if (caster == null || !caster.CanAct || settings == null) return -1;
            SkillSlots slots = caster.Slots;
            urgencies.Clear();
            aims.Clear();
            for (int i = 0; i < slots.Count; i++)
            {
                SkillAim aim = SkillAim.At(caster.Actor.transform.position);
                SkillAutoUrgency urgency = slots.IsReady(i) ? Evaluate(slots.Get(i), out aim) : SkillAutoUrgency.None;
                urgencies.Add(urgency);
                aims.Add(aim);
            }
            for (int tries = 0; tries < urgencies.Count; tries++)
            {
                int best = SkillAutoRules.MostUrgent(urgencies);
                if (best < 0) return -1;
                if (caster.TryCast(best, aims[best]) == SkillCastResult.Cast)
                {
                    AutoCasts++;
                    return best;
                }
                urgencies[best] = SkillAutoUrgency.None;
            }
            return -1;
        }

        SkillAutoUrgency Evaluate(SkillDefinition skill, out SkillAim aim)
        {
            CharacterActor self = caster.Actor;
            aim = SkillAim.At(self.transform.position);
            if (skill == null) return SkillAutoUrgency.None;
            CharacterActor current = TargetSource != null ? TargetSource() : ai != null ? ai.Target : null;
            CharacterActor target = self.IsEnemy(current) ? current : null;
            switch (SkillAutoRules.Intent(skill))
            {
                case SkillAutoIntent.Offense: return ChooseOffense(skill, self, target, ref aim);
                case SkillAutoIntent.Heal: return ChooseHeal(skill, self, ref aim);
                default: return ChooseSupport(skill, self, target, ref aim);
            }
        }

        SkillAutoUrgency ChooseOffense(SkillDefinition skill, CharacterActor self, CharacterActor target, ref SkillAim aim)
        {
            bool stagger = SkillAutoRules.HasStagger(skill);
            switch (skill.TargetMode)
            {
                case SkillTargetMode.TargetUnit:
                    CharacterActor focus = stagger ? NearestEnemy(self, skill.Range, true, false, true) : null;
                    if (focus != null)
                    {
                        aim = SkillAim.AtUnit(focus);
                        return SkillAutoRules.UnitOffenseUrgency(true);
                    }
                    CharacterActor unit = SkillTargeting.InUnitRange(self, target, skill.Range)
                        ? target : NearestEnemy(self, skill.Range, true);
                    if (unit == null) return SkillAutoUrgency.None;
                    aim = SkillAim.AtUnit(unit);
                    return SkillAutoRules.UnitOffenseUrgency(stagger && IsHighValue(unit));
                case SkillTargetMode.AimPoint:
                    if (!TryClusterAim(skill, self, target, stagger, out Vector3 point)) return SkillAutoUrgency.None;
                    aim = SkillAim.At(point);
                    break;
                case SkillTargetMode.Self:
                    break;
                default:
                    return SkillAutoUrgency.None;
            }
            SkillTargeting.Collect(self, skill, aim, true, caster.Context, scratch);
            bool staggersHighValue = false;
            if (stagger)
                foreach (CharacterActor enemy in scratch)
                    if (IsHighValue(enemy)) { staggersHighValue = true; break; }
            return SkillAutoRules.AreaOffenseUrgency(scratch.Count, staggersHighValue, settings);
        }

        // 사거리 안 적 위치를 후보로, 반경 안에 적이 가장 많이 드는 곳(COMBAT-13). 같으면 현재 대상(없으면 자신)에 가까운 곳.
        // 경직 스킬이고 사거리 안에 고가치 적이 있으면 고가치 적 위치만 후보로 삼는다(COMBAT-18).
        bool TryClusterAim(SkillDefinition skill, CharacterActor self, CharacterActor target, bool stagger, out Vector3 point)
        {
            point = self.transform.position;
            candidates.Clear(); points.Clear(); bodies.Clear();
            CombatSpatialIndex.QueryEnemies(self.Team, self.transform.position, skill.Range + skill.Radius, scratch);
            bool focusOnly = false;
            for (int i = 0; i < scratch.Count; i++)
            {
                CharacterActor actor = scratch[i];
                if (!self.IsEnemy(actor)) continue;
                Vector3 position = actor.transform.position;
                points.Add(position);
                bodies.Add(actor.Profile != null ? actor.Profile.Radius : 0f);
                if (FlatDistance(self.transform.position, position) > skill.Range) continue;
                bool highValue = stagger && IsHighValue(actor);
                if (highValue && !focusOnly) { candidates.Clear(); focusOnly = true; }
                if (highValue || !focusOnly) candidates.Add(position);
            }
            Vector3 prefer = target != null ? target.transform.position : self.transform.position;
            int best = SkillAutoRules.BestClusterCenter(candidates, points, bodies, skill.Radius, prefer, out _);
            if (best < 0) return false;
            point = candidates[best];
            return true;
        }

        SkillAutoUrgency ChooseSupport(SkillDefinition skill, CharacterActor self, CharacterActor target, ref SkillAim aim)
        {
            bool engaged = IsEngagedWith(self, target) || NearestEnemy(self, settings.EngageDistance, false, true) != null;
            if (!engaged) return SkillAutoUrgency.None;
            if (skill.TargetMode == SkillTargetMode.TargetUnit) aim = SkillAim.AtUnit(self);
            bool moraleLow = SkillAutoRules.HasMoraleGain(skill) && MoraleIsLow(skill, self, aim);
            return SkillAutoRules.SupportUrgency(true, SkillAutoRules.IsMoraleOnly(skill), moraleLow);
        }

        // 교전 중인 적인지: 교전 거리 안이고, 근접 영웅이면 높이 차도 기준 이하(COMBAT-18). 성벽 위 근접 영웅이 성벽 아래 적에게 버프를 쓰지 않는다.
        bool IsEngagedWith(CharacterActor self, CharacterActor enemy) =>
            enemy != null && Distance(self, enemy) <= settings.EngageDistance && WithinEngageHeight(self, enemy);

        bool WithinEngageHeight(CharacterActor self, CharacterActor enemy) =>
            SkillAutoRules.WithinEngageHeight(IsMelee(self), enemy.transform.position.y - self.transform.position.y, settings);

        // 사기 올리는 스킬: 대상 분대 중 사기가 오를 수 있는 분대의 가장 낮은 비율이 기준 이하인지(COMBAT-13).
        bool MoraleIsLow(SkillDefinition skill, CharacterActor self, SkillAim aim)
        {
            ISkillContext context = caster.Context;
            if (context == null) return false;
            SkillTargeting.Collect(self, skill, aim, false, context, scratch);
            squadsSeen.Clear();
            bool found = false;
            float lowest = 1f;
            foreach (CharacterActor ally in scratch)
            {
                int squad = context.SquadOf(ally);
                if (squad < 0 || !squadsSeen.Add(squad) || !context.TryGetMoraleRatio(squad, out float ratio)) continue;
                found = true;
                lowest = Mathf.Min(lowest, ratio);
            }
            return found && SkillAutoRules.ShouldRaiseMorale(lowest, settings);
        }

        SkillAutoUrgency ChooseHeal(SkillDefinition skill, CharacterActor self, ref SkillAim aim)
        {
            if (skill.TargetMode == SkillTargetMode.TargetUnit)
            {
                CharacterActor hurt = MostHurtAllyInRange(self, skill.Range);
                if (hurt == null) return SkillAutoUrgency.None;
                aim = SkillAim.AtUnit(hurt);
                return SkillAutoRules.HealUrgency(Ratio(hurt), settings);
            }
            if (skill.TargetMode == SkillTargetMode.AimPoint)
            {
                if (!TryHealAim(skill, self, out Vector3 point)) return SkillAutoUrgency.None;
                aim = SkillAim.At(point);
            }
            SkillTargeting.Collect(self, skill, aim, false, caster.Context, scratch);
            if (scratch.Count == 0) return SkillAutoUrgency.None;
            float lowest = 1f;
            foreach (CharacterActor ally in scratch) lowest = Mathf.Min(lowest, Ratio(ally));
            return SkillAutoRules.HealUrgency(lowest, settings);
        }

        // 범위 회복 중심(COMBAT-18): 사거리 안 회복 기준 이하 아군 위치를 후보로, 반경 안 잃은 체력 비율 합이 가장 큰 곳.
        // 같으면 가장 많이 다친 아군에 가까운 곳.
        bool TryHealAim(SkillDefinition skill, CharacterActor self, out Vector3 point)
        {
            point = self.transform.position;
            candidates.Clear(); points.Clear(); bodies.Clear(); ratios.Clear();
            CombatSpatialIndex.QueryTeam(self.Team, self.transform.position, skill.Range + skill.Radius, scratch);
            CharacterActor mostHurt = null;
            float lowest = float.PositiveInfinity;
            for (int i = 0; i < scratch.Count; i++)
            {
                CharacterActor actor = scratch[i];
                if (!SkillTargeting.IsAlly(self, actor)) continue;
                Vector3 position = actor.transform.position;
                float ratio = Ratio(actor);
                points.Add(position);
                bodies.Add(actor.Profile != null ? actor.Profile.Radius : 0f);
                ratios.Add(ratio);
                if (Distance(self, actor) > skill.Range || !SkillAutoRules.ShouldHeal(ratio, settings)) continue;
                candidates.Add(position);
                if (ratio < lowest) { lowest = ratio; mostHurt = actor; }
            }
            if (mostHurt == null) return false;
            int best = SkillAutoRules.BestHealCenter(candidates, points, bodies, ratios, skill.Radius,
                mostHurt.transform.position, out _);
            if (best < 0) return false;
            point = candidates[best];
            return true;
        }

        CharacterActor MostHurtAllyInRange(CharacterActor self, float range)
        {
            CharacterActor best = null;
            float bestRatio = float.PositiveInfinity;
            CombatSpatialIndex.QueryTeam(self.Team, self.transform.position, range, scratch);
            for (int i = 0; i < scratch.Count; i++)
            {
                CharacterActor actor = scratch[i];
                if (!SkillTargeting.IsAlly(self, actor) || Distance(self, actor) > range) continue;
                float ratio = Ratio(actor);
                if (ratio < bestRatio) { best = actor; bestRatio = ratio; }
            }
            return best;
        }

        // 거리 안에서 가장 가까운 적. withBody면 대상 몸 반경만큼 더 멀어도 된다(TargetUnit 사거리 판정과 같다).
        // engageHeight면 교전 높이 판정(COMBAT-18)을, highValueOnly면 고가치 적만 본다.
        CharacterActor NearestEnemy(CharacterActor self, float range, bool withBody, bool engageHeight = false, bool highValueOnly = false)
        {
            CharacterActor best = null;
            float bestDistance = float.PositiveInfinity;
            CombatSpatialIndex.QueryEnemies(self.Team, self.transform.position, range, scratch);
            for (int i = 0; i < scratch.Count; i++)
            {
                CharacterActor actor = scratch[i];
                if (!self.IsEnemy(actor)) continue;
                if (highValueOnly && !IsHighValue(actor)) continue;
                if (engageHeight && !WithinEngageHeight(self, actor)) continue;
                float distance = Distance(self, actor);
                float reach = range + (withBody && actor.Profile != null ? actor.Profile.Radius : 0f);
                if (distance > reach || distance >= bestDistance) continue;
                best = actor;
                bestDistance = distance;
            }
            return best;
        }

        // 경직을 먼저 걸 고가치 적(COMBAT-18): 보스, 공성 파괴, 사다리(원거리 집중 사격 대상과 같다).
        static bool IsHighValue(CharacterActor actor) =>
            actor != null && TargetSpreadRules.IsFocusTarget(actor.Threat, actor.IsBoss);

        static bool IsMelee(CharacterActor actor) =>
            actor.Profile != null && actor.Profile.AttackKind == AttackKind.Melee;

        static float Ratio(CharacterActor actor) =>
            SkillAutoRules.HealthRatio(actor.Health.Current, actor.Health.Maximum);

        static float Distance(CharacterActor a, CharacterActor b) => Vector3.Distance(a.transform.position, b.transform.position);

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
