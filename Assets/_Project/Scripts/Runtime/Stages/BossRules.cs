using System;
using System.Collections.Generic;
using ProjectNeo.Stats;

namespace ProjectNeo.Stages
{
    /// <summary>보스(SIEGE-09)의 수치 규칙. 씬을 모르는 순수 규칙이다.</summary>
    public static class BossRules
    {
        /// <summary>
        /// 바탕 적의 기본 능력치를 보스 목표값으로 바꾸는 수정값. 목표값이 0인 능력치는 건드리지 않는다.
        /// 덧셈(Flat)으로 맞추므로 승천의 비율 수정값(FLOW-03)은 보스 값 위에 곱해진다.
        /// </summary>
        public static List<StatModifier> StatModifiers(BossDefinition boss, Func<StatType, float> baseValue)
        {
            if (boss == null) throw new ArgumentNullException(nameof(boss));
            if (baseValue == null) throw new ArgumentNullException(nameof(baseValue));
            var list = new List<StatModifier>();
            AddTarget(list, StatType.MaxHealth, boss.MaxHealth, baseValue);
            AddTarget(list, StatType.Attack, boss.Attack, baseValue);
            AddTarget(list, StatType.Defense, boss.Defense, baseValue);
            AddTarget(list, StatType.AttackRange, boss.AttackRange, baseValue);
            AddTarget(list, StatType.AimSpread, boss.AimSpread, baseValue);
            if (!Approximately(boss.MoveSpeedMultiplier, 1f))
                list.Add(StatModifier.Percent(StatType.MoveSpeed, boss.MoveSpeedMultiplier - 1f));
            return list;
        }

        static void AddTarget(List<StatModifier> list, StatType stat, float target, Func<StatType, float> baseValue)
        {
            if (target <= 0f) return;
            float delta = target - baseValue(stat);
            if (!Approximately(delta, 0f)) list.Add(StatModifier.Flat(stat, delta));
        }

        /// <summary>지휘형 강화가 주변 적에게 붙이는 수정값. 강화가 없으면 빈 목록.</summary>
        public static List<StatModifier> AuraModifiers(BossDefinition boss)
        {
            var list = new List<StatModifier>();
            if (boss == null || !boss.HasAura) return list;
            if (!Approximately(boss.AuraAttackPercent, 0f)) list.Add(StatModifier.Percent(StatType.Attack, boss.AuraAttackPercent));
            if (!Approximately(boss.AuraDefenseFlat, 0f)) list.Add(StatModifier.Flat(StatType.Defense, boss.AuraDefenseFlat));
            return list;
        }

        /// <summary>지휘형이 죽은 뒤 같은 웨이브의 남은 적에게 붙이는 약화. 지휘형이 아니거나 값이 0이면 빈 목록.</summary>
        public static List<StatModifier> DeathPenaltyModifiers(BossDefinition boss)
        {
            var list = new List<StatModifier>();
            if (boss == null || boss.Role != BossRole.Commander || boss.DeathAttackPenalty <= 0f) return list;
            list.Add(StatModifier.Percent(StatType.Attack, -boss.DeathAttackPenalty));
            return list;
        }

        /// <summary>강화 반경 안인지. 경계(같은 거리)도 안으로 본다.</summary>
        public static bool InAura(BossDefinition boss, float distanceSquared) =>
            boss != null && boss.HasAura && distanceSquared >= 0f && distanceSquared <= boss.AuraRadius * boss.AuraRadius;

        /// <summary>
        /// 결투형이 대상을 고를 때 쓰는 제곱 거리 배율. 플레이어·영웅이면 1/선호²(더 가깝게 본다), 아니면 1.
        /// 결투형이 아니면 언제나 1이다.
        /// </summary>
        public static float TargetDistanceScale(BossDefinition boss, bool isHero)
        {
            if (boss == null || !boss.PrefersHeroes || !isHero) return 1f;
            float preference = Math.Max(1f, boss.HeroPreference);
            return 1f / (preference * preference);
        }

        /// <summary>성문·성벽·공성 무기에 주는 피해. 배율은 0 아래로 내려가지 않는다.</summary>
        public static float StructureDamage(float damage, BossDefinition boss) =>
            boss == null ? damage : damage * Math.Max(0f, boss.StructureDamageMultiplier);

        static bool Approximately(float a, float b) => Math.Abs(a - b) < 0.0001f;
    }
}
