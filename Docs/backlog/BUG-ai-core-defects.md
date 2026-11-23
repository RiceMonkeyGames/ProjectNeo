---
id: BUG-ai-core-defects
title: Fix AI Machicolation Leak, Hold Fallback, Flow Field Elevation & Ladder Crowding
status: In Progress
assignee: SeongminHwang
branch: fix/bug-ai-core-defects
area: BUG
depends_on: []
created: 2026-11-23
---

# BUG-ai-core-defects — Fix AI Machicolation Leak, Hold Fallback, Flow Field Elevation & Ladder Crowding

## Scope & Completion Criteria

Fix four priority-1 runtime AI defects found in [CombatAndSquads](../codemap/CombatAndSquads.md) and [WallsAndSiege](../codemap/WallsAndSiege.md):

1. **Machicolation claim leak on Hold squad sleep (`UnitCombatAI`)**: Release `ShotOpening` even when `squadNeighborhoodClear` is true so defenders returning to `HoldPost` after enemies clear do not permanently lock the opening.
2. **`FindTargetNearPost` elevation & fallback gap (`UnitCombatAI`)**: Prevent unreachable lower-level enemies beneath walls from filling the top-K candidate slots and hiding reachable same-level enemies; add a fallback pass over remaining candidates when all top-K fail `CanReachInHold`.
3. **Flow Field ground elevation & LOD2/Advance guard (`BattleFlowFieldService`, `UnitCombatAI`)**: Compute `groundElevation` from the ground baseline (minimum actor elevation, not average across wall-top defenders) so ground cells are not falsely marked blocked, and restrict Flow Field steering to `AILodTier.Lod2` units without `HasAdvancePoint`.
4. **Allied ladder approach crowding (`LadderTraveller`, `SiegeLadder`)**: Track approach-time allied ladder reservations before arrival so a squad moving across walls splits across available ladders instead of queuing behind one nearest ladder while `Waiting.Count == 0`.

### Completion Criteria
- Pure rules extracted and verified by EditMode tests.
- PlayMode regression tests cover machicolation release on sleep, wall-top Hold target acquisition over lower-level enemies, Flow Field ground elevation / LOD2 guard, and multi-ladder allied distribution.
- `code-rules-check.ps1`, scoped + regression Unity EditMode/PlayMode suites, and `doc-finish-check.ps1 -StrictDocs` pass.
