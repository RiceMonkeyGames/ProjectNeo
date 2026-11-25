using System.Collections.Generic;
using ProjectNeo.Combat;
using ProjectNeo.Run;
using ProjectNeo.Walls;
using TMPro;
using UnityEngine;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// Game 씬 준비 단계의 건설·수리 작업 주문(Q-53, SIEGE-08). 놓은 조각·무기와 수리 주문을 <see cref="WorkQueue"/>에 넣고,
    /// 준비 시계가 갈 때마다 진행하며, 전투 시작 때 남은 주문을 정리한다(0%는 없애고 환불, 나머지는 진행률만큼 체력).
    /// 준비 시간 설정이 없으면 아무 주문도 만들지 않아 예전처럼 바로 완성된다. <see cref="GameBuildEconomy"/>가 붙인다.
    /// </summary>
    public sealed class GameConstructionWork : MonoBehaviour
    {
        static readonly Color WorkTint = new Color(1f, 0.82f, 0.45f, 0.45f);
        readonly Dictionary<(WorkTarget, int), Component> targets = new Dictionary<(WorkTarget, int), Component>();
        readonly WorkQueue queue = new WorkQueue();
        ConstructionWorld world;
        PlacementController placement;
        GameBattleController battle;
        GameBuildEconomy build;
        TMP_FontAsset font;
        RunState subscribed;

        public WorkQueue Queue => queue;
        RunState State => battle != null ? battle.RunLink.State : null;
        PrepTimeSettings Time => State?.PrepTime;
        /// <summary>준비 시간이 켜져 있어 작업 주문을 쓴다.</summary>
        public bool IsActive => Time != null;
        /// <summary>장인 자리 수(PREP-11). 런의 장인 수이며 최소 1이다.</summary>
        int Artisans => State != null && State.EffectiveArtisanCount > 0 ? State.EffectiveArtisanCount : 1;

        public void Configure(ConstructionWorld castle, PlacementController controller, GameBattleController owner,
            GameBuildEconomy economy, TMP_FontAsset labelFont)
        {
            world = castle; placement = controller; battle = owner; build = economy; font = labelFont;
            queue.Completed -= OnCompleted; queue.Completed += OnCompleted;
            queue.Progressed -= OnProgressed; queue.Progressed += OnProgressed;
        }

        void Update() => Subscribe();

        // 런 연결과 시계는 씬 초기화 순서와 상관없이 처음 쓸 때 잇는다.
        void Subscribe()
        {
            if (battle == null) return;
            GameRunLink link = battle.RunLink;
            if (link.PieceWork == null)
            {
                link.PieceWork = PieceWorkOf;
                link.WeaponWork = WeaponWorkOf;
                link.WeaponUnfinished = mount => WeaponFor(mount) is PlacedSiegeWeapon weapon && weapon.IsUnfinished;
            }
            RunState state = State;
            if (ReferenceEquals(state, subscribed)) return;
            if (subscribed != null) subscribed.Clock.Stepped -= OnClockStepped;
            subscribed = state;
            if (subscribed != null) subscribed.Clock.Stepped += OnClockStepped;
        }

        void OnDestroy()
        {
            if (subscribed != null) subscribed.Clock.Stepped -= OnClockStepped;
            queue.Completed -= OnCompleted;
            queue.Progressed -= OnProgressed;
        }

        // ── 주문 넣기 ─────────────────────────────────────────────

        /// <summary>새로 놓은 조각을 짓는 주문. 준비 시간이 꺼져 있으면 주문 없이 바로 완성이다.</summary>
        public bool OrderPiece(PlacedStructure body)
        {
            if (!IsActive || body == null || body.Placement == null) return false;
            Subscribe();
            body.SetUnderConstruction(true);
            return Add(new WorkOrder(WorkKind.Build, WorkTarget.Piece, body.Placement.Id, Time.PieceWorkHours), body);
        }

        public bool OrderWeapon(PlacedSiegeWeapon weapon)
        {
            if (!IsActive || weapon == null || weapon.Mount == null) return false;
            Subscribe();
            weapon.SetUnfinished(true);
            return Add(new WorkOrder(WorkKind.Build, WorkTarget.Weapon, weapon.Mount.Id, Time.WeaponWorkHours), weapon);
        }

        /// <summary>수리 주문. 골드는 부르는 쪽이 이미 냈다. 이미 주문이 걸려 있으면 false.</summary>
        public bool OrderRepair(Component target, int gold)
        {
            if (!IsActive || !TryTarget(target, out WorkTarget kind, out int key, out Health health)) return false;
            if (queue.Find(kind, key) != null || health.Maximum <= 0f) return false;
            Subscribe();
            float start = health.IsDead ? 0f : health.Current;
            float hours = Time.RepairHours((health.Maximum - start) / health.Maximum);
            // 승천의 수리 시간 증가(FLOW-03).
            if (State != null) hours = State.Ascension.RepairHours(hours);
            return Add(new WorkOrder(WorkKind.Repair, kind, key, hours, gold, start), target);
        }

        /// <summary>대상에 걸린 주문이 있는지.</summary>
        public bool HasOrder(Component target) =>
            TryTarget(target, out WorkTarget kind, out int key, out _) && queue.Find(kind, key) != null;

        public WorkOrder OrderOf(Component target) =>
            TryTarget(target, out WorkTarget kind, out int key, out _) ? queue.Find(kind, key) : null;

        bool Add(WorkOrder order, Component target)
        {
            queue.SetCrews(Artisans);
            queue.Add(order);
            targets[(order.Target, order.Key)] = target;
            if (order.Kind == WorkKind.Build) ShowView(target, 0f);
            if (order.IsComplete) { OnCompleted(order); queue.RemoveCompleted(); }
            return true;
        }

        /// <summary>
        /// 철거 등으로 대상이 없어질 때 부른다. 주문을 지우고, 수리 주문이었으면 다 못 한 만큼의 골드를 돌려준다.
        /// 돌려준 골드를 알린다(건설 주문은 철거 환불이 따로 전액을 준다).
        /// </summary>
        public int Cancel(Component target)
        {
            if (!TryTarget(target, out WorkTarget kind, out int key, out _)) return 0;
            WorkOrder order = queue.Remove(kind, key);
            targets.Remove((kind, key));
            HideView(target);
            if (order == null || order.Kind != WorkKind.Repair) return 0;
            int refund = order.UnfinishedRefund;
            if (refund > 0) State?.Wallet.AddGold(refund);
            return refund;
        }

        // ── 진행 ─────────────────────────────────────────────

        void OnClockStepped(PrepClockStep step)
        {
            if (!IsActive || battle == null || GameSession.Flow.Phase != GamePhase.Preparation) return;
            queue.SetCrews(Artisans);
            // 대장간·시설 강화를 하는 동안은 그 장인이 빠진다(PREP-11).
            queue.Advance(step.Hours, step.FreeArtisans(Artisans));
            queue.RemoveCompleted();
        }

        /// <summary>
        /// 시험용(<see cref="GameDevKeys"/>): 준비 시계를 쓰지 않고 남은 주문을 모두 끝낸다. 끝낸 주문 수를 알린다.
        /// </summary>
        public int FinishAllForTesting()
        {
            int pending = queue.PendingCount;
            if (pending == 0) return 0;
            queue.SetCrews(Artisans);
            queue.Advance(queue.HoursToFinishAll() + 0.01f);
            queue.RemoveCompleted();
            return pending;
        }

        void OnProgressed(WorkOrder order)
        {
            if (order.Kind == WorkKind.Build && targets.TryGetValue((order.Target, order.Key), out Component target))
                ShowView(target, order.Progress);
        }

        void OnCompleted(WorkOrder order)
        {
            if (!targets.TryGetValue((order.Target, order.Key), out Component target) || target == null) return;
            targets.Remove((order.Target, order.Key));
            if (order.Kind == WorkKind.Build)
            {
                if (target is PlacedStructure piece) piece.SetUnderConstruction(false);
                if (target is PlacedSiegeWeapon weapon) weapon.SetUnfinished(false);
                HideView(target);
                return;
            }
            // 수리 완료: 가득 채운다. 부서진 성문은 이때 닫힌다(사용자 결정 2026-11-18).
            ApplyHealth(target, order, 1f);
            if (target is PlacedSiegeWeapon repaired && repaired.IsUnfinished) { repaired.SetUnfinished(false); HideView(target); }
        }

        // ── 전투 시작 ─────────────────────────────────────────────

        /// <summary>
        /// 전투 시작 때 남은 주문을 정리한다(Q-53, SIEGE-08). 0% 건설 주문은 없애고 전액 환불, 진행된 건설은 진행률만큼의 체력으로
        /// 완성품처럼 서고(무기는 미완성으로 남아 쏘지 않는다), 수리는 도달한 체력을 남기고 못 한 만큼 환불한다. 목록은 비운다.
        /// </summary>
        public void FinishForBattle()
        {
            var orders = new List<WorkOrder>(queue.Orders);
            queue.Clear();
            BuildEconomy economy = build != null ? build.Economy : null;
            foreach (WorkOrder order in orders)
            {
                if (!targets.TryGetValue((order.Target, order.Key), out Component target) || target == null) continue;
                if (order.Kind == WorkKind.Build) FinishBuild(order, target, economy);
                else
                {
                    if (order.Progress > PrepClock.Epsilon) ApplyHealth(target, order, order.Progress);
                    int refund = order.UnfinishedRefund;
                    if (refund > 0) State?.Wallet.AddGold(refund);
                    if (target is PlacedSiegeWeapon weapon && weapon.IsUnfinished && order.IsComplete) weapon.SetUnfinished(false);
                }
            }
            targets.Clear();
            // 다 짓지 못한 무기는 전투 중에도 반투명·진행률로 남겨 쏘지 않음을 알린다.
            RefreshUnfinishedViews();
        }

        void FinishBuild(WorkOrder order, Component target, BuildEconomy economy)
        {
            float progress = order.Progress;
            if (target is PlacedStructure piece)
            {
                if (progress <= PrepClock.Epsilon)
                {
                    BuildPieceDefinition definition = piece.Placement.Definition;
                    int id = piece.Placement.Id;
                    HideView(piece);
                    if (world.RemoveUnbuilt(piece)) economy?.RefundPiece(definition, id);
                    return;
                }
                piece.Health.TakeDamage(piece.Health.Maximum * (1f - progress));
                piece.SetUnderConstruction(false);
                HideView(piece);
                return;
            }
            if (target is PlacedSiegeWeapon weapon)
            {
                if (progress <= PrepClock.Epsilon)
                {
                    SiegeWeaponDefinition definition = weapon.Definition;
                    int mountId = weapon.Mount.Id;
                    HideView(weapon);
                    if (world.RemoveWeapon(weapon)) economy?.RefundWeapon(definition, mountId);
                    return;
                }
                weapon.ReceiveDamage(weapon.Health.Maximum * (1f - progress));
                weapon.SetUnfinished(true);
            }
        }

        static void ApplyHealth(Component target, WorkOrder order, float progress)
        {
            Health health = target is PlacedStructure piece ? piece.Health : target is PlacedSiegeWeapon weapon ? weapon.Health : null;
            if (health == null) return;
            float goal = order.StartHealth + (health.Maximum - order.StartHealth) * Mathf.Clamp01(progress);
            if (goal <= 0f) return;
            if (health.IsDead)
            {
                health.ReviveFull();
                if (goal < health.Maximum) health.TakeDamage(health.Maximum - goal);
            }
            else if (goal > health.Current) health.Heal(goal - health.Current);
        }

        // ── 준비 시작·되살리기 ─────────────────────────────────────────────

        /// <summary>준비 시작 때 부른다. 되살린 런이면 저장된 주문을 다시 넣는다. 미완성 무기 표시를 맞춘다.</summary>
        public void OnPreparation(IReadOnlyList<RestoredWork> restored)
        {
            Subscribe();
            if (restored != null && restored.Count > 0 && queue.Orders.Count == 0 && IsActive)
            {
                queue.SetCrews(Artisans);
                foreach (RestoredWork entry in restored)
                {
                    Component target = entry.Piece != null ? entry.Piece : (Component)entry.Weapon;
                    if (target == null || entry.Order == null || queue.Find(entry.Order.Target, entry.Order.Key) != null) continue;
                    queue.Add(entry.Order);
                    targets[(entry.Order.Target, entry.Order.Key)] = target;
                    if (entry.Order.Kind == WorkKind.Build) ShowView(target, entry.Order.Progress);
                }
            }
            RefreshUnfinishedViews();
        }

        void RefreshUnfinishedViews()
        {
            if (world == null) return;
            foreach (PlacedSiegeWeapon weapon in world.Weapons)
                if (weapon != null && weapon.IsUnfinished && !HasBuildOrder(weapon))
                    ShowView(weapon, weapon.Health.Maximum > 0f ? weapon.Health.Current / weapon.Health.Maximum : 0f);
        }

        bool HasBuildOrder(Component target)
        {
            WorkOrder order = OrderOf(target);
            return order != null && order.Kind == WorkKind.Build;
        }

        // ── 저장 ─────────────────────────────────────────────

        CastleWorkState PieceWorkOf(BuildPlacement placement) => Record(WorkTarget.Piece, placement.Id);
        CastleWorkState WeaponWorkOf(MountedWeapon mount) => Record(WorkTarget.Weapon, mount.Id);

        CastleWorkState Record(WorkTarget target, int key)
        {
            for (int i = 0; i < queue.Orders.Count; i++)
            {
                WorkOrder order = queue.Orders[i];
                if (order.Target == target && order.Key == key && !order.IsComplete) return CastleWorkState.From(order, i);
            }
            return default;
        }

        PlacedSiegeWeapon WeaponFor(MountedWeapon mount)
        {
            foreach (PlacedSiegeWeapon weapon in world.Weapons)
                if (weapon != null && weapon.Mount == mount) return weapon;
            return null;
        }

        // ── 표시 ─────────────────────────────────────────────

        /// <summary>작업 목록 한 줄 요약. 주문이 없으면 비어 있다.</summary>
        public string Summary()
        {
            int pending = queue.PendingCount;
            if (!IsActive || pending == 0) return string.Empty;
            return $"작업 주문 {pending}개 · 장인 {queue.Crews}명 · 완료까지 {queue.HoursToFinishAll():0.#}시간";
        }

        // ── 작업 탭(PREP-11) ─────────────────────────────────────────────

        /// <summary>이 작업 주문을 쓰는 런(장인 수·고용). 없으면 null.</summary>
        public RunState Run => State;
        bool Preparing => IsActive && battle != null && GameSession.Flow != null && GameSession.Flow.Phase == GamePhase.Preparation;

        /// <summary>작업 탭이 보여 줄 끝나지 않은 주문을 순서대로 채운다(우선순위 순).</summary>
        public void PendingOrders(List<WorkOrder> into)
        {
            into.Clear();
            Subscribe();
            if (IsActive) queue.SetCrews(Artisans);
            foreach (WorkOrder order in queue.Orders) if (!order.IsComplete) into.Add(order);
        }

        /// <summary>장인 자리를 맡은 주문인가(아니면 대기).</summary>
        public bool IsAssigned(WorkOrder order) => queue.IsAssigned(order);

        /// <summary>작업 카드 이름: 조각·무기 이름, 수리면 앞에 "수리".</summary>
        public string NameOf(WorkOrder order)
        {
            if (order == null) return string.Empty;
            targets.TryGetValue((order.Target, order.Key), out Component target);
            string name = target is PlacedStructure piece && piece != null ? piece.Placement.Definition.DisplayName
                : target is PlacedSiegeWeapon weapon && weapon != null ? weapon.Definition.DisplayName
                : order.Target == WorkTarget.Weapon ? "수성 무기" : "조각";
            return order.Kind == WorkKind.Repair ? "수리 " + name : name;
        }

        /// <summary>주문을 한 칸 앞(earlier)이나 뒤로 옮긴다. 준비 중이 아니거나 더 갈 곳이 없으면 false.</summary>
        public bool Move(WorkOrder order, bool earlier)
        {
            if (!Preparing || order == null) return false;
            return earlier ? queue.MoveUp(order) : queue.MoveDown(order);
        }

        /// <summary>
        /// 주문을 취소한다. 건설은 철거와 같다(이번 준비에 지은 것이라 전액 환불, 위에 무기가 있으면 무기부터).
        /// 수리는 못 한 만큼의 골드를 돌려준다. 결과는 건설 막대 안내에 보인다.
        /// </summary>
        public bool CancelOrder(WorkOrder order)
        {
            if (!Preparing || order == null || queue.Find(order.Target, order.Key) != order) return false;
            targets.TryGetValue((order.Target, order.Key), out Component target);
            if (target == null) { queue.Remove(order.Target, order.Key); return true; }
            if (order.Kind == WorkKind.Build)
            {
                PlacedStructure support = target as PlacedStructure ?? (target as PlacedSiegeWeapon)?.Support;
                return build != null && support != null && build.Dismantle(support);
            }
            string name = NameOf(order);
            int refund = Cancel(target);
            build?.Notify($"{name} 취소 - {refund}골드 환불");
            return true;
        }

        /// <summary>장인을 한 명 고용한다(시간 없는 행동). 결과는 건설 막대 안내에 보인다.</summary>
        public bool HireArtisan()
        {
            RunState state = State;
            if (!Preparing || state == null) return false;
            int gold = state.NextArtisanHireGold;
            ArtisanHireCheck check = state.CheckHireArtisan();
            bool hired = check == ArtisanHireCheck.Ok && state.TryHireArtisan();
            if (hired) queue.SetCrews(Artisans);
            build?.Notify(hired ? $"장인 고용 - {gold}골드 · 장인 {state.ArtisanCount}명" : HireRefusal(check, state));
            return hired;
        }

        /// <summary>고용할 수 없는 이유 한 줄.</summary>
        public static string HireRefusal(ArtisanHireCheck check, RunState state) => check switch
        {
            ArtisanHireCheck.AtCap when state != null && state.PrepTime != null && state.ArtisanCap < state.PrepTime.ArtisanCap(int.MaxValue)
                => "장인이 가득 찼습니다. 대장간을 강화하면 한 명 더 고용할 수 있습니다.",
            ArtisanHireCheck.AtCap => "장인을 더 고용할 수 없습니다.",
            ArtisanHireCheck.NotEnoughFunds => $"골드가 부족합니다: 장인 고용 {state?.NextArtisanHireGold ?? 0}골드",
            ArtisanHireCheck.BattlePending => "전투 준비 중에는 고용할 수 없습니다.",
            _ => "장인을 고용할 수 없습니다.",
        };

        /// <summary>
        /// 전투 시작 전 미완료 작업 경고 한 줄(Q-77). 끝나지 않은 주문이 없으면 비어 있다.
        /// 0% 건설 주문은 전투 시작 때 사라지고 환불된다는 것을, 진행된 작업은 지금 진행률로 전투에 들어간다는 것을 알린다.
        /// </summary>
        public string BattleStartWarning()
        {
            int pending = queue.PendingCount;
            if (!IsActive || pending == 0) return string.Empty;
            int notStarted = queue.NotStartedBuildCount;
            string detail = notStarted > 0
                ? $"시작 안 한 {notStarted}개는 전투가 시작되면 없어지고 환불됩니다"
                : "지금까지 진행한 만큼만 전투에 들어갑니다";
            return $"<color=#FFC857>! 끝나지 않은 작업 {pending}개 - {detail}</color>";
        }

        void ShowView(Component target, float progress)
        {
            if (target == null) return;
            var view = target.GetComponent<UnderConstructionView>();
            if (view == null) view = target.gameObject.AddComponent<UnderConstructionView>();
            float height = target is PlacedStructure piece ? piece.Placement.Definition.Height : 1.5f;
            view.Begin(placement != null && placement.ViewSettings != null ? placement.ViewSettings.GhostMaterial : null,
                WorkTint, font, height);
            view.SetProgress(progress);
        }

        static void HideView(Component target)
        {
            if (target == null) return;
            var view = target.GetComponent<UnderConstructionView>();
            if (view != null) view.End();
        }

        static bool TryTarget(Component target, out WorkTarget kind, out int key, out Health health)
        {
            kind = WorkTarget.Piece; key = 0; health = null;
            if (target is PlacedStructure piece && piece != null && piece.Placement != null)
            { kind = WorkTarget.Piece; key = piece.Placement.Id; health = piece.Health; return health != null; }
            if (target is PlacedSiegeWeapon weapon && weapon != null && weapon.Mount != null && !weapon.IsRemoved)
            { kind = WorkTarget.Weapon; key = weapon.Mount.Id; health = weapon.Health; return health != null; }
            return false;
        }
    }
}
