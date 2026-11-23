using System.Collections.Generic;

namespace ProjectNeo.Siege
{
    /// <summary>양방향이 공유하는 선착순 대기열. 방향이나 진영에 우선권을 주지 않는다.</summary>
    public sealed class LadderTraffic<T> where T : class
    {
        readonly List<T> waiting = new List<T>();
        readonly List<T> approaching = new List<T>();
        public T Occupant { get; private set; }
        public IReadOnlyList<T> Waiting => waiting;
        public IReadOnlyList<T> Approaching => approaching;

        /// <summary>사다리 입구로 이동 중인 접근 예약을 등록한다(BUG-ai-core-defects). 이미 점유/대기 중이면 추가하지 않는다.</summary>
        public void Reserve(T participant)
        {
            if (participant != null && !ReferenceEquals(Occupant, participant)
                && !waiting.Contains(participant) && !approaching.Contains(participant))
                approaching.Add(participant);
        }

        public void Unreserve(T participant)
        {
            if (participant != null) approaching.Remove(participant);
        }

        /// <summary>점유자 + 입구 대기자 + 접근 예약자 수(<paramref name="exclude"/> 본인 제외).</summary>
        public int PlannedLoad(T exclude = null)
        {
            int load = (Occupant != null && !ReferenceEquals(Occupant, exclude) ? 1 : 0)
                + waiting.Count + approaching.Count;
            if (exclude != null)
            {
                if (waiting.Contains(exclude)) load--;
                if (approaching.Contains(exclude)) load--;
            }
            return load;
        }

        public void Join(T participant)
        {
            if (participant == null) return;
            approaching.Remove(participant);
            if (!ReferenceEquals(Occupant, participant) && !waiting.Contains(participant))
                waiting.Add(participant);
        }
        public bool TryEnter(T participant)
        {
            Join(participant);
            if (participant == null || Occupant != null || waiting.Count == 0
                || !ReferenceEquals(waiting[0], participant)) return false;
            waiting.RemoveAt(0);
            Occupant = participant;
            return true;
        }
        public void Leave(T participant)
        {
            approaching.Remove(participant);
            waiting.Remove(participant);
            if (ReferenceEquals(Occupant, participant)) Occupant = null;
        }
        public void Clear() { approaching.Clear(); waiting.Clear(); Occupant = null; }
    }
}
