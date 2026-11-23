---
id: PERF-ai-alloc-and-split
title: Remove Per-Frame AI Allocations & Refactor UnitCombatAI Sub-States
status: In Progress
assignee: SeongminHwang
branch: feature/perf-ai-alloc-and-split
area: PERF
depends_on: []
created: 2026-11-23
---

# PERF-ai-alloc-and-split — Remove Per-Frame AI Allocations & Refactor UnitCombatAI Sub-States

## Scope & Completion Criteria

Reduce GC/physics overhead and improve maintainability across [UnitCombatAI](../codemap/CombatAndSquads.md), [SiegeLadder](../codemap/WallsAndSiege.md), [FlyerBrain](../codemap/WallsAndSiege.md), and [WallBreakerAttack](../codemap/WallsAndSiege.md):

- **`SiegeLadder.FindPushers`**: Replace per-frame full `ActiveActors` loops and repeated `GetComponent` calls with spatial queries and cached references; prune dead/disabled entries from `aiPushHandles`.
- **`FlyerBrain`**: Replace `Physics.RaycastAll` allocations in `IsSheltered` and `UpdateBody` with `RaycastNonAlloc` shared buffers, add throttled ground/shelter caching, and apply decision staggering + AI LOD.
- **`WallBreakerAttack`**: Cache structure colliders on `PlacedStructure` and reuse a preallocated 4-face approach point buffer in `TryApproach`.
- **`UnitCombatAI` sub-state cleanup**: Consolidate transient state cleanup (`opening`, `meleeSlot`, `fallback`, `flowField`) and extract self-contained helper structs/classes to prevent state-interaction leaks.
- Verify with EditMode/PlayMode regression suites and `UnitAIQueryBenchmark` / allocation checks.
