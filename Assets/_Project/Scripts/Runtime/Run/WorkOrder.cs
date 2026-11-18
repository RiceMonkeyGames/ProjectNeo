using System;

namespace ProjectNeo.Run
{
    /// <summary>작업 주문 종류(SIEGE-08). 저장 기록에 쓰이므로 값을 고정하고 새 값은 끝에 더한다.</summary>
    public enum WorkKind
    {
        None = 0,
        /// <summary>새로 놓은 조각·수성 무기를 짓는다.</summary>
        Build = 1,
        /// <summary>깎인 체력을 되돌린다.</summary>
        Repair = 2,
    }

    /// <summary>무엇에 대한 주문인지. 조각과 수성 무기는 번호 체계가 달라 나눈다.</summary>
    public enum WorkTarget
    {
        Piece = 0,
        Weapon = 1,
    }

    /// <summary>
    /// 작업 주문 하나(Q-53, SIEGE-08). 작업대가 준비 시계가 가는 만큼 <see cref="Done"/>을 채운다. 씬을 모른다.
    /// <see cref="Key"/>는 씬 실행 중의 배치 번호(조각) 또는 무기 번호(무기)다.
    /// </summary>
    public sealed class WorkOrder
    {
        public WorkKind Kind { get; }
        public WorkTarget Target { get; }
        public int Key { get; }
        /// <summary>끝내는 데 드는 작업 시간.</summary>
        public float Hours { get; }
        /// <summary>지금까지 한 작업 시간.</summary>
        public float Done { get; private set; }
        /// <summary>주문 때 낸 골드. 수리를 다 못 하면 남은 비율만큼 돌려준다.</summary>
        public int GoldPaid { get; }
        /// <summary>수리 주문을 낸 때의 체력. 수리 뒤 체력 = 이 값 + (최대 - 이 값) × 진행률.</summary>
        public float StartHealth { get; }

        public float Progress => Hours <= 0f ? 1f : Math.Min(1f, Done / Hours);
        public float Remaining => Math.Max(0f, Hours - Done);
        public bool IsComplete => Remaining <= PrepClock.Epsilon;

        public WorkOrder(WorkKind kind, WorkTarget target, int key, float hours, int goldPaid = 0, float startHealth = 0f, float done = 0f)
        {
            if (kind == WorkKind.None) throw new ArgumentException("작업 종류가 없다.", nameof(kind));
            Kind = kind;
            Target = target;
            Key = key;
            Hours = Math.Max(0f, hours);
            GoldPaid = Math.Max(0, goldPaid);
            StartHealth = Math.Max(0f, startHealth);
            Done = Math.Min(Hours, Math.Max(0f, done));
        }

        /// <summary>작업 시간을 더한다. 끝내는 데 필요한 만큼만 쓰고, 실제로 쓴 시간을 돌려준다.</summary>
        public float Work(float hours)
        {
            float used = Math.Min(Math.Max(0f, hours), Remaining);
            Done += used;
            if (IsComplete) Done = Hours;
            return used;
        }

        /// <summary>남은 작업 비율만큼 돌려줄 골드(내림).</summary>
        public int UnfinishedRefund => (int)Math.Floor(GoldPaid * (1f - Progress) + PrepClock.Epsilon);

        /// <summary>수리 주문의 지금 체력.</summary>
        public float RepairedHealth(float maximum) => StartHealth + (Math.Max(StartHealth, maximum) - StartHealth) * Progress;
    }
}
