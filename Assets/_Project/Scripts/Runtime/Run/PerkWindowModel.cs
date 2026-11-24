using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ProjectNeo.Meta;
using ProjectNeo.Stats;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 퍽 창의 규칙과 글자(META-09). 화면 오브젝트를 모르는 순수 C#이다.
    /// 카드를 고르면 오른쪽 상세에 보이고, 획득 버튼을 눌러야 찍는다(사용자 결정 2026-11-24, UI-13 C안. 이전: 같은 퍽 두 번 클릭).
    /// 찍은 퍽은 이번 런이 끝날 때까지 되돌릴 수 없다.
    /// </summary>
    public sealed class PerkWindowModel
    {
        public RunState State { get; }
        public PerkTreeCatalog Trees { get; }
        public PerkDefinition SelectedPerk { get; private set; }
        public string Notice { get; private set; } = string.Empty;

        public PerkWindowModel(RunState state, PerkTreeCatalog trees)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Trees = trees ?? throw new ArgumentNullException(nameof(trees));
        }

        PerkLoadout Perks => State.Perks;

        public string PointsText => Perks == null ? "특성 포인트: -"
            : $"특성 포인트: {Perks.AvailablePoints} 남음 (사용 {Perks.SpentPoints})";

        public bool IsAllocated(PerkDefinition perk) => Perks != null && perk != null && ContainsAllocated(perk);
        public bool CanAllocate(PerkDefinition perk) => State.CheckPerk(perk) == PerkAllocateCheck.Ok;

        bool ContainsAllocated(PerkDefinition perk)
        {
            foreach (PerkDefinition allocated in Perks.Allocated) if (allocated == perk) return true;
            return false;
        }

        /// <summary>카드를 고른다(UI-13 C안). 고르기만 하고 얻지는 않는다. 얻는 것은 <see cref="Acquire"/>.</summary>
        public void Select(PerkDefinition perk)
        {
            SelectedPerk = perk;
            Notice = string.Empty;
        }

        /// <summary>고른 퍽을 얻는다. 결과는 해금 창과 같은 방식이고, 얻으면 Opened, 못 얻으면 이유를 Notice에 두고 Refused.</summary>
        public UnlockClickResult Acquire()
        {
            PerkDefinition perk = SelectedPerk;
            PerkAllocateCheck check = State.CheckPerk(perk);
            if (check != PerkAllocateCheck.Ok)
            {
                Notice = perk == null ? "얻을 특성을 먼저 고르세요." : Reason(check, perk);
                return UnlockClickResult.Refused;
            }
            if (!State.TryAllocatePerk(perk))
            {
                Notice = "이 특성을 얻지 못했습니다.";
                return UnlockClickResult.Refused;
            }
            Notice = $"{perk.DisplayName} 획득.";
            return UnlockClickResult.Opened;
        }

        /// <summary>타일의 짧은 상태 글: 획득함 / 필요한 선행 / 획득 가능. 모든 퍽이 1포인트라 비용은 쓰지 않는다(사용자 2026-11-24).</summary>
        public string StateText(PerkDefinition perk)
        {
            if (perk == null) return string.Empty;
            if (IsAllocated(perk)) return "획득함";
            List<PerkDefinition> missing = Perks != null ? Perks.MissingPrerequisites(perk) : new List<PerkDefinition>();
            if (missing.Count > 0) return $"필요: {Names(missing)}";
            return "획득 가능";
        }

        /// <summary>카드 글자: 이름과 상태.</summary>
        public string EntryText(PerkDefinition perk) => perk == null ? string.Empty
            : $"{perk.DisplayName}\n<size=80%>{StateText(perk)}</size>";

        /// <summary>오른쪽 상세 글자: 이름, 효과 설명, 선행. 비용은 모두 1포인트라 쓰지 않는다.</summary>
        public string DetailText(PerkDefinition perk)
        {
            if (perk == null) return "특성을 고르세요.";
            string text = $"{perk.DisplayName}\n\n{Describe(perk)}";
            if (perk.Prerequisites != null && perk.Prerequisites.Count > 0)
            {
                var names = new List<string>();
                foreach (PerkDefinition need in perk.Prerequisites) if (need != null) names.Add(need.DisplayName);
                text += $"\n\n선행{string.Join(", ", names)}";
            }
            return text;
        }

        /// <summary>상세의 획득 버튼 글자.</summary>
        public string AcquireLabel => SelectedPerk != null && IsAllocated(SelectedPerk) ? "획득함" : "획득";

        string Reason(PerkAllocateCheck check, PerkDefinition perk) => check switch
        {
            PerkAllocateCheck.AlreadyAllocated => $"{perk.DisplayName}은(는) 이미 얻었습니다.",
            PerkAllocateCheck.MissingPrerequisite => $"먼저 {Names(Perks.MissingPrerequisites(perk))}을(를) 얻으세요.",
            PerkAllocateCheck.NotEnoughPoints => $"특성 포인트가 부족합니다: {perk.PointCost} 필요, {Perks.AvailablePoints} 남음.",
            PerkAllocateCheck.BattlePending => "전투 중에는 특성을 얻을 수 없습니다.",
            PerkAllocateCheck.Locked => $"{perk.DisplayName}은(는) 잠겨 있습니다.",
            PerkAllocateCheck.NoLoadout => "아직 특성을 사용할 수 없습니다.",
            _ => "없는 특성입니다.",
        };

        static string Names(List<PerkDefinition> perks)
        {
            var names = new List<string>();
            foreach (PerkDefinition perk in perks) names.Add(perk.DisplayName);
            return string.Join(", ", names);
        }

        /// <summary>퍽 효과를 한 줄로 설명한다. 데이터에 설명이 있으면 그것을 쓴다.</summary>
        public static string Describe(PerkDefinition perk)
        {
            if (perk == null) return string.Empty;
            if (!string.IsNullOrEmpty(perk.Description)) return perk.Description;
            var parts = new List<string>();
            foreach (PerkStatEffect effect in perk.StatEffects)
                parts.Add($"{TargetName(effect.target)} {StatName(effect.modifier.stat)} {Amount(effect.modifier)}");
            foreach (PerkRunEffect effect in perk.RunEffects)
                parts.Add(effect.kind switch
                {
                    PerkRunEffectKind.RewardGoldPercent => $"승리 골드 {Signed(effect.value)}%",
                    PerkRunEffectKind.ShopPricePercent => $"상점 가격 {Signed(effect.value)}%",
                    PerkRunEffectKind.PrepHours => $"준비 시간 {Signed(effect.value)}시간",
                    _ => effect.kind.ToString(),
                });
            foreach (PerkSpecialEffect effect in perk.SpecialEffects)
                if (effect is MoraleBoostPerkEffect morale)
                    parts.Add($"부대 사기 {Signed(morale.Amount)} {TriggerName(morale.Trigger)}");
            return string.Join(", ", parts);
        }

        static string TargetName(PerkTarget target) => target switch
        {
            PerkTarget.Player => "플레이어",
            PerkTarget.Heroes => "영웅",
            _ => "병사",
        };

        static string StatName(StatType stat) => stat switch
        {
            StatType.MaxHealth => "체력",
            StatType.Attack => "공격력",
            StatType.Defense => "방어력",
            StatType.AttackSpeed => "공격 속도",
            StatType.MoveSpeed => "이동 속도",
            StatType.AttackRange => "사거리",
            StatType.AimSpread => "탄퍼짐",
            _ => stat.ToString(),
        };

        static string TriggerName(PerkTrigger trigger) => trigger switch
        {
            PerkTrigger.BattleStart => "(전투 시작 시)",
            PerkTrigger.AllySquadMemberDied => "(아군 부대원이 죽을 때)",
            _ => "(스킬을 쓸 때)",
        };

        static string Amount(StatModifier modifier) =>
            modifier.kind == ModifierKind.Percent ? Signed(modifier.value * 100f) + "%" : Signed(modifier.value);

        static string Signed(float value) => (value >= 0f ? "+" : "") + value.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
