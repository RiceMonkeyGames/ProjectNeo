namespace ProjectNeo.Run
{
    /// <summary>준비 시간(Q-53, PREP-06). 상점 매물처럼 준비마다 한 번 새로 시작하고 런 저장에 함께 남는다.</summary>
    public sealed partial class RunState
    {
        /// <summary>준비 시간 수치. 없으면 시간 제약을 쓰지 않는다(모든 행동이 시간 없이 된다).</summary>
        public PrepTimeSettings PrepTime { get; private set; }
        /// <summary>지금 준비의 시계. 설정이 없으면 예산 0인 채로 쓰이지 않는다.</summary>
        public PrepClock Clock { get; } = new PrepClock();
        /// <summary>시계를 시작한 준비 번호(그때의 <see cref="BattlesFought"/>). 아직 없으면 -1.</summary>
        public int ClockPreparation { get; private set; } = -1;

        public void SetupPrepTime(PrepTimeSettings settings)
        {
            if (PrepTime == null && settings != null) PrepTime = settings;
        }

        /// <summary>
        /// 이번 준비의 시계를 아직 시작하지 않았으면 예산을 채운다. 시작했으면 true. 전투 대기 중이거나 설정이 없으면 하지 않는다.
        /// 남은 시간은 넘어가지 않는다(Q-53).
        /// </summary>
        public bool BeginPreparationClockIfNeeded()
        {
            if (PrepTime == null || HasPendingBattle || ClockPreparation == BattlesFought) return false;
            ClockPreparation = BattlesFought;
            // 승천의 준비 시간 감소(FLOW-03)는 기본 예산에, 퍽의 추가 시간(META-02)은 그 위에 더한다.
            Clock.Begin(Ascension.PrepBudgetHours(PrepTime.BudgetHours) + PerkPrepHours);
            Changed?.Invoke();
            return true;
        }

        /// <summary>시간을 쓰는 행동을 지금 할 수 있는가. 시간 설정이 없으면 언제나 된다.</summary>
        public bool HasPrepTime(float hours)
        {
            if (PrepTime == null) return true;
            BeginPreparationClockIfNeeded();
            return Clock.CanSpend(hours);
        }

        /// <summary>준비 시간을 쓴다. 전투 대기 중이거나 모자라면 false이고 바뀌는 것이 없다. 시간 설정이 없으면 true다.</summary>
        /// <param name="occupiedArtisans">이 행동이 그동안 쓰는 장인 수(PREP-11). 대장간·시설 강화는 1, 기다리기·승급은 0.</param>
        public bool TrySpendPrepTime(float hours, int occupiedArtisans = 0)
        {
            if (PrepTime == null) return true;
            if (HasPendingBattle || !HasPrepTime(hours)) return false;
            if (!Clock.TrySpend(hours, occupiedArtisans)) return false;
            if (hours > 0f) Changed?.Invoke();
            return true;
        }

        /// <summary>기다리기를 할 수 있는가. 시간 설정이 있고, 준비 중이며, 한 번 기다릴 시간이 남아 있어야 한다.</summary>
        public bool CanWait => PrepTime != null && PrepTime.WaitHours > 0f && !HasPendingBattle && HasPrepTime(PrepTime.WaitHours);

        /// <summary>기다리기 한 번(Q-53). 시계만 앞으로 보낸다. 못 하면 false.</summary>
        public bool TryWait() => CanWait && TrySpendPrepTime(PrepTime.WaitHours);

        float CraftHours => PrepTime != null ? PrepTime.CraftHours : 0f;
        float UpgradeHours => PrepTime != null ? PrepTime.UpgradeHours : 0f;
        /// <summary>시설 한 단계를 올리는 준비 시간(PREP-11). 시간 설정이 없으면 0.</summary>
        public float FacilityUpgradeHours => PrepTime != null ? PrepTime.FacilityUpgradeHours : 0f;

        void RestorePrepClock(PrepTimeRecord record)
        {
            if (record == null || PrepTime == null || record.preparation < 0) return;
            ClockPreparation = record.preparation;
            Clock.Restore(record.budget, record.used);
        }
    }
}
