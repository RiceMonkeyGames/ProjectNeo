using ProjectNeo.Economy;
using ProjectNeo.Facilities;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 장인(PREP-11). 건설·수리 주문은 장인 수만큼 동시에 진행하고, 대장간 제작·강화와 시설 강화는 그동안 장인 1명을 쓴다.
    /// 시작 장인 수·대장간 레벨별 상한·고용 골드는 <see cref="PrepTimeSettings"/>에 있다. 고용한 수는 런 저장에 남는다.
    /// </summary>
    public sealed partial class RunState
    {
        /// <summary>대장간 제작·강화와 시설 강화가 그동안 쓰는 장인 수.</summary>
        public const int DirectActionArtisans = 1;

        /// <summary>이번 런에서 고용한 장인 수. 시작 장인은 세지 않는다.</summary>
        public int HiredArtisans { get; private set; }

        /// <summary>장인 수(시작 + 고용). 준비 시간 설정이 없으면 0이다.</summary>
        public int ArtisanCount => PrepTime != null ? PrepTime.StartingArtisans + HiredArtisans : 0;

        /// <summary>
        /// 실제로 일하는 장인 수. 지금은 <see cref="ArtisanCount"/>와 같고, 나중의 퍽·승천·메타 보너스는 여기에 더한다.
        /// </summary>
        public int EffectiveArtisanCount => ArtisanCount;

        /// <summary>지금 대장간 레벨의 장인 상한. 대장간이 없으면 시작 장인 수, 설정이 없으면 0.</summary>
        public int ArtisanCap
        {
            get
            {
                if (PrepTime == null) return 0;
                return Smithy != null ? PrepTime.ArtisanCap(Smithy.Level) : PrepTime.StartingArtisans;
            }
        }

        /// <summary>다음 장인 고용 골드. 설정이 없으면 0.</summary>
        public int NextArtisanHireGold => PrepTime != null ? PrepTime.ArtisanHireGold(HiredArtisans) : 0;

        /// <summary>장인을 고용할 수 있는지. 시간은 쓰지 않는다. 런을 바꾸지 않는다.</summary>
        public ArtisanHireCheck CheckHireArtisan()
        {
            if (PrepTime == null) return ArtisanHireCheck.NotConfigured;
            if (HasPendingBattle) return ArtisanHireCheck.BattlePending;
            if (ArtisanCount >= ArtisanCap) return ArtisanHireCheck.AtCap;
            if (!Wallet.CanAfford(new Cost(NextArtisanHireGold))) return ArtisanHireCheck.NotEnoughFunds;
            return ArtisanHireCheck.Ok;
        }

        /// <summary>장인을 한 명 고용한다(시간 없는 행동). <see cref="CheckHireArtisan"/>가 Ok가 아니면 false이고 런은 바뀌지 않는다.</summary>
        public bool TryHireArtisan()
        {
            if (CheckHireArtisan() != ArtisanHireCheck.Ok) return false;
            if (!Wallet.TrySpend(new Cost(NextArtisanHireGold))) return false;
            HiredArtisans++;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 시설을 한 단계 올릴 수 있는지. 없는 시설 → 최대 레벨 → 비용 → 시간 순으로 처음 걸린 이유를 돌려준다.
        /// 전투 대기 중이면 시간을 따지지 않는다(예전처럼). 런을 바꾸지 않는다.
        /// </summary>
        public FacilityUpgradeCheck CheckUpgradeFacility(FacilityType type)
        {
            FacilityState facility = Facility(type);
            if (facility == null) return FacilityUpgradeCheck.NoFacility;
            if (facility.IsMaxLevel) return FacilityUpgradeCheck.MaxLevel;
            if (!Wallet.CanAfford(facility.NextUpgradeCost)) return FacilityUpgradeCheck.NotEnoughFunds;
            if (!HasPendingBattle && !HasPrepTime(FacilityUpgradeHours)) return FacilityUpgradeCheck.NotEnoughTime;
            return FacilityUpgradeCheck.Ok;
        }

        void RestoreArtisans(int hired) => HiredArtisans = hired > 0 ? hired : 0;
    }
}
