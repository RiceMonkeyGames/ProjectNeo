using System;
using System.Collections.Generic;
using ProjectNeo.Stats;

namespace ProjectNeo.Meta
{
    /// <summary>
    /// 이번 런에서 찍은 퍽. 남은 퍽 포인트가 있으면 찍을 수 있고(언제 찍을지는 런이 정한다, Q-06),
    /// 런이 끝나면 찍은 내역만 사라진다(런마다 새로 만든다, META-02). 퍽 포인트 개수(MetaProgress)는 그대로 남는다.
    /// </summary>
    public sealed class PerkLoadout
    {
        readonly MetaProgress progress;
        readonly List<PerkDefinition> allocated = new List<PerkDefinition>();

        public IReadOnlyList<PerkDefinition> Allocated => allocated;

        /// <summary>퍽을 찍은 직후 호출된다. 스탯을 가진 쪽이 이걸 듣고 바로 효과를 붙인다.</summary>
        public event Action<PerkDefinition> PerkAllocated;
        /// <summary>찍은 퍽을 모두 지운 직후 호출된다. 인자는 지워진 퍽들이다.</summary>
        public event Action<IReadOnlyList<PerkDefinition>> PerksReset;

        public PerkLoadout(MetaProgress progress)
        {
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
        }

        public int SpentPoints
        {
            get
            {
                int spent = 0;
                foreach (PerkDefinition perk in allocated) spent += perk.PointCost;
                return spent;
            }
        }

        /// <summary>
        /// 저장되지 않는 덤 포인트. 에디터 전용 개발 단축키와 테스트가 쓴다(META-02). 이어 하기 뒤에는 0이므로
        /// 덤으로 찍은 퍽은 포인트가 모자라면 되살리지 않는다.
        /// </summary>
        public int BonusPoints { get; set; }

        public int AvailablePoints => progress.perkPoints + BonusPoints - SpentPoints;

        /// <summary>찍은 퍽들의 런 효과 중 한 종류의 합(META-02).</summary>
        public float RunEffectTotal(PerkRunEffectKind kind)
        {
            float total = 0f;
            foreach (PerkDefinition perk in allocated) total += perk.RunEffectTotal(kind);
            return total;
        }

        public bool CanAllocate(PerkDefinition perk) => Check(perk) == PerkAllocateCheck.Ok;

        /// <summary>
        /// 이 퍽을 찍을 수 있는지와 안 되는 이유(META-09). 이미 찍음 → 잠김 → 선행 퍽 → 포인트 순으로 처음 걸린 것을 돌려준다.
        /// 아무것도 바꾸지 않는다.
        /// </summary>
        public PerkAllocateCheck Check(PerkDefinition perk)
        {
            if (perk == null) return PerkAllocateCheck.UnknownPerk;
            if (allocated.Contains(perk)) return PerkAllocateCheck.AlreadyAllocated;
            if (!progress.IsUnlocked(perk)) return PerkAllocateCheck.Locked;
            foreach (PerkDefinition required in perk.Prerequisites)
                if (required != null && !allocated.Contains(required)) return PerkAllocateCheck.MissingPrerequisite;
            return perk.PointCost <= AvailablePoints ? PerkAllocateCheck.Ok : PerkAllocateCheck.NotEnoughPoints;
        }

        /// <summary>아직 찍지 않은 선행 퍽.</summary>
        public List<PerkDefinition> MissingPrerequisites(PerkDefinition perk)
        {
            var missing = new List<PerkDefinition>();
            if (perk == null) return missing;
            foreach (PerkDefinition required in perk.Prerequisites)
                if (required != null && !allocated.Contains(required)) missing.Add(required);
            return missing;
        }

        public bool TryAllocate(PerkDefinition perk)
        {
            if (!CanAllocate(perk)) return false;
            allocated.Add(perk);
            PerkAllocated?.Invoke(perk);
            return true;
        }

        public void Reset()
        {
            if (allocated.Count == 0) return;
            var removed = new List<PerkDefinition>(allocated);
            allocated.Clear();
            PerksReset?.Invoke(removed);
        }

        /// <summary>퍽 하나의 스탯 효과 중 이 대상에 해당하는 것을 스탯 묶음에 붙인다. 출처는 퍽이다.</summary>
        public static void ApplyStatEffects(PerkDefinition perk, StatSheet stats, PerkTarget target)
        {
            if (perk == null) throw new ArgumentNullException(nameof(perk));
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            foreach (PerkStatEffect effect in perk.StatEffects)
                if (effect.target == target) stats.AddModifier(effect.modifier, perk);
        }

        /// <summary>지금까지 찍은 모든 퍽의 스탯 효과를 붙인다. 새로 생긴 유닛에 쓴다.</summary>
        public void ApplyAllStatEffects(StatSheet stats, PerkTarget target)
        {
            foreach (PerkDefinition perk in allocated)
                ApplyStatEffects(perk, stats, target);
        }

        /// <summary>전투에서 어떤 시점이 왔을 때 호출한다. 그 시점에 반응하는 특수 효과가 발동한다.</summary>
        public void RaiseTrigger(PerkTrigger trigger, IPerkEffectContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            foreach (PerkDefinition perk in allocated)
                foreach (PerkSpecialEffect effect in perk.SpecialEffects)
                    if (effect != null && effect.Trigger == trigger) effect.Apply(context);
        }
    }
}
