using System;
using System.Collections.Generic;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 작업 주문 목록과 작업대(Q-53, SIEGE-08). 작업대 수만큼 앞에서부터 동시에 짓고, 하나가 끝나면 남은 시간은 다음 주문으로 간다.
    /// 준비 시계가 간 시간만큼 <see cref="Advance"/>로 진행한다. 씬을 모른다.
    /// </summary>
    public sealed class WorkQueue
    {
        readonly List<WorkOrder> orders = new List<WorkOrder>();
        readonly List<WorkOrder> active = new List<WorkOrder>();

        public int Crews { get; private set; }
        /// <summary>주문 순서대로. 끝난 주문도 <see cref="RemoveCompleted"/> 전까지 남는다.</summary>
        public IReadOnlyList<WorkOrder> Orders => orders;
        public int PendingCount { get { int n = 0; foreach (WorkOrder order in orders) if (!order.IsComplete) n++; return n; } }

        /// <summary>주문이 진행되었다(진행률이 바뀌었다).</summary>
        public event Action<WorkOrder> Progressed;
        /// <summary>주문이 끝났다.</summary>
        public event Action<WorkOrder> Completed;

        public WorkQueue(int crews = 2) { SetCrews(crews); }

        public void SetCrews(int crews) => Crews = Math.Max(1, crews);

        public void Add(WorkOrder order)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));
            if (Find(order.Target, order.Key) != null) throw new InvalidOperationException("같은 대상의 주문이 이미 있다.");
            orders.Add(order);
        }

        public WorkOrder Find(WorkTarget target, int key)
        {
            foreach (WorkOrder order in orders)
                if (order.Target == target && order.Key == key) return order;
            return null;
        }

        /// <summary>주문을 지운다(철거 등). 있었으면 그 주문을 돌려준다.</summary>
        public WorkOrder Remove(WorkTarget target, int key)
        {
            WorkOrder found = Find(target, key);
            if (found != null) orders.Remove(found);
            return found;
        }

        public void Clear() => orders.Clear();

        public void RemoveCompleted() => orders.RemoveAll(order => order.IsComplete);

        /// <summary>
        /// 시간을 흘려 주문을 진행한다. 매 순간 끝나지 않은 앞쪽 주문 <see cref="Crews"/>개가 같은 속도로 진행되고,
        /// 하나가 끝나면 그 작업대는 다음 주문을 맡는다. 진행할 주문이 없으면 남은 시간은 버린다.
        /// </summary>
        public void Advance(float hours)
        {
            float left = Math.Max(0f, hours);
            var touched = new List<WorkOrder>();
            while (left > PrepClock.Epsilon)
            {
                active.Clear();
                foreach (WorkOrder order in orders)
                {
                    if (order.IsComplete) continue;
                    active.Add(order);
                    if (active.Count >= Crews) break;
                }
                if (active.Count == 0) break;
                float step = left;
                foreach (WorkOrder order in active) step = Math.Min(step, order.Remaining);
                foreach (WorkOrder order in active)
                {
                    order.Work(step);
                    if (!touched.Contains(order)) touched.Add(order);
                }
                left -= step;
                foreach (WorkOrder order in active)
                    if (order.IsComplete) Completed?.Invoke(order);
            }
            foreach (WorkOrder order in touched) Progressed?.Invoke(order);
        }

        /// <summary>남은 주문을 모두 끝내는 데 드는 시간(작업대를 고려).</summary>
        public float HoursToFinishAll()
        {
            var remaining = new List<float>();
            foreach (WorkOrder order in orders) if (!order.IsComplete) remaining.Add(order.Remaining);
            float total = 0f;
            while (remaining.Count > 0)
            {
                int count = Math.Min(Crews, remaining.Count);
                float step = float.MaxValue;
                for (int i = 0; i < count; i++) step = Math.Min(step, remaining[i]);
                total += step;
                for (int i = count - 1; i >= 0; i--)
                {
                    remaining[i] -= step;
                    if (remaining[i] <= PrepClock.Epsilon) remaining.RemoveAt(i);
                }
            }
            return total;
        }
    }
}
