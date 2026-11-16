using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ProjectNeo.Siege
{
    /// <summary>아군 경로와 플레이어 입력을 공용 사다리 대기열에 연결한다.</summary>
    public sealed class LadderTraveller : MonoBehaviour, IInterruptibleAction
    {
        CharacterActor actor;
        NavMeshAgent agent;
        CharacterController motor;
        PlayerInputController player;
        UnitCombatAI ai;
        SiegeLadder ladder, interaction;
        float progress, held, retry, pushInterruptCooldown, interruptNoticeTimer;
        bool ascending, queuedPlayer, deferred, deferredPoint, agentWasEnabled, motorWasEnabled, tapBoardEligible;
        UnitDirective nextDirective;
        Vector3 nextPoint;
        NavMeshPath path;
        public bool IsTraversing { get; private set; }
        public string PlayerHint { get; private set; } = "";
        public bool HasInterruptNotice => interruptNoticeTimer > 0f;
        public ActionPoise InterruptPoise => interaction != null
            && interaction.State.Phase == LadderPhase.Up
            && interaction.IsNear(actor, true)
            && IsHoldingTopple(interaction)
            && pushInterruptCooldown <= 0f
            ? ActionPoise.Interruptible(interaction.Settings.LadderPushPoise)
            : ActionPoise.Uninterruptible;

        void Awake()
        {
            actor = GetComponent<CharacterActor>(); agent = GetComponent<NavMeshAgent>();
            motor = GetComponent<CharacterController>(); player = GetComponent<PlayerInputController>();
            ai = GetComponent<UnitCombatAI>();
            path = new NavMeshPath();
        }
        void OnEnable()
        {
            if (actor == null) return;
            actor.Died += OnDied;
            actor.RegisterInterruptible(this);
        }
        void OnDisable()
        {
            if (actor != null)
            {
                actor.Died -= OnDied;
                actor.UnregisterInterruptible(this);
            }
            if (IsTraversing) Drop();
            CancelWaiting();
        }
        void OnDied() { if (IsTraversing) Drop(); else CancelWaiting(); }
        public bool IsHoldingTopple(SiegeLadder target) => pushInterruptCooldown <= 0f && interaction == target && held >= target.Settings.LadderTapSeconds;

        public void InterruptAction()
        {
            if (interaction == null || interaction.State.Phase != LadderPhase.Up || !IsHoldingTopple(interaction)) return;
            pushInterruptCooldown = interaction.Settings != null ? interaction.Settings.LadderPushInterruptCooldown : 0.3f;
            held = 0f;
            tapBoardEligible = false;
            interruptNoticeTimer = 1.5f;
        }

        public bool Defer(UnitDirective directive, bool point, Vector3 destination)
        {
            if (!IsTraversing) { CancelWaiting(); retry = 0f; return false; }
            deferred = true; nextDirective = directive; deferredPoint = point; nextPoint = destination;
            return true;
        }
        public void CancelWaiting()
        {
            if (IsTraversing) return;
            if (ladder != null) { ladder.Leave(actor); ladder.StopRaising(actor); }
            ladder = null; queuedPlayer = false;
        }
        public void Interrupt()
        {
            if (IsTraversing) Drop();
            CancelWaiting();
            if (interaction != null) interaction.StopRaising(actor);
            interaction = null; held = 0f; pushInterruptCooldown = 0f; interruptNoticeTimer = 0f; PlayerHint = "";
        }
        public static bool HandleAI(UnitCombatAI unit)
        {
            if (unit.Actor.Team != CombatTeam.Allied) return false;
            var traveller = unit.GetComponent<LadderTraveller>();
            if (traveller == null && (SiegeLadder.Active.Count == 0
                || (unit.Directive != UnitDirective.Move && !(unit.Directive == UnitDirective.Engage && unit.HasAdvancePoint)))) return false;
            if (traveller == null) traveller = unit.gameObject.AddComponent<LadderTraveller>();
            return traveller.TickAI();
        }
        bool TickAI()
        {
            if (IsTraversing) { MoveOnLadder(ascending ? 1f : -1f); return true; }
            if (actor.Health == null || actor.Health.IsDead || agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
            if (ai.Directive != UnitDirective.Move && !(ai.Directive == UnitDirective.Engage && ai.HasAdvancePoint))
            { CancelWaiting(); return false; }
            if (ladder == null)
            {
                retry -= Time.deltaTime;
                if (retry > 0f) return false;
                retry = ai.DecisionInterval;
                if (agent.CalculatePath(ai.MoveDestination, path) && path.status == NavMeshPathStatus.PathComplete) return false;
                ladder = FindRoute(ai.MoveDestination, out ascending);
                if (ladder == null) return false;
            }
            if (!ladder.isActiveAndEnabled || !ladder.HasWall) { CancelWaiting(); return false; }
            if (ladder.World.IsRebuilding || ladder.World.RebuildPending) return true;
            Vector3 entry = ascending ? ladder.BasePoint : ladder.TopPoint;
            agent.speed = actor.Stats.GetValue(ProjectNeo.Stats.StatType.MoveSpeed);
            agent.isStopped = false;
            if (!ladder.IsNear(actor, !ascending)) { agent.SetDestination(entry); return true; }
            agent.ResetPath(); agent.isStopped = true;
            if (ladder.TryEnter(actor)) Begin(ascending);
            else if (ascending && ladder.State.Phase != LadderPhase.Up) ladder.TryRaise(actor);
            return true;
        }
        SiegeLadder FindRoute(Vector3 destination, out bool up)
        {
            SiegeLadder best = null; up = true; float score = float.PositiveInfinity;
            foreach (var candidate in SiegeLadder.Active)
            {
                if (candidate.gameObject.scene != gameObject.scene || !candidate.HasWall
                    || candidate.World.IsRebuilding || candidate.World.RebuildPending) continue;
                bool ascent = Mathf.Abs(transform.position.y - candidate.BasePoint.y)
                    < Mathf.Abs(transform.position.y - candidate.TopPoint.y);
                Vector3 entry = ascent ? candidate.BasePoint : candidate.TopPoint;
                Vector3 exit = ascent ? candidate.TopPoint : candidate.BasePoint;
                var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                if (!agent.CalculatePath(entry, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                if (!NavMesh.CalculatePath(exit, destination, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                float distance = Vector3.Distance(transform.position, entry) + Vector3.Distance(exit, destination)
                    + candidate.Waiting.Count * candidate.Settings.LadderCrowdPenalty;
                if (distance >= score) continue;
                score = distance; best = candidate; up = ascent;
            }
            return best;
        }
        public static bool HandlePlayer(PlayerInputController input, bool captured, float vertical)
        {
            var traveller = input.GetComponent<LadderTraveller>();
            if (traveller == null) traveller = input.gameObject.AddComponent<LadderTraveller>();
            return traveller.TickPlayer(captured, vertical);
        }
        bool TickPlayer(bool captured, float vertical)
        {
            PlayerHint = "";
            if (pushInterruptCooldown > 0f) pushInterruptCooldown = Mathf.Max(0f, pushInterruptCooldown - Time.deltaTime);
            if (interruptNoticeTimer > 0f) interruptNoticeTimer = Mathf.Max(0f, interruptNoticeTimer - Time.deltaTime);
            if (IsTraversing)
            {
                PlayerHint = "W/S · 사다리 오르내리기";
                MoveOnLadder(captured ? vertical : 0f); return true;
            }
            var falling = GetComponent<UnitFallController>();
            if (!captured || actor.Health == null || actor.Health.IsDead || (falling != null && falling.IsFalling))
            { held = 0f; interaction = null; CancelWaiting(); return false; }
            SiegeLadder nearest = null; float distance = float.PositiveInfinity;
            foreach (var candidate in SiegeLadder.Active)
            {
                if (candidate.gameObject.scene != gameObject.scene || !candidate.HasWall) continue;
                if (!candidate.IsNear(actor, false) && !candidate.IsNear(actor, true)) continue;
                float d = Mathf.Min((transform.position - candidate.BasePoint).sqrMagnitude,
                    (transform.position - candidate.TopPoint).sqrMagnitude);
                if (d >= distance) continue;
                nearest = candidate; distance = d;
            }
            if (nearest != interaction)
            {
                if (interaction != null) interaction.StopRaising(actor);
                held = 0f; CancelWaiting(); interaction = nearest;
            }
            if (nearest == null) return false;
            bool upper = nearest.IsNear(actor, true);
            bool pressed = Keyboard.current != null && Keyboard.current.fKey.isPressed;
            if (pressed)
            {
                if (upper && pushInterruptCooldown > 0f)
                {
                    held = 0f;
                    tapBoardEligible = false;
                }
                else
                {
                    // 자동 안착 중 시작한 입력은 완료 직후 놓아도 탑승 입력으로 바뀌지 않는다.
                    if (held == 0f) tapBoardEligible = nearest.State.CanBoard;
                    held += Time.deltaTime;
                    if (!upper && nearest.State.Phase != LadderPhase.Up) nearest.TryRaise(actor);
                }
            }
            else
            {
                nearest.StopRaising(actor);
                if (tapBoardEligible && held > 0f && held < nearest.Settings.LadderTapSeconds && nearest.State.CanBoard)
                { queuedPlayer = true; ladder = nearest; ascending = !upper; }
                held = 0f;
            }
            PlayerHint = nearest.State.Phase == LadderPhase.Up
                ? "F 짧게 · 탑승 / 위에서 F 유지 · 사다리 밀기"
                : upper ? "사다리가 내려가 있습니다" : "F 유지 · 사다리 세우기";
            if (nearest.State.IsSettling) PlayerHint = "사다리가 움직이는 중 · 잠시 기다리세요";
            if (queuedPlayer)
            {
                PlayerHint = "사다리 이용 대기 중";
                if (ladder.TryEnter(actor)) { queuedPlayer = false; Begin(ascending); return true; }
            }
            return false;
        }
        void Begin(bool up)
        {
            ascending = up; progress = up ? 0f : 1f;
            agentWasEnabled = agent != null && agent.enabled;
            motorWasEnabled = motor != null && motor.enabled;
            if (agentWasEnabled) agent.enabled = false;
            if (motorWasEnabled) motor.enabled = false;
            actor.SetOnLadder(true); IsTraversing = true;
            player?.SetMovementSuspended(true);
            transform.position = Position(progress);
        }
        Vector3 Position(float t) => ladder.FollowTilt(t < 0.85f
            ? Vector3.Lerp(ladder.BasePoint, ladder.EdgePoint, t / 0.85f)
            : Vector3.Lerp(ladder.EdgePoint, ladder.TopPoint, (t - 0.85f) / 0.15f));
        public void FollowLadderTilt()
        {
            if (IsTraversing && ladder != null) transform.position = Position(progress);
        }
        void MoveOnLadder(float direction)
        {
            if (ladder == null || !ladder.isActiveAndEnabled || !ladder.HasWall || ladder.State.Phase != LadderPhase.Up)
            { Drop(); return; }
            if (ladder.State.PushProgress > 0f) { FollowLadderTilt(); return; }
            progress = Mathf.Clamp01(progress + Mathf.Clamp(direction, -1f, 1f) * Time.deltaTime / ladder.Settings.LadderClimbSeconds);
            transform.position = Position(progress);
            if ((direction > 0f && progress >= 1f) || (direction < 0f && progress <= 0f)) Finish();
        }
        void Finish()
        {
            Vector3 endpoint = progress >= 1f ? ladder.TopPoint : ladder.BasePoint;
            if (agentWasEnabled)
            {
                if (ladder.World.IsRebuilding || ladder.World.RebuildPending) return;
                var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                if (!NavMesh.SamplePosition(endpoint, out var hit, ladder.Settings.LadderHeightTolerance, filter)
                    || Mathf.Abs(hit.position.y - endpoint.y) > ladder.Settings.LadderHeightTolerance) { Drop(); return; }
                endpoint = hit.position;
            }
            transform.position = endpoint; IsTraversing = false;
            ladder.Leave(actor); ladder = null;
            if (agentWasEnabled) { agent.enabled = true; agent.Warp(endpoint); }
            if (motorWasEnabled) motor.enabled = true;
            player?.SetMovementSuspended(false);
            if (deferred && ai != null)
            {
                deferred = false;
                if (deferredPoint) ai.SetDirective(nextDirective, nextPoint); else ai.SetDirective(nextDirective);
            }
        }
        public void OnToppled() { if (IsTraversing) Drop(); else CancelWaiting(); }
        void Drop()
        {
            if (!IsTraversing) return;
            IsTraversing = false;
            Vector3 fallVelocity = Vector3.zero;
            if (ladder != null)
            {
                if (ladder.TryGetFallMotion(transform.position, out Vector3 separatedPosition, out Vector3 outwardVelocity))
                {
                    transform.position = separatedPosition;
                    fallVelocity = outwardVelocity;
                }
                ladder.Leave(actor);
                ladder = null;
            }
            actor.SetOnLadder(false);
            // 낙하 제어가 플레이어 모터의 복구 여부를 기록하도록 먼저 돌려놓는다.
            if (motorWasEnabled && actor.Health != null && !actor.Health.IsDead) motor.enabled = true;
            var fall = GetComponent<UnitFallController>();
            if (fall != null) fall.BeginFall(agentWasEnabled, fallVelocity);
            else
            {
                if (agentWasEnabled && actor.Health != null && !actor.Health.IsDead) agent.enabled = true;
                player?.SetMovementSuspended(false);
            }
            deferred = false;
        }
    }
}
