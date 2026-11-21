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

        public string PointsText => Perks == null ? "특성 포인트: -"
            : $"특성 포인트: {Perks.AvailablePoints} 남음 (사용 {Perks.SpentPoints})";

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
                Notice = "이 특성을 얻지 못했습니다.";
                return UnlockClickResult.Refused;
            }
            Notice = $"{perk.DisplayName} 획득.";
            return UnlockClickResult.Opened;
        }

        public void Disarm() => ArmedPerk = null;

        /// <summary>퍽 버튼 글자: 이름, 설명, 상태.</summary>
        public string EntryText(PerkDefinition perk)
        {
            if (perk == null) return string.Empty;
            string head = $"{perk.DisplayName}\n<size=80%>{Describe(perk)}</size>";
            if (IsAllocated(perk)) return head + "\n<size=80%>획득함</size>";
            if (perk == ArmedPerk) return head + $"\n<size=80%>한 번 더 클릭해서 획득 ({perk.PointCost}포인트)</size>";
            List<PerkDefinition> missing = Perks != null ? Perks.MissingPrerequisites(perk) : new List<PerkDefinition>();
            if (missing.Count > 0) return head + $"\n<size=80%>필요: {Names(missing)}</size>";
            return head + $"\n<size=80%>{perk.PointCost}포인트</size>";
        }

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
