using System;
using System.Collections.Generic;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Skills
{
    /// <summary>
    /// 영웅 AI 자동 시전(Q-63, COMBAT-06, COMBAT-13). <see cref="Auto"/>가 켜져 있으면 정해진 간격마다 준비된 칸을 앞에서부터 보고
    /// <see cref="SkillAutoRules"/>에 맞는 첫 스킬 하나를 쓴다. 이동·공격 판단(<see cref="UnitCombatAI"/>)은 읽기만 하고 바꾸지 않는다.
    /// 현재 대상이 없거나 스킬 사거리 밖이면 스킬 사거리 안에서 가장 가까운 적을 직접 찾는다(COMBAT-13).
    /// 끄면 플레이어의 직접 명령(Alt + 스킬 키)으로만 쓴다.
    /// </summary>
    [RequireComponent(typeof(SkillCaster))]
    public sealed class HeroSkillAI : MonoBehaviour
    {
        readonly List<CharacterActor> scratch = new List<CharacterActor>();
        readonly List<Vector3> candidates = new List<Vector3>();
        readonly List<Vector3> enemyPoints = new List<Vector3>();
        readonly List<float> enemyBodies = new List<float>();
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

        /// <summary>한 번 판단한다. 쓴 칸 번호, 쓰지 않았으면 -1.</summary>
        public int Think()
        {
            if (caster == null || !caster.CanAct || settings == null) return -1;
            SkillSlots slots = caster.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots.IsReady(i) || !TryChoose(slots.Get(i), out SkillAim aim)) continue;
                if (caster.TryCast(i, aim) != SkillCastResult.Cast) continue;
                AutoCasts++;
                return i;
            }
            return -1;
        }

        bool TryChoose(SkillDefinition skill, out SkillAim aim)
        {
            CharacterActor self = caster.Actor;
            aim = SkillAim.At(self.transform.position);
            CharacterActor current = TargetSource != null ? TargetSource() : ai != null ? ai.Target : null;
            CharacterActor target = self.IsEnemy(current) ? current : null;
            switch (SkillAutoRules.Intent(skill))
            {
                case SkillAutoIntent.Offense: return ChooseOffense(skill, self, target, ref aim);
                case SkillAutoIntent.Heal: return ChooseHeal(skill, self, ref aim);
                default: return ChooseSupport(skill, self, target, ref aim);
            }
        }

        bool ChooseOffense(SkillDefinition skill, CharacterActor self, CharacterActor target, ref SkillAim aim)
        {
            switch (skill.TargetMode)
            {
                case SkillTargetMode.TargetUnit:
                    CharacterActor unit = SkillTargeting.InUnitRange(self, target, skill.Range)
                        ? target : NearestEnemy(self, skill.Range, true);
                    if (unit == null) return false;
                    aim = SkillAim.AtUnit(unit);
                    return true;
                case SkillTargetMode.AimPoint:
                    if (!TryClusterAim(skill, self, target, out Vector3 point)) return false;
                    aim = SkillAim.At(point);
                    break;
                case SkillTargetMode.Self:
                    break;
                default:
                    return false;
            }
            SkillTargeting.Collect(self, skill, aim, true, caster.Context, scratch);
            return SkillAutoRules.ShouldUseArea(scratch.Count, settings);
        }

        // 사거리 안 적 위치를 후보로, 반경 안에 적이 가장 많이 드는 곳(COMBAT-13). 같으면 현재 대상(없으면 자신)에 가까운 곳.
        bool TryClusterAim(SkillDefinition skill, CharacterActor self, CharacterActor target, out Vector3 point)
        {
            point = self.transform.position;
            candidates.Clear(); enemyPoints.Clear(); enemyBodies.Clear();
            CombatSpatialIndex.QueryEnemies(self.Team, self.transform.position, skill.Range + skill.Radius, scratch);
            for (int i = 0; i < scratch.Count; i++)
            {
                CharacterActor actor = scratch[i];
                if (!self.IsEnemy(actor)) continue;
                Vector3 position = actor.transform.position;
                enemyPoints.Add(position);
                enemyBodies.Add(actor.Profile != null ? actor.Profile.Radius : 0f);
                if (FlatDistance(self.transform.position, position) <= skill.Range) candidates.Add(position);
            }
            Vector3 prefer = target != null ? target.transform.position : self.transform.position;
            int best = SkillAutoRules.BestClusterCenter(candidates, enemyPoints, enemyBodies, skill.Radius, prefer, out _);
            if (best < 0) return false;
            point = candidates[best];
            return true;
        }

        bool ChooseSupport(SkillDefinition skill, CharacterActor self, CharacterActor target, ref SkillAim aim)
        {
            CharacterActor engaged = target != null && Distance(self, target) <= settings.EngageDistance
                ? target : NearestEnemy(self, settings.EngageDistance, false);
            if (!SkillAutoRules.ShouldSupport(engaged != null, engaged != null ? Distance(self, engaged) : 0f, settings))
                return false;
            if (skill.TargetMode == SkillTargetMode.TargetUnit) aim = SkillAim.AtUnit(self);
            return !SkillAutoRules.IsMoraleOnly(skill) || MoraleIsLow(skill, self, aim);
        }

        // 사기만 올리는 스킬: 대상 분대 중 사기가 오를 수 있는 분대의 가장 낮은 비율이 기준 이하인지(COMBAT-13).
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

        bool ChooseHeal(SkillDefinition skill, CharacterActor self, ref SkillAim aim)
        {
            if (skill.TargetMode == SkillTargetMode.AimPoint || skill.TargetMode == SkillTargetMode.TargetUnit)
            {
                CharacterActor hurt = MostHurtAllyInRange(self, skill.Range);
                if (hurt == null || !SkillAutoRules.ShouldHeal(Ratio(hurt), settings)) return false;
                aim = skill.TargetMode == SkillTargetMode.TargetUnit ? SkillAim.AtUnit(hurt) : SkillAim.At(hurt.transform.position);
                return true;
            }
            SkillTargeting.Collect(self, skill, aim, false, caster.Context, scratch);
            float lowest = 1f;
            foreach (CharacterActor ally in scratch) lowest = Mathf.Min(lowest, Ratio(ally));
            return scratch.Count > 0 && SkillAutoRules.ShouldHeal(lowest, settings);
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
        CharacterActor NearestEnemy(CharacterActor self, float range, bool withBody)
        {
            CharacterActor best = null;
            float bestDistance = float.PositiveInfinity;
            CombatSpatialIndex.QueryEnemies(self.Team, self.transform.position, range, scratch);
            for (int i = 0; i < scratch.Count; i++)
            {
                CharacterActor actor = scratch[i];
                if (!self.IsEnemy(actor)) continue;
                float distance = Distance(self, actor);
                float reach = range + (withBody && actor.Profile != null ? actor.Profile.Radius : 0f);
                if (distance > reach || distance >= bestDistance) continue;
                best = actor;
                bestDistance = distance;
            }
            return best;
        }

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
