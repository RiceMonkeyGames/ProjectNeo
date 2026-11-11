using System;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 수성전 프로토타입의 임시 건설 코스트. 이 시험에서만 쓰며 런 동안 쓰는 재화와는 별개다.
    /// </summary>
    public sealed class BuildBudget
    {
        public int Total { get; }
        public int Remaining { get; private set; }
        public event Action Changed;

        public BuildBudget(int total)
        {
            Total = Math.Max(0, total);
            Remaining = Total;
        }

        public bool CanAfford(int cost) => cost >= 0 && cost <= Remaining;

        public bool TrySpend(int cost)
        {
            if (!CanAfford(cost)) return false;
            Remaining -= cost;
            Changed?.Invoke();
            return true;
        }

        /// <summary>철거한 만큼 돌려준다. 처음 받은 양을 넘지 않는다.</summary>
        public void Refund(int cost)
        {
            if (cost <= 0) return;
            Remaining = Math.Min(Total, Remaining + cost);
            Changed?.Invoke();
        }
    }
}
