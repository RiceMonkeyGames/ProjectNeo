using System;
using System.Collections.Generic;
using ProjectNeo.Combat;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Skills
{
    /// <summary>
    /// 유닛 한 명의 스킬 칸과 시전(Q-62~Q-65, COMBAT-06). 플레이어 입력과 영웅 AI가 같은 <see cref="TryCast"/>를 쓴다.
    /// 실행 중에 <see cref="CharacterActor"/> 옆에 붙고(씬 수정 없음), 시전은 즉시 효과를 건 뒤 <see cref="Cast"/> 신호를 보낸다.
    /// </summary>
    [RequireComponent(typeof(CharacterActor))]
    public sealed class SkillCaster : MonoBehaviour
    {
        static readonly List<SkillCaster> activeCasters = new List<SkillCaster>();
        readonly List<CharacterActor> targets = new List<CharacterActor>();
        readonly List<CharacterActor> affected = new List<CharacterActor>();
        readonly HashSet<int> raisedSquads = new HashSet<int>();
        CharacterActor actor;
        ISkillContext context;

        public CharacterActor Actor => actor != null ? actor : actor = GetComponent<CharacterActor>();
        public SkillSlots Slots { get; private set; } = new SkillSlots(null);
        public ISkillContext Context => context;
        public static IReadOnlyList<SkillCaster> ActiveCasters => activeCasters;

        /// <summary>시전에 성공했을 때.</summary>
        public event Action<SkillCastInfo> Cast;
        /// <summary><see cref="Setup"/>으로 칸 구성이 바뀌었을 때(HUD 다시 그리기).</summary>
        public event Action SlotsReplaced;

        public static SkillCaster On(CharacterActor target)
        {
            if (target == null) return null;
            SkillCaster caster = target.GetComponent<SkillCaster>();
            return caster != null ? caster : target.gameObject.AddComponent<SkillCaster>();
        }

        /// <summary>칸을 새로 채운다(최대 3개, 모두 준비 상태).</summary>
        public void Setup(IEnumerable<SkillDefinition> skills)
        {
            Slots = new SkillSlots(skills);
            SlotsReplaced?.Invoke();
        }

        public void UseContext(ISkillContext skillContext) => context = skillContext;

        /// <summary>모든 칸을 준비 상태로(전투 시작).</summary>
        public void ResetCooldowns() => Slots.ResetAll();

        public bool CanAct => Actor != null && Actor.Health != null && !Actor.Health.IsDead && !Actor.IsOnLadder
            && isActiveAndEnabled;

        /// <summary>칸의 스킬을 지금 쓸 수 있는지 미리 본다(쿨다운·상태·대상). 쿨다운은 쓰지 않는다.</summary>
        public SkillCastResult Check(int slot, SkillAim aim)
        {
            SkillDefinition skill = Slots.Get(slot);
            if (skill == null) return SkillCastResult.NoSkill;
            if (!CanAct) return SkillCastResult.CannotAct;
            if (!Slots.IsReady(slot)) return SkillCastResult.NotReady;
            if (skill.TargetMode != SkillTargetMode.TargetUnit) return SkillCastResult.Cast;
            if (aim.Unit == null || !AnyEffectFits(skill, aim.Unit)) return SkillCastResult.NoTarget;
            return SkillTargeting.InUnitRange(Actor, aim.Unit, skill.Range) ? SkillCastResult.Cast : SkillCastResult.OutOfRange;
        }

        /// <summary>시전한다. 실패하면 쿨다운을 쓰지 않는다.</summary>
        public SkillCastResult TryCast(int slot, SkillAim aim)
        {
            SkillCastResult check = Check(slot, aim);
            if (check != SkillCastResult.Cast) return check;
            SkillDefinition skill = Slots.Get(slot);
            Slots.TryUse(slot);
            int count = Apply(skill, aim);
            Cast?.Invoke(new SkillCastInfo(Actor, skill, slot, SkillTargeting.Center(Actor, skill, aim), count));
            return SkillCastResult.Cast;
        }

        int Apply(SkillDefinition skill, SkillAim aim)
        {
            affected.Clear();
            float attack = Actor.Stats.GetValue(StatType.Attack);
            IReadOnlyList<SkillEffect> effects = skill.Effects;
            for (int i = 0; i < effects.Count; i++)
            {
                SkillEffect effect = effects[i];
                SkillTargeting.Collect(Actor, skill, aim, SkillEffectRules.AffectsEnemies(effect.kind), context, targets);
                if (effect.kind == SkillEffectKind.MoraleGain) RaiseMorale(effect, targets);
                else foreach (CharacterActor target in targets) ApplyTo(skill, i, effect, attack, target);
                foreach (CharacterActor target in targets)
                    if (!affected.Contains(target)) affected.Add(target);
            }
            return affected.Count;
        }

        void ApplyTo(SkillDefinition skill, int index, SkillEffect effect, float attack, CharacterActor target)
        {
            if (target == null || target.Health == null || target.Health.IsDead) return;
            float amount = SkillEffectRules.Amount(effect, attack);
            switch (effect.kind)
            {
                case SkillEffectKind.Damage:
                    float minimum = Actor.Profile != null ? Actor.Profile.MinimumDamage : 0f;
                    target.ReceiveDamage(DamageRules.Calculate(amount, target.Stats.GetValue(StatType.Defense), minimum), Actor);
                    break;
                case SkillEffectKind.Heal:
                    target.Health.Heal(amount);
                    break;
                case SkillEffectKind.Buff:
                case SkillEffectKind.Debuff:
                    SkillStatusEffects.On(target).Apply(skill, index, effect.modifier, effect.duration);
                    break;
                case SkillEffectKind.Stagger:
                    target.ApplyStagger(amount);
                    break;
            }
        }

        /// <summary>대상들이 속한 분대마다 한 번씩 사기를 올린다.</summary>
        void RaiseMorale(SkillEffect effect, List<CharacterActor> allies)
        {
            if (context == null) return;
            raisedSquads.Clear();
            foreach (CharacterActor ally in allies)
            {
                int squad = context.SquadOf(ally);
                if (squad >= 0 && raisedSquads.Add(squad)) context.AddMorale(squad, effect.amount);
            }
        }

        bool AnyEffectFits(SkillDefinition skill, CharacterActor unit)
        {
            foreach (SkillEffect effect in skill.Effects)
                if (SkillTargeting.IsSide(Actor, unit, SkillEffectRules.AffectsEnemies(effect.kind))) return true;
            return false;
        }

        void Update()
        {
            if (Actor != null && Actor.Health != null && !Actor.Health.IsDead) Slots.Tick(Time.deltaTime);
        }

        void OnEnable()
        {
            if (!activeCasters.Contains(this)) activeCasters.Add(this);
        }

        void OnDisable() => activeCasters.Remove(this);
    }
}
