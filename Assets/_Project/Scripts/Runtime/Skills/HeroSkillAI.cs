using System;
using System.Collections.Generic;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Skills
{
    /// <summary>
    /// 영웅 AI 자동 시전(Q-63, COMBAT-06). <see cref="Auto"/>가 켜져 있으면 정해진 간격마다 준비된 칸을 앞에서부터 보고
    /// <see cref="SkillAutoRules"/>에 맞는 첫 스킬 하나를 쓴다. 이동·공격 판단(<see cref="UnitCombatAI"/>)은 읽기만 하고 바꾸지 않는다.
    /// 끄면 플레이어의 직접 명령(Alt + 스킬 키)으로만 쓴다.
    /// </summary>
    [RequireComponent(typeof(SkillCaster))]
    public sealed class HeroSkillAI : MonoBehaviour
    {
        readonly List<CharacterActor> scratch = new List<CharacterActor>();
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
            if (caster == null || !caster.CanAct) return -1;
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
                default:
                    if (!SkillAutoRules.ShouldSupport(target != null, target != null ? Distance(self, target) : 0f, settings))
                        return false;
                    if (skill.TargetMode == SkillTargetMode.TargetUnit) aim = SkillAim.AtUnit(self);
                    return true;
            }
        }

        bool ChooseOffense(SkillDefinition skill, CharacterActor self, CharacterActor target, ref SkillAim aim)
        {
            switch (skill.TargetMode)
            {
                case SkillTargetMode.TargetUnit:
                    if (target == null || !SkillTargeting.InUnitRange(self, target, skill.Range)) return false;
                    aim = SkillAim.AtUnit(target);
                    return true;
                case SkillTargetMode.AimPoint:
                    if (target == null || Distance(self, target) > skill.Range) return false;
                    aim = SkillAim.At(target.transform.position);
                    break;
                case SkillTargetMode.Self:
                    break;
                default:
                    return false;
            }
            SkillTargeting.Collect(self, skill, aim, true, caster.Context, scratch);
            return SkillAutoRules.ShouldUseArea(scratch.Count, settings);
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
            foreach (CharacterActor actor in CharacterActor.ActiveActors)
            {
                if (!SkillTargeting.IsAlly(self, actor) || Distance(self, actor) > range) continue;
                float ratio = Ratio(actor);
                if (ratio < bestRatio) { best = actor; bestRatio = ratio; }
            }
            return best;
        }

        static float Ratio(CharacterActor actor) =>
            SkillAutoRules.HealthRatio(actor.Health.Current, actor.Health.Maximum);

        static float Distance(CharacterActor a, CharacterActor b) => Vector3.Distance(a.transform.position, b.transform.position);
    }
}
