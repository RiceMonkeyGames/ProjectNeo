---
id: PERF-siege-ai-scale
title: Squad-Level Idle Sleep, Siege Reachability Cache & Archer Search Budget
status: Waiting
assignee: Unassigned
branch:
area: PERF
depends_on: [PERF-ai-query-budget, PERF-ai-spatial-grid]
created: 2026-11-22
---

# PERF-siege-ai-scale — Squad-Level Idle Sleep, Siege Reachability Cache & Archer Search Budget

## Scope & Completion Criteria

Phase 4 of the 200 vs 200 large-scale battle AI optimization pass ([Combat](../design/Combat.md), [WallsAndSiege](../design/WallsAndSiege.md), [CombatAndSquads codemap](../codemap/CombatAndSquads.md), [WallsAndSiege codemap](../codemap/WallsAndSiege.md)). Coordinates with `SIEGE-15` and `SIEGE-17` to remove redundant per-unit path and firing-spot queries across 10 allied squads (210 units) and 200 siege attackers.

### 1. Allied Squad-Level Hold Watch & Idle Sleep (`Squads/SquadThreatWatch.cs`)
- **Problem**: With 10 squads × 21 units in `Hold`, rear or reserve squads far from enemies still run per-member target searches every decision interval.
- **Implementation**:
  - For each squad in `SquadOrder.Hold` where all members have `Target == null` and `returningToPost == false`, compute the squad bounding sphere (`squadCenter`, `squadRadius + maxMemberHoldScanRange`) once per squad tick.
  - Query `CombatSpatialIndex` once for the entire squad sphere. If zero enemies exist within that sphere, mark the squad `IsNeighborhoodClear = true` for `DecisionInterval`: member `UnitCombatAI.UpdateHold` skips `FindTargetInReach`, `FindTargetNearPost`, and `UpdateOpening` completely for that tick.
  - Any `OnHitReceived` on a squad member or new squad order wakes the squad immediately.

### 2. Shared Siege Reachability Cache (`Siege/SiegeReachabilityCache.cs`)
- **Problem**: Before the castle gate or walls are breached, up to 200 ground `SiegeAttackerBrain` units call `AnyReachableEnemy` every `AttackerDecisionInterval`, each running `NavMesh.CalculatePath` against inside-castle defenders only to discover all paths are incomplete.
- **Implementation**:
  - Maintain a battlefield reachability cache keyed by `(attackerElevationBucket, targetSquadIndexOrRegion)` and invalidated whenever `ConstructionWorld.LayoutRevision` changes, a gate/wall piece collapses (`PieceCollapsing`), a ladder reaches `LadderPhase.Up` / topples, or an allied squad moves outside the castle footprint.
  - When a probe unit confirms that an enclosed courtyard squad is unreachable from outside ground level at the current `LayoutRevision` + ladder state, other ground attackers on the same exterior component reuse the cached `unreachable` result for `ReachabilityCacheTtl` (e.g., `1.0s`) and only check `CalculatePath` against allies outside the walls or within local engagement range (aligning with `SIEGE-15` assault thresholds).

### 3. Enemy Archer Side-Step Search Budget (`SiegeAttackerBrain.TryFindFiringPosition`)
- **Problem**: When wall-top defenders crouch behind crenels (`COMBAT-10`) or stand behind merlons (`SIEGE-10`), `TryFindFiringPosition` tests `2 * ArcherSideStepCount` positions × `ActiveActors` raycasts per archer. In a 60-archer wave, simultaneous side-step searches cause severe physics spikes.
- **Implementation**:
  - Limit `TryFindFiringPosition` executions via a shared per-frame token budget (`SiegeSettings.MaxArcherRepositionSearchesPerFrame`, default `2`); archers waiting for a token keep `cover.Wait()` until the next tick.
  - Query `CombatSpatialIndex` within `MaximumAttackRange` instead of `CharacterActor.ActiveActors` inside `CanShootFrom(standPoint)`, and stop at the top-N nearest defenders (`N = 4`).

### 4. Verification
- **EditMode tests**: `SquadThreatWatchTests` and `SiegeReachabilityCacheTests` covering cache invalidation on gate breach, ladder raise, and member hit wake-up.
- **PlayMode & 200v200 Siege Benchmark**: Verify closed-wall siege assault, gate breach transition, and 60-archer volley performance without behavioral regressions in `EnemyCompositionPlayTests` and `SiegePrototype`.
