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
            if (subscribed != null) subscribed.Clock.Advanced -= OnClockAdvanced;
            subscribed = state;
            if (subscribed != null) subscribed.Clock.Advanced += OnClockAdvanced;
        }

        void OnDestroy()
        {
            if (subscribed != null) subscribed.Clock.Advanced -= OnClockAdvanced;
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
            return Add(new WorkOrder(WorkKind.Repair, kind, key, hours, gold, start), target);
        }

        /// <summary>대상에 걸린 주문이 있는지.</summary>
        public bool HasOrder(Component target) =>
            TryTarget(target, out WorkTarget kind, out int key, out _) && queue.Find(kind, key) != null;

        public WorkOrder OrderOf(Component target) =>
            TryTarget(target, out WorkTarget kind, out int key, out _) ? queue.Find(kind, key) : null;

        bool Add(WorkOrder order, Component target)
        {
            queue.SetCrews(Time.WorkCrews);
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

        void OnClockAdvanced(float hours)
        {
            if (!IsActive || battle == null || GameSession.Flow.Phase != GamePhase.Preparation) return;
            queue.SetCrews(Time.WorkCrews);
            queue.Advance(hours);
            queue.RemoveCompleted();
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
                queue.SetCrews(Time.WorkCrews);
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
            return $"Work orders {pending} · {queue.Crews} crews · {queue.HoursToFinishAll():0.#}h to finish";
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
