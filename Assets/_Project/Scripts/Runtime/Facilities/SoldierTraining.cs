using System;
using System.Collections.Generic;
using ProjectNeo.Stats;

namespace ProjectNeo.Facilities
{
    /// <summary>
    /// 병영(훈련장) 레벨에 따른 잡졸 능력치 보너스(D-4, Q-37). 레벨이 오르면 따로 훈련하지 않아도 모든 잡졸에게 바로 붙는다.
    /// 한 출처로 붙여 두고 다시 적용할 때 통째로 바꾸므로, 여러 번 불러도 보너스가 겹치지 않는다.
    /// </summary>
    public static class SoldierTraining
    {
        /// <summary>훈련 보너스를 붙일 때 쓰는 출처. 다른 수정값(장비 등)과 섞이지 않는다.</summary>
        public static readonly object Source = new object();

        static readonly StatModifier[] None = new StatModifier[0];

        /// <summary>지금 병영 레벨의 보너스. 병영이 없거나 보너스가 비었으면 빈 목록.</summary>
        public static IReadOnlyList<StatModifier> ModifiersOf(FacilityState trainingGround)
        {
            if (trainingGround == null || trainingGround.Definition.Type != FacilityType.TrainingGround) return None;
            List<StatModifier> modifiers = trainingGround.Current.soldierModifiers;
            return modifiers != null ? (IReadOnlyList<StatModifier>)modifiers : None;
        }

        /// <summary>잡졸 스탯의 훈련 보너스를 지금 병영 레벨 것으로 바꾼다. 병영이 없으면 보너스를 지운다.</summary>
        public static void Apply(StatSheet stats, FacilityState trainingGround)
        {
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            stats.RemoveModifiersFrom(Source);
            stats.AddModifiers(ModifiersOf(trainingGround), Source);
        }
    }
}
