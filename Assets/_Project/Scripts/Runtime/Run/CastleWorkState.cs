namespace ProjectNeo.Run
{
    /// <summary>
    /// 성 조각·무기 하나에 걸린 작업 주문의 기록(SIEGE-08). 준비 중 저장하면 주문 순서와 진행을 그대로 되살린다.
    /// 주문은 한 준비 안에서만 있으므로 전투 시작 뒤 기록에는 없다.
    /// </summary>
    public readonly struct CastleWorkState
    {
        public WorkKind Kind { get; }
        /// <summary>작업 목록 안의 순서(작을수록 먼저).</summary>
        public int Order { get; }
        public float Hours { get; }
        public float Done { get; }
        public int GoldPaid { get; }
        public float StartHealth { get; }
        public bool HasWork => Kind != WorkKind.None;

        public CastleWorkState(WorkKind kind, int order, float hours, float done, int goldPaid, float startHealth)
        {
            Kind = kind; Order = order; Hours = hours; Done = done; GoldPaid = goldPaid; StartHealth = startHealth;
        }

        public static CastleWorkState From(WorkOrder order, int index) => order == null
            ? default
            : new CastleWorkState(order.Kind, index, order.Hours, order.Done, order.GoldPaid, order.StartHealth);

        public WorkOrder ToOrder(WorkTarget target, int key) =>
            HasWork ? new WorkOrder(Kind, target, key, Hours, GoldPaid, StartHealth, Done) : null;
    }
}
