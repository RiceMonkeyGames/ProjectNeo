using System;
using ProjectNeo.Economy;

namespace ProjectNeo.Facilities
{
    /// <summary>런 동안 시설 하나의 현재 레벨.</summary>
    public sealed class FacilityState
    {
        public FacilityDefinition Definition { get; }

        /// <summary>현재 레벨. 0이 시작 레벨이다.</summary>
        public int Level { get; private set; }

        public event Action Upgraded;

        public FacilityState(FacilityDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.Levels.Count == 0) throw new ArgumentException("시설에 레벨 데이터가 하나도 없습니다.", nameof(definition));
            Definition = definition;
        }

        public FacilityLevel Current => Definition.Levels[Level];

        public bool IsMaxLevel => Level >= Definition.Levels.Count - 1;

        /// <summary>다음 레벨로 올리는 비용. 최대 레벨이면 예외를 던진다.</summary>
        public Cost NextUpgradeCost
        {
            get
            {
                if (IsMaxLevel) throw new InvalidOperationException("이미 최대 레벨입니다.");
                return Definition.Levels[Level + 1].upgradeCost;
            }
        }

        /// <summary>비용을 낼 수 있으면 차감하고 한 레벨 올린다. 최대 레벨이거나 비용이 모자라면 false.</summary>
        public bool TryUpgrade(Wallet wallet)
        {
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (IsMaxLevel) return false;
            if (!wallet.TrySpend(NextUpgradeCost)) return false;
            Level++;
            Upgraded?.Invoke();
            return true;
        }
    }
}
