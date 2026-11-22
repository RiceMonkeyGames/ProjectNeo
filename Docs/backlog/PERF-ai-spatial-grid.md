---
id: PERF-ai-spatial-grid
title: 2D Spatial Hash Grid for Fast AI Proximity & Target Queries
status: In Progress
assignee: 신용준
branch: feature/perf-ai-spatial-grid
area: PERF
depends_on: [PERF-ai-query-budget]
created: 2026-11-22
---

# PERF-ai-spatial-grid — 2D Spatial Hash Grid for Fast AI Proximity & Target Queries

## Scope & Completion Criteria

Phase 2 of the 200 vs 200 large-scale battle AI optimization pass ([Combat](../design/Combat.md), [CombatAndSquads codemap](../codemap/CombatAndSquads.md)). Replaces `O(N)` full-list scans over `CharacterActor.ActiveActors` (400 units) in short- and medium-range queries with a zero-allocation 2D XZ spatial hash grid.

### 1. Pure C# 2D Spatial Grid (`Combat/CombatSpatialGrid.cs`)
- **Data structure**:
  - Pure generic grid `CombatSpatialGrid<T>` partitioning the XZ plane into uniform square cells (`cellSize = 8f` meters, configurable in `UnitAISettings`).
  - Uses pre-allocated flat arrays / linked-index lists (`head[]` + `next[]` per actor index, capacity growing by power of two) so rebuilding and querying allocate zero GC memory per frame.
  - Supports team-separated buckets (`CombatTeam.Allied`, `CombatTeam.Enemy`) so queries iterate only opposing units.
- **Query API**:
  - `QueryCircle( CombatTeam enemyTeam, Vector3 center, float radius, List<T> results )`: collects all opposing units in cells overlapping the circle's XZ bounding box.
  - Fallback: when `DetectionRange` exceeds a configurable full-scan threshold (e.g., `> 48m`), callers can either query the bounded grid window or fall back to the team list without duplicating allocation.

### 2. Runtime Service & Frame Rebuild (`Combat/CombatSpatialIndex.cs`)
- Lightweight runtime updater that rebuilds `CombatSpatialGrid<CharacterActor>` once per frame (or on first query of `Time.frameCount`) from `CharacterActor.ActiveActors`.
- Automatically cleared on scene unload / disable so test fixtures never leak stale actor references.

### 3. Query Call-Site Integration
- **`UnitCombatAI`**:
  - `FindMeleeThreat` (radius `RangedMeleeThreatRadius` = 3m): query only the 3x3 local cells around the archer instead of scanning 400 actors.
  - `FindTargetInReach` (radius `CombatPhysics.MaximumAttackRange(actor)` + max body radius pad, typically 3–25m): query overlapping cells.
  - `FindTargetNearPost` (radius `HoldChaseRadius`, 6–26m): query overlapping cells centered at `HoldPost`.
  - `FindTarget` (radius `DetectionRange`): query cells within `DetectionRange`.
- **`HeroSkillAI` & `ShotOpening`**:
  - Route `SkillAimFinder` area cluster lookups (`COMBAT-13`) and `ShotOpening.HasEnemyBelow` (`SIEGE-12`) through `CombatSpatialIndex` to avoid nested `O(N^2)` scans during 200v200 clashes.

### 4. Verification
- **EditMode tests (`CombatSpatialGridTests`)**: Verify cell coordinate hashing (including negative world coordinates), boundary-straddling units, team filtering, zero-allocation reuse, and exact equivalence against brute-force radius queries.
- **PlayMode & Benchmark**: Run `UnitCombatPlayTests`, `WallTopArcherPlayTests`, `MachicolationPlayTests`, and 200v200 `UnitAIQueryBenchmark` contact scenarios.
