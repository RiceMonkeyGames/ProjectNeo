using System.Collections.Generic;

namespace ProjectNeo.Siege
{
    /// <summary>양방향이 공유하는 선착순 대기열. 방향이나 진영에 우선권을 주지 않는다.</summary>
    public sealed class LadderTraffic<T> where T : class
    {
        readonly List<T> waiting = new List<T>();
        public T Occupant { get; private set; }
        public IReadOnlyList<T> Waiting => waiting;
        public void Join(T participant)
        {
            if (participant != null && !ReferenceEquals(Occupant, participant) && !waiting.Contains(participant))
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
            waiting.Remove(participant);
            if (ReferenceEquals(Occupant, participant)) Occupant = null;
        }
        public void Clear() { waiting.Clear(); Occupant = null; }
    }
}
