---
id: PERF-ai-flow-field
title: Hybrid Flow Field Navigation for Far-Distance (LOD 2) & Mass Siege Marching
status: Waiting
assignee: Unassigned
branch:
area: PERF
depends_on: []
created: 2026-11-23
---

# PERF-ai-flow-field — Hybrid Flow Field Navigation for Far-Distance (LOD 2) & Mass Siege Marching

## Scope & Completion Criteria

Follow-up to [PERF-ai-lod](../history/PERF/PERF-ai-lod.md) and [PERF-siege-ai-scale](../history/PERF/PERF-siege-ai-scale.md). Replaces individual `NavMesh.CalculatePath` calls on far-distance (`AILodTier.Lod2`) and mass-marching ground units with a shared 2D Flow Field (integration + vector field) while preserving exact `NavMeshAgent` pathfinding for near/mid-range combat (`Lod0`/`Lod1`) and multi-tier wall/stair traversal.

### 1. Pure C# 2D Flow Field Generator (`Combat/FlowFieldGrid.cs`)
- **Grid & Cost Field**:
  - Uniform 2D XZ grid (configurable `cellSize`, default `2.5f`–`3.0f` meters matching castle grid scale) with zero-allocation pre-allocated arrays (`cost[]`, `integration[]`, `direction[]`, `queue[]`).
  - Marks impassable cells from ground-level obstacles, closed gates, and intact level-0 castle walls.
- **Integration & Direction Field**:
  - Multi-seed Dijkstra / BFS wave propagation from one or more goal positions (e.g., allied frontline cluster centroids, gate approach points, ladder bases, or squad move destinations).
  - Computes normalized 8-neighbor gradient direction vectors (`Vector2`) per cell and provides bilinear-interpolated world direction sampling `TrySampleDirection(Vector3 worldPos, out Vector3 flowDirection)`.

### 2. Shared Runtime Flow Field Service (`Combat/BattleFlowFieldService.cs`)
- Maintains shared flow fields keyed by tactical goal group:
  - **Hostile Assault / Siege March Field**: seeded by reachable allied ground clusters (or active breach / gate / ladder base targets), shared across all marching ground enemies.
  - **Allied Squad / Army Move Field**: seeded per squad destination cluster when a squad or full army marches over long distance.
- Rebuilds lazily at most once per rebuild interval (e.g., `0.5s`–`1.0s`) or immediately when `ConstructionWorld.NavigationRevision` changes (wall/gate breach).

### 3. Hybrid LOD 2 Steering & Seamless NavMesh Handoff (`UnitCombatAI` & `SiegeAttackerBrain`)
- **Flow Field Steering Mode**:
  - Ground units (`|y - groundY| < 1m`) in `AILodTier.Lod2` whose target or destination is beyond `FlowFieldMinDistance` (e.g., `20m`, configurable in `UnitAISettings`) steer along the sampled flow vector using short local NavMesh steps / velocity steering without calling full-map `agent.CalculatePath`.
- **Handoff to Exact NavMesh**:
  - Automatically switches back to standard `NavMeshAgent` pathfinding + `MeleeSlotBoard` ring slots when the unit enters `Lod1`/`Lod0`, approaches within `FlowFieldMinDistance` of its target/destination, boards a ladder, or climbs stairs onto elevated wall tiers.

### 4. Verification & Visualization
- **EditMode tests (`FlowFieldGridTests`)**: Verify obstacle routing around U-shaped walls, multi-seed convergence, unreachable island handling (`Vector3.zero`), and zero-allocation rebuilds.
- **PlayMode & 200v200 Visual Verification**:
  - Verify `UnitCombatPlayTests` and `SiegePrototypePlayTests` regressions pass.
  - Add optional flow-vector arrow gizmo / toggle in `LargeBattleVerification.unity` (`LargeBattleDebugHud`) and confirm per-frame `CalculatePath` calls drop near zero during 200 vs 200 long-distance approach.
