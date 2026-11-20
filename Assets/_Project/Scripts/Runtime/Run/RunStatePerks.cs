using System;
using System.Collections.Generic;
using ProjectNeo.Meta;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 이번 런에서 찍은 퍽(META-02). 런마다 새로 생기므로 런이 끝나면 자연히 초기화되고(Q-72), 런 저장에 퍽 id로 남는다.
    /// 퍽은 준비 단계와 보상 화면에서만 찍고 전투 중에는 찍지 않는다(Q-06).
    /// 런 효과(상점 가격·준비 시간)는 여기서 바로 반영하고, 유닛 능력치·특수 효과는 씬 쪽이 <see cref="Perks"/>를 보고 붙인다.
    /// </summary>
    public sealed partial class RunState
    {
        readonly List<PerkDefinition> restoredPerks = new List<PerkDefinition>();

        /// <summary>이번 런의 퍽. <see cref="SetupPerks"/> 전에는 null.</summary>
        public PerkLoadout Perks { get; private set; }

        /// <summary>찍은 퍽이 바뀌었을 때(찍었을 때) 알린다. 씬이 능력치를 다시 붙인다.</summary>
        public event Action<PerkDefinition> PerkAllocated;

        /// <summary>
        /// 메타 진행(퍽 포인트·해금)으로 이 런의 퍽 묶음을 만든다. 이미 있으면 그대로 돌려준다.
        /// 저장에서 되살린 퍽은 여기서 다시 찍는다(포인트가 모자라거나 잠긴 퍽은 빠진다).
        /// </summary>
        public PerkLoadout SetupPerks(MetaProgress progress)
        {
            if (Perks != null) return Perks;
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            Perks = new PerkLoadout(progress);
            foreach (PerkDefinition perk in restoredPerks) Perks.TryAllocate(perk);
            restoredPerks.Clear();
            Perks.PerkAllocated += OnPerkAllocated;
            ApplyShopPerks();
            return Perks;
        }

        /// <summary>지금 이 퍽을 찍을 수 있는지. 전투 중이거나 퍽 묶음이 없으면 false.</summary>
        public bool CanAllocatePerk(PerkDefinition perk) => Perks != null && !HasPendingBattle && Perks.CanAllocate(perk);

        /// <summary>퍽을 찍는다. <see cref="CanAllocatePerk"/>가 false면 아무것도 바꾸지 않는다.</summary>
        public bool TryAllocatePerk(PerkDefinition perk) => CanAllocatePerk(perk) && Perks.TryAllocate(perk);

        /// <summary>찍은 퍽들의 런 효과 합. 퍽 묶음이 없으면 0.</summary>
        public float PerkRunEffect(PerkRunEffectKind kind) => Perks != null ? Perks.RunEffectTotal(kind) : 0f;

        float PerkPrepHours => Math.Max(0f, PerkRunEffect(PerkRunEffectKind.PrepHours));

        /// <summary>승리 보상 골드에 붙는 퍽 덤(반올림). 보상 쪽이 부른다.</summary>
        public int PerkRewardGoldBonus(int gold) =>
            Math.Max(0, (int)Math.Round(Math.Max(0, gold) * PerkRunEffect(PerkRunEffectKind.RewardGoldPercent) / 100.0));

        void OnPerkAllocated(PerkDefinition perk)
        {
            ApplyShopPerks();
            // 이번 준비 시계가 이미 돌고 있으면 늘어난 준비 시간을 바로 더한다.
            float hours = Math.Max(0f, perk.RunEffectTotal(PerkRunEffectKind.PrepHours));
            if (hours > 0f && PrepTime != null && ClockPreparation == BattlesFought && !HasPendingBattle)
                Clock.Restore(Clock.Budget + hours, Clock.Used);
            PerkAllocated?.Invoke(perk);
            Changed?.Invoke();
        }

        void ApplyShopPerks()
        {
            if (Shop != null) Shop.PriceChangePercent = PerkRunEffect(PerkRunEffectKind.ShopPricePercent);
        }

        /// <summary>찍은 퍽 id(저장용). 아직 퍽 묶음이 없으면 되살리려고 들고 있는 퍽.</summary>
        IEnumerable<string> PerkIds()
        {
            IEnumerable<PerkDefinition> source = Perks != null ? Perks.Allocated : (IEnumerable<PerkDefinition>)restoredPerks;
            foreach (PerkDefinition perk in source)
                if (perk != null) yield return perk.Id;
        }

        void RestorePerks(IEnumerable<string> ids, IRunContentLookup content)
        {
            if (ids == null) return;
            foreach (string id in ids)
            {
                // 없어진 퍽은 건너뛴다. 퍽은 런마다 다시 찍으므로 런 저장 전체를 버릴 이유가 없다.
                PerkDefinition perk = content.Perk(id);
                if (perk != null && !restoredPerks.Contains(perk)) restoredPerks.Add(perk);
            }
        }
    }
}
