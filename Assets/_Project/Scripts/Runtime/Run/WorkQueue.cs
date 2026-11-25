using System;
using System.Collections.Generic;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 작업 주문 목록과 장인 자리(Q-53, SIEGE-08, PREP-11). <see cref="Crews"/>는 장인 수이고, 그만큼 앞에서부터 동시에 짓는다.
    /// 하나가 끝나면 남은 시간은 다음 주문으로 간다. 목록 순서가 우선순위이며 플레이어가 바꿀 수 있다.
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
        /// <summary>
        /// 아직 손대지 않은(0%) 건설 주문 수(Q-77). 전투 시작 때 이 주문들은 사라지고 전액 환불된다.
        /// 수리 주문은 0%여도 대상이 남으므로 세지 않는다.
        /// </summary>
        public int NotStartedBuildCount
        {
            get
            {
                int n = 0;
                foreach (WorkOrder order in orders)
                    if (order.Kind == WorkKind.Build && !order.IsComplete && order.Done <= PrepClock.Epsilon) n++;
                return n;
            }
        }

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

        // ── 장인 자리와 우선순위(PREP-11) ─────────────────────────────────────

        /// <summary>
        /// 이 주문이 장인 자리를 맡고 있는가. 끝나지 않은 주문 중 앞에서 <see cref="Crews"/>개가 자리를 맡고 나머지는 대기다.
        /// 끝난 주문이나 목록에 없는 주문은 false.
        /// </summary>
        public bool IsAssigned(WorkOrder order)
        {
            if (order == null || order.IsComplete) return false;
            int rank = 0;
            foreach (WorkOrder entry in orders)
            {
                if (entry.IsComplete) continue;
                if (entry == order) return rank < Crews;
                rank++;
            }
            return false;
        }

        /// <summary>장인 자리를 맡은 주문 수.</summary>
        public int AssignedCount => Math.Min(Crews, PendingCount);
        /// <summary>자리가 없어 기다리는 주문 수.</summary>
        public int WaitingCount => Math.Max(0, PendingCount - Crews);

        /// <summary>끝나지 않은 바로 앞 주문과 자리를 바꾼다. 맨 앞이거나 끝난 주문이면 false.</summary>
        public bool MoveUp(WorkOrder order) => Swap(order, -1);

        /// <summary>끝나지 않은 바로 뒤 주문과 자리를 바꾼다. 맨 뒤이거나 끝난 주문이면 false.</summary>
        public bool MoveDown(WorkOrder order) => Swap(order, +1);

        /// <summary>주문을 맨 앞으로 옮긴다. 이미 맨 앞(끝난 주문 제외)이거나 끝난 주문이면 false.</summary>
        public bool MoveToFront(WorkOrder order)
        {
            int index = orders.IndexOf(order);
            if (index < 0 || order.IsComplete) return false;
            int first = orders.FindIndex(entry => !entry.IsComplete);
            if (first == index) return false;
            orders.RemoveAt(index);
            orders.Insert(first, order);
            return true;
        }

        bool Swap(WorkOrder order, int direction)
        {
            int index = orders.IndexOf(order);
            if (index < 0 || order.IsComplete) return false;
            for (int other = index + direction; other >= 0 && other < orders.Count; other += direction)
            {
                if (orders[other].IsComplete) continue;
                orders[index] = orders[other];
                orders[other] = order;
                return true;
            }
            return false;
        }

        // ── 진행 ─────────────────────────────────────────────

        /// <summary>모든 장인이 건설·수리를 하며 시간을 흘린다(기다리기·승급).</summary>
        public void Advance(float hours) => Advance(hours, Crews);

        /// <summary>
        /// 시간을 흘려 주문을 진행한다. 매 순간 끝나지 않은 앞쪽 주문 min(<see cref="Crews"/>, <paramref name="availableArtisans"/>)개가
        /// 같은 속도로 진행되고, 하나가 끝나면 그 장인은 다음 주문을 맡는다. 대장간·시설 강화로 장인이 빠지면 자리를 맡은 주문 중
        /// 뒤쪽이 이번 걸음에 쉰다(PREP-11). 진행할 주문이나 장인이 없으면 남은 시간은 버린다.
        /// </summary>
        public void Advance(float hours, int availableArtisans)
        {
            int workers = Math.Min(Crews, Math.Max(0, availableArtisans));
            if (workers == 0) return;
            float left = Math.Max(0f, hours);
            var touched = new List<WorkOrder>();
            while (left > PrepClock.Epsilon)
            {
                active.Clear();
                foreach (WorkOrder order in orders)
                {
                    if (order.IsComplete) continue;
                    active.Add(order);
                    if (active.Count >= workers) break;
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
