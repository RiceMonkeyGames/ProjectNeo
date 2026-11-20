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
    /// 퍽을 처음 누르면 확인을 기다리고, 같은 퍽을 다시 누르면 찍는다(사용자 결정 2026-11-20: 해금 창과 같은 방식).
    /// 찍은 퍽은 이번 런이 끝날 때까지 되돌릴 수 없다.
    /// </summary>
    public sealed class PerkWindowModel
    {
        public RunState State { get; }
        public PerkTreeCatalog Trees { get; }
        public PerkDefinition ArmedPerk { get; private set; }
        public string Notice { get; private set; } = string.Empty;

        public PerkWindowModel(RunState state, PerkTreeCatalog trees)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Trees = trees ?? throw new ArgumentNullException(nameof(trees));
        }

        PerkLoadout Perks => State.Perks;

        public string PointsText => Perks == null ? "Perk points: -"
            : $"Perk points: {Perks.AvailablePoints} left (spent {Perks.SpentPoints})";

        public bool IsAllocated(PerkDefinition perk) => Perks != null && perk != null && ContainsAllocated(perk);
        public bool CanAllocate(PerkDefinition perk) => State.CheckPerk(perk) == PerkAllocateCheck.Ok;

        bool ContainsAllocated(PerkDefinition perk)
        {
            foreach (PerkDefinition allocated in Perks.Allocated) if (allocated == perk) return true;
            return false;
        }

        /// <summary>퍽을 누른다. 결과는 해금 창과 같은 세 가지다.</summary>
        public UnlockClickResult Click(PerkDefinition perk)
        {
            PerkAllocateCheck check = State.CheckPerk(perk);
            if (check != PerkAllocateCheck.Ok)
            {
                ArmedPerk = null;
                Notice = Reason(check, perk);
                return UnlockClickResult.Refused;
            }
            if (ArmedPerk != perk)
            {
                ArmedPerk = perk;
                Notice = string.Empty;
                return UnlockClickResult.Armed;
            }
            ArmedPerk = null;
            if (!State.TryAllocatePerk(perk))
            {
                Notice = "Could not take this perk.";
                return UnlockClickResult.Refused;
            }
            Notice = $"Took {perk.DisplayName}.";
            return UnlockClickResult.Opened;
        }

        public void Disarm() => ArmedPerk = null;

        /// <summary>퍽 버튼 글자: 이름, 설명, 상태.</summary>
        public string EntryText(PerkDefinition perk)
        {
            if (perk == null) return string.Empty;
            string head = $"{perk.DisplayName}\n<size=80%>{Describe(perk)}</size>";
            if (IsAllocated(perk)) return head + "\n<size=80%>Taken</size>";
            if (perk == ArmedPerk) return head + $"\n<size=80%>Click again to take ({perk.PointCost} pt)</size>";
            List<PerkDefinition> missing = Perks != null ? Perks.MissingPrerequisites(perk) : new List<PerkDefinition>();
            if (missing.Count > 0) return head + $"\n<size=80%>Needs {Names(missing)}</size>";
            return head + $"\n<size=80%>{perk.PointCost} pt</size>";
        }

        string Reason(PerkAllocateCheck check, PerkDefinition perk) => check switch
        {
            PerkAllocateCheck.AlreadyAllocated => $"{perk.DisplayName} is already taken.",
            PerkAllocateCheck.MissingPrerequisite => $"Take {Names(Perks.MissingPrerequisites(perk))} first.",
            PerkAllocateCheck.NotEnoughPoints => $"Not enough perk points: {perk.PointCost} needed, {Perks.AvailablePoints} left.",
            PerkAllocateCheck.BattlePending => "Perks cannot be taken during battle.",
            PerkAllocateCheck.Locked => $"{perk.DisplayName} is locked.",
            PerkAllocateCheck.NoLoadout => "Perks are not available yet.",
            _ => "This perk does not exist.",
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
                    PerkRunEffectKind.RewardGoldPercent => $"Victory gold {Signed(effect.value)}%",
                    PerkRunEffectKind.ShopPricePercent => $"Shop prices {Signed(effect.value)}%",
                    PerkRunEffectKind.PrepHours => $"Prep time {Signed(effect.value)}h",
                    _ => effect.kind.ToString(),
                });
            foreach (PerkSpecialEffect effect in perk.SpecialEffects)
                if (effect is MoraleBoostPerkEffect morale)
                    parts.Add($"Squad morale {Signed(morale.Amount)} {TriggerName(morale.Trigger)}");
            return string.Join(", ", parts);
        }

        static string TargetName(PerkTarget target) => target switch
        {
            PerkTarget.Player => "Player",
            PerkTarget.Heroes => "Hero",
            _ => "Soldier",
        };

        static string StatName(StatType stat) => stat switch
        {
            StatType.MaxHealth => "health",
            StatType.AttackSpeed => "attack speed",
            StatType.MoveSpeed => "move speed",
            _ => stat.ToString().ToLowerInvariant(),
        };

        static string TriggerName(PerkTrigger trigger) => trigger switch
        {
            PerkTrigger.BattleStart => "at battle start",
            PerkTrigger.AllySquadMemberDied => "when an ally squad member dies",
            _ => "when a skill is used",
        };

        static string Amount(StatModifier modifier) =>
            modifier.kind == ModifierKind.Percent ? Signed(modifier.value * 100f) + "%" : Signed(modifier.value);

        static string Signed(float value) => (value >= 0f ? "+" : "") + value.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
