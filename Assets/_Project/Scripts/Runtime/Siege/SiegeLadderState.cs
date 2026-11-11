using System;

namespace ProjectNeo.Siege
{
    public enum LadderPhase { Down = 0, Raising = 1, Up = 2 }

    /// <summary>
    /// 사다리 하나의 상태 규칙. 바닥에 놓임 → 세우는 중 → 세워짐. 세워진 사다리는 한 번에 한 명만 오르고,
    /// 밀어내면 다시 바닥에 놓인다. 씬과 외형은 알지 않는다.
    /// </summary>
    public sealed class SiegeLadderState
    {
        readonly float raiseSeconds;
        readonly float pushSeconds;
        public LadderPhase Phase { get; private set; } = LadderPhase.Down;
        public float RaiseProgress { get; private set; }
        public float PushProgress { get; private set; }
        public bool HasClimber { get; private set; }
        public int ToppleCount { get; private set; }
        /// <summary>쓰러졌다. 인자는 그때 오르던 유닛이 있었는지.</summary>
        public event Action<bool> Toppled;

        public SiegeLadderState(float raiseSeconds, float pushSeconds)
        {
            this.raiseSeconds = Math.Max(0.01f, raiseSeconds);
            this.pushSeconds = Math.Max(0.01f, pushSeconds);
        }

        /// <param name="raiserPresent">세우는 유닛이 사다리 밑에 있는지</param>
        /// <param name="pusherPresent">밀어낼 수 있는 아군이 사다리 위쪽 끝에 있는지</param>
        /// <param name="pushBlocked">올라온 적이 있어 밀 수 없는지</param>
        public void Tick(float deltaTime, bool raiserPresent, bool pusherPresent, bool pushBlocked)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime)) return;
            switch (Phase)
            {
                case LadderPhase.Down:
                    if (raiserPresent) Phase = LadderPhase.Raising;
                    break;
                case LadderPhase.Raising:
                    // 세우던 유닛이 죽거나 떠나면 사다리는 다시 바닥에 놓인다.
                    if (!raiserPresent) { Phase = LadderPhase.Down; RaiseProgress = 0f; break; }
                    RaiseProgress = Math.Min(1f, RaiseProgress + deltaTime / raiseSeconds);
                    if (RaiseProgress >= 1f) Phase = LadderPhase.Up;
                    break;
                case LadderPhase.Up:
                    if (!pusherPresent || pushBlocked) { PushProgress = 0f; break; }
                    PushProgress = Math.Min(1f, PushProgress + deltaTime / pushSeconds);
                    if (PushProgress >= 1f) Topple();
                    break;
            }
        }

        public bool TryBeginClimb()
        {
            if (Phase != LadderPhase.Up || HasClimber) return false;
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
            HasClimber = false;
            ToppleCount++;
            Toppled?.Invoke(hadClimber);
        }
    }
}
