---
id: PERF-ai-query-budget
title: AI Decision Tick Staggering, Top-K Path Filtering & LoS Caching
status: Waiting
assignee: Unassigned
branch:
area: PERF
depends_on: []
created: 2026-11-22
---

# PERF-ai-query-budget — AI Decision Tick Staggering, Top-K Path Filtering & LoS Caching

## Scope & Completion Criteria

Phase 1 of the 200 vs 200 large-scale battle AI optimization pass ([Combat](../design/Combat.md), [CombatAndSquads codemap](../codemap/CombatAndSquads.md), baseline [PERF-nav-query](../history/PERF/PERF-nav-query.md)). Eliminates synchronous `NavMesh.CalculatePath` spikes and per-frame physics line-of-sight checks in `UnitCombatAI` and `SiegeAttackerBrain` without altering combat outcome rules.

### 1. Decision Tick Staggering & Path Query Budget (`AIDecisionScheduler`)
- **Pure C# rules (`Combat/AIDecisionScheduler.cs`)**:
  - Deterministic initial tick phase offset: `InitialOffset(int unitIndex, int bucketCount, float interval) => (unitIndex % bucketCount) * (interval / bucketCount)` so waves spawned together or squads receiving simultaneous `SetDirective` orders do not evaluate on the exact same frame.
  - Per-frame global pathfinding token bucket (`TryConsumePathToken(int lodTier)`): limits synchronous `NavMesh.CalculatePath` calls per frame (configurable `MaxPathQueriesPerFrame` in `UnitAISettings`, default `24`, `0` = unlimited for legacy tests), prioritizing LOD 0 / engaged units and deferring excess queries to the next frame (`decisionRemaining = 0f`).
- **Integration**:
  - Apply staggered initial `decisionRemaining` on `UnitCombatAI.OnEnable` and squad-wide `SquadCommandCenter` directive assignment, and stagger `SiegeAttackerBrain.think` on spawn/activation.

### 2. Top-K Candidate Sorting Before `NavMesh.CalculatePath`
- **Problem**: `UnitCombatAI.FindTarget`, `UnitCombatAI.FindTargetNearPost`, and `SiegeAttackerBrain.AnyReachableEnemy` currently call `agent.CalculatePath` inside the full `CharacterActor.ActiveActors` loop whenever a candidate has a smaller straight-line distance/score than the current best. When candidates appear in unfavorable order or paths are blocked by closed walls, one unit performs dozens of `CalculatePath` calls in a single decision tick.
- **Implementation (`Combat/TargetCandidateBuffer.cs`)**:
  - Pure fixed-capacity top-K min-buffer (`TopKCandidates`, default `K = 4` in `UnitAISettings.MaxPathCandidatesPerTick`): first filter all candidates by team, alive state, `DetectionRange` / `HoldChaseRadius`, and `CombatPhysics.CanTarget`, keeping only the best `K` candidates sorted by priority score (`RangedTargetPriorityRules.Score`).
  - Run `CanReach` / `CanReachInHold` (`agent.CalculatePath`) **only** on those sorted top-K candidates in ascending score order and stop at the first reachable candidate.

### 3. Decision-Tick LoS Caching in `UnitCombatAI.UpdateEngage`
- **Problem**: `UnitCombatAI.UpdateEngage` calls `CombatPhysics.HasClearShot(actor, Target)` every frame (2 physics raycasts per unit per frame = 800 raycasts/frame at 200v200) even while ranged cooldown is active or melee units are still walking to range.
- **Implementation**:
  - Cache `hasClearShotToTarget` on `UnitCombatAI`, refreshing it only when `refreshed == true` (each `DecisionInterval`), when `Target` changes, or immediately before `actor.TryAttack(Target)` when `actor.CanAttack(Target)` is ready.
  - Keep `HandleBlockedShot` driven by the cached decision-tick LoS state.

### 4. Verification
- **EditMode tests**: `AIDecisionSchedulerTests` and `TopKCandidatesTests` verifying deterministic tick distribution, budget deferral, and exact match with full-scan target selection when the top candidate is reachable.
- **PlayMode & Benchmark**: Existing `UnitCombatPlayTests` and `EngagementPlayTests` pass; extend `UnitAIQueryBenchmark` to log per-frame `CalculatePath` call counts and p95 frame time at 200 vs 200.
