using System;

namespace ProjectNeo.Siege
{
    public enum LadderPhase { Down = 0, Raising = 1, Up = 2 }

    /// <summary>
    /// 사다리 하나의 상태 규칙. 바닥에 놓임 → 세우는 중 → 세워짐. 세워진 사다리는 한 번에 한 명만 오르고,
    /// 밀어내면 다시 바닥에 놓인다. 수직 균형점 전의 취소는 복귀하고, 넘긴 뒤에는 자동으로 안착한다.
    /// 씬과 외형은 알지 않는다.
    /// </summary>
    public sealed class SiegeLadderState
    {
        readonly float raiseSeconds;
        readonly float pushSeconds;
        readonly LadderPushTuning pushTuning;
        bool returning, committed;
        public float BalancePoint { get; private set; }
        public bool IsSettling => returning || committed;
        public bool CanBoard => Phase == LadderPhase.Up && PushProgress == 0f;
        public float RaisedFraction => Phase == LadderPhase.Up ? 1f - PushProgress
            : Phase == LadderPhase.Raising ? RaiseProgress : 0f;
        public LadderPhase Phase { get; private set; } = LadderPhase.Down;
        public float RaiseProgress { get; private set; }
        public float PushProgress { get; private set; }
        public bool HasClimber { get; private set; }
        public int ToppleCount { get; private set; }
        /// <summary>쓰러졌다. 인자는 그때 오르던 유닛이 있었는지.</summary>
        public event Action<bool> Toppled;
        /// <summary>밀기가 수직 균형점을 넘어 탑승자가 떨어진다. 완전히 눕는 시점과 구분한다.</summary>
        public event Action ClimberDislodged;

        /// <param name="pushSeconds">눕는 전체 회전 시간. 균형점 이후 자동 안착과 취소 복귀의 속도다.</param>
        /// <param name="pushTuning">균형점까지 밀어야 하는 시간과 무게·협동 배율. 없으면 pushSeconds 속도로 민다.</param>
        public SiegeLadderState(float raiseSeconds, float pushSeconds, float balancePoint = 0.5f,
            LadderPushTuning pushTuning = null)
        {
            this.raiseSeconds = Math.Max(0.01f, raiseSeconds);
            this.pushSeconds = Math.Max(0.01f, pushSeconds);
            this.pushTuning = pushTuning;
            ConfigureBalancePoint(balancePoint);
        }

        /// <summary>
        /// 세워진 사다리를 지금 탑승 상태와 주어진 인원으로 균형점까지 미는 데 걸리는 시간.
        /// 균형점을 넘으면 손을 떼도 넘어가므로 이 시간이 실제로 버텨야 하는 시간이다.
        /// </summary>
        public float PushToBalanceSeconds(int pushers)
        {
            if (pushers <= 0) return float.PositiveInfinity;
            if (pushTuning == null) return pushSeconds * (1f - BalancePoint);
            return pushTuning.SecondsToBalance(pushers, HasClimber);
        }

        float PushRate(int pushers)
        {
            float span = 1f - BalancePoint;
            // 균형점이 세워진 자세와 같으면 밀자마자 넘어가므로 안착 속도만 쓴다.
            if (span <= 0f) return 1f / pushSeconds;
            return span / Math.Max(0.0001f, PushToBalanceSeconds(pushers));
        }

        /// <summary>누운 자세에서 성벽에 기댄 자세까지 회전 중 수직을 지나는 비율.</summary>
        public void ConfigureBalancePoint(float raisedFraction)
            => BalancePoint = Math.Max(0f, Math.Min(1f, raisedFraction));

        /// <param name="raiserPresent">세우는 유닛이 사다리 밑에 있는지</param>
        /// <param name="pusherPresent">밀어낼 수 있는 아군이 사다리 위쪽 끝에 있는지(한 명으로 친다)</param>
        /// <param name="pushBlocked">올라온 적이 있어 밀 수 없는지</param>
        public void Tick(float deltaTime, bool raiserPresent, bool pusherPresent, bool pushBlocked)
            => Tick(deltaTime, raiserPresent, pusherPresent ? 1 : 0, pushBlocked);

        /// <param name="pusherCount">사다리 위쪽 끝에서 함께 미는 아군 수</param>
        public void Tick(float deltaTime, bool raiserPresent, int pusherCount, bool pushBlocked)
        {
            bool pusherPresent = pusherCount > 0;
            if (deltaTime <= 0f || float.IsNaN(deltaTime)) return;
            switch (Phase)
            {
                case LadderPhase.Down:
                    if (raiserPresent) Phase = LadderPhase.Raising;
                    break;
                case LadderPhase.Raising:
                    if (!committed && !raiserPresent) returning = true;
                    if (returning)
                    {
                        RaiseProgress = Math.Max(0f, RaiseProgress - deltaTime / raiseSeconds);
                        if (RaiseProgress == 0f) { Phase = LadderPhase.Down; returning = false; }
                        break;
                    }
                    RaiseProgress = Math.Min(1f, RaiseProgress + deltaTime / raiseSeconds);
                    if (RaiseProgress >= BalancePoint) committed = true;
                    if (RaiseProgress >= 1f) { Phase = LadderPhase.Up; committed = false; }
                    break;
                case LadderPhase.Up:
                    if (!committed && (!pusherPresent || pushBlocked)) returning = PushProgress > 0f;
                    if (returning)
                    {
                        PushProgress = Math.Max(0f, PushProgress - deltaTime / pushSeconds);
                        if (PushProgress == 0f) returning = false;
                        break;
                    }
                    if (!committed && (!pusherPresent || pushBlocked)) break;
                    // 균형점 전에는 미는 인원과 무게가 속도를 정하고, 넘은 뒤에는 기존 안착 속도로 넘어간다.
                    float rate = committed ? 1f / pushSeconds : PushRate(pusherCount);
                    float nextPushProgress = Math.Min(1f, PushProgress + deltaTime * rate);
                    if (!committed && nextPushProgress >= 1f - BalancePoint)
                    {
                        // 긴 프레임에서도 낙하는 균형점의 위치에서 시작한다.
                        PushProgress = 1f - BalancePoint;
                        committed = true;
                        if (HasClimber)
                        {
                            HasClimber = false;
                            ClimberDislodged?.Invoke();
                        }
                    }
                    PushProgress = nextPushProgress;
                    if (PushProgress >= 1f) Topple();
                    break;
            }
        }

        public bool TryBeginClimb()
        {
            if (!CanBoard || HasClimber) return false;
            HasClimber = true;
            return true;
        }

        public void EndClimb() => HasClimber = false;

        /// <summary>세워졌거나 세우는 중인 사다리를 바닥으로 돌린다. 성벽이 사라졌을 때도 쓴다.</summary>
        public void Topple()
        {
            if (Phase == LadderPhase.Down && !HasClimber) return;
            bool hadClimber = HasClimber;
            Phase = LadderPhase.Down;
            RaiseProgress = 0f;
            PushProgress = 0f;
            returning = committed = false;
            HasClimber = false;
            ToppleCount++;
            Toppled?.Invoke(hadClimber);
        }
    }
}
