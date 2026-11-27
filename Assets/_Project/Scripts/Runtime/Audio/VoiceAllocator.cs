using System;

namespace ProjectNeo.Audio
{
    /// <summary>
    /// 정해진 수의 재생 자리(AudioSource)를 나눠 준다. 자리가 모자라면 어느 소리를 끊을지 정한다.
    /// 1) 같은 소리의 동시 재생 상한에 닿으면 그 소리 중 가장 오래된 것을 새 것으로 바꾼다.
    /// 2) 빈 자리가 있으면 쓴다.
    /// 3) 없으면 우선순위가 가장 낮은 것 → 가장 먼 것 → 가장 오래된 것을 고르고,
    ///    그 소리가 새 소리보다 덜 중요하거나(우선순위가 낮음) 같은 우선순위에 더 멀거나 같은 거리일 때만 끊는다.
    ///    아니면 새 소리를 버린다(-1).
    /// </summary>
    public sealed class VoiceAllocator
    {
        struct Slot
        {
            public bool Busy;
            public int Cue;
            public int Priority;
            public float DistanceSq;
            public long Order;
        }

        readonly Slot[] slots;
        long nextOrder;

        public VoiceAllocator(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), "At least one voice is required.");
            slots = new Slot[capacity];
        }

        public int Capacity => slots.Length;

        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < slots.Length; i++) if (slots[i].Busy) count++;
                return count;
            }
        }

        public bool IsBusy(int index) => slots[index].Busy;
        public int CueAt(int index) => slots[index].Busy ? slots[index].Cue : -1;

        public int CountOf(int cue)
        {
            int count = 0;
            for (int i = 0; i < slots.Length; i++) if (slots[i].Busy && slots[i].Cue == cue) count++;
            return count;
        }

        /// <summary>
        /// 자리를 하나 받는다. 받지 못하면 -1. stolen이 true면 그 자리에서 나던 소리를 부르는 쪽이 멈춰야 한다.
        /// priority는 클수록 중요하다. maxVoicesForCue가 0 이하면 같은 소리 상한이 없다.
        /// </summary>
        public int Allocate(int cue, int priority, float distanceSq, int maxVoicesForCue, out bool stolen)
        {
            stolen = false;
            int index;
            if (maxVoicesForCue > 0 && CountOf(cue) >= maxVoicesForCue)
            {
                index = OldestOf(cue);
                stolen = true;
            }
            else
            {
                index = FirstFree();
                if (index < 0)
                {
                    index = Victim();
                    Slot victim = slots[index];
                    bool weaker = victim.Priority < priority
                        || (victim.Priority == priority && victim.DistanceSq >= distanceSq);
                    if (!weaker) return -1;
                    stolen = true;
                }
            }
            slots[index] = new Slot { Busy = true, Cue = cue, Priority = priority, DistanceSq = distanceSq, Order = nextOrder++ };
            return index;
        }

        public void Release(int index)
        {
            if (index >= 0 && index < slots.Length) slots[index].Busy = false;
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < slots.Length; i++) slots[i].Busy = false;
        }

        int FirstFree()
        {
            for (int i = 0; i < slots.Length; i++) if (!slots[i].Busy) return i;
            return -1;
        }

        int OldestOf(int cue)
        {
            int best = -1;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].Busy || slots[i].Cue != cue) continue;
                if (best < 0 || slots[i].Order < slots[best].Order) best = i;
            }
            return best;
        }

        int Victim()
        {
            int best = 0;
            for (int i = 1; i < slots.Length; i++)
            {
                Slot candidate = slots[i], current = slots[best];
                if (candidate.Priority != current.Priority)
                {
                    if (candidate.Priority < current.Priority) best = i;
                    continue;
                }
                if (candidate.DistanceSq != current.DistanceSq)
                {
                    if (candidate.DistanceSq > current.DistanceSq) best = i;
                    continue;
                }
                if (candidate.Order < current.Order) best = i;
            }
            return best;
        }
    }
}
