using System;
using System.Collections.Generic;
using ProjectNeo.Stats;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 고른 승천 단계에서 쌓인 규칙을 합친 결과(FLOW-03, Q-78). 단계 N은 1~N의 규칙을 모두 더한다.
    /// 씬을 모르고, 각 시스템은 여기서 자기 값만 꺼내 쓴다. 단계 0(또는 표 없음)은 아무것도 바꾸지 않는다.
    /// </summary>
    public sealed class AscensionEffects
    {
        public static readonly AscensionEffects None = new AscensionEffects(0, 0f);

        readonly float[] totals = new float[Enum.GetValues(typeof(AscensionRuleKind)).Length];

        /// <summary>적용된 단계(0 = 기본 게임).</summary>
        public int Level { get; }
        /// <summary>점수와 해금 포인트 배율(1 + 단계 × 단계당 배율).</summary>
        public float RewardMultiplier { get; }

        AscensionEffects(int level, float perLevel)
        {
            Level = Math.Max(0, level);
            RewardMultiplier = 1f + Level * Math.Max(0f, perLevel);
        }

        /// <summary>표의 1~<paramref name="level"/>단계 규칙을 합친다. 표가 없으면 <see cref="None"/>. 단계는 표 범위로 맞춘다.</summary>
        public static AscensionEffects For(AscensionTable table, int level)
        {
            if (table == null) return None;
            int clamped = table.Clamp(level);
            if (clamped == 0) return None;
            var effects = new AscensionEffects(clamped, table.RewardMultiplierPerLevel);
            for (int i = 1; i <= clamped; i++)
            {
                AscensionLevel step = table.LevelAt(i);
                if (step?.rules == null) continue;
                foreach (AscensionRule rule in step.rules)
                {
                    int index = (int)rule.kind;
                    if (index >= 0 && index < effects.totals.Length) effects.totals[index] += rule.value;
                }
            }
            return effects;
        }

        /// <summary>쌓인 한 종류의 합. 예: 적 체력 +15% 두 번이면 0.3.</summary>
        public float Total(AscensionRuleKind kind)
        {
            int index = (int)kind;
            return index >= 0 && index < totals.Length ? totals[index] : 0f;
        }

        float Multiplier(AscensionRuleKind kind) => Math.Max(0f, 1f + Total(kind));

        /// <summary>런 시작 골드. 반올림하고 0 아래로 내려가지 않는다.</summary>
        public int StartingGold(int baseGold) => Math.Max(0, (int)Math.Round(baseGold * Multiplier(AscensionRuleKind.StartingGoldPercent), MidpointRounding.AwayFromZero));

        /// <summary>승리 보상 골드. 반올림하고 0 아래로 내려가지 않는다.</summary>
        public int VictoryGold(int baseGold) => Math.Max(0, (int)Math.Round(baseGold * Multiplier(AscensionRuleKind.VictoryGoldPercent), MidpointRounding.AwayFromZero));

        /// <summary>한 준비의 시간 예산. 0 아래로 내려가지 않는다.</summary>
        public float PrepBudgetHours(float baseHours) => Math.Max(0f, baseHours + Total(AscensionRuleKind.PrepBudgetHours));

        /// <summary>성벽 수리 시간.</summary>
        public float RepairHours(float baseHours) => Math.Max(0f, baseHours * Multiplier(AscensionRuleKind.RepairTimePercent));

        /// <summary>새 부대의 시작 사기. 0 ~ 최대 사기 안으로 맞춘다.</summary>
        public float StartingMorale(float baseMorale, float maxMorale) =>
            Math.Min(Math.Max(0f, baseMorale + Total(AscensionRuleKind.StartingMorale)), maxMorale);

        /// <summary>무리 하나의 적 수. 늘어난 수는 올림하고, 원래 1 이상이면 1 아래로 내려가지 않는다.</summary>
        public int EnemyCount(int baseCount)
        {
            if (baseCount <= 0) return baseCount;
            double scaled = CeilClean(baseCount * (double)Multiplier(AscensionRuleKind.EnemyCountPercent));
            return Math.Max(1, (int)scaled);
        }

        /// <summary>
        /// 전장에 나오는 적에게 붙일 스탯 수정값. 보스면 보스 규칙도 더한다. 바뀌는 것이 없으면 빈 목록.
        /// </summary>
        public List<StatModifier> EnemyModifiers(bool isBoss = false)
        {
            var modifiers = new List<StatModifier>();
            float health = Total(AscensionRuleKind.EnemyHealthPercent) + (isBoss ? Total(AscensionRuleKind.BossHealthPercent) : 0f);
            float attack = Total(AscensionRuleKind.EnemyAttackPercent) + (isBoss ? Total(AscensionRuleKind.BossAttackPercent) : 0f);
            float speed = Total(AscensionRuleKind.EnemyMoveSpeedPercent);
            if (health != 0f) modifiers.Add(StatModifier.Percent(StatType.MaxHealth, health));
            if (attack != 0f) modifiers.Add(StatModifier.Percent(StatType.Attack, attack));
            if (speed != 0f) modifiers.Add(StatModifier.Percent(StatType.MoveSpeed, speed));
            return modifiers;
        }

        /// <summary>
        /// 점수나 해금 포인트에 승천 배율을 곱한다(META-04, META-08이 쓴다). 소수점은 올림한다.
        /// </summary>
        public int ScaleReward(int amount) =>
            amount <= 0 ? amount : (int)CeilClean(amount * (double)RewardMultiplier);

        // float 합의 작은 오차(예: 5 × 1.2000000476 = 6.0000002)로 한 칸 더 올림하지 않게 소수 넷째 자리에서 먼저 반올림한다.
        static double CeilClean(double value) => Math.Ceiling(Math.Round(value, 4));
    }
}
