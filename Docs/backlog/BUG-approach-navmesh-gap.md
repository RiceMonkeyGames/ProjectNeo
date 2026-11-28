---
id: BUG-approach-navmesh-gap
title: Approach Field NavMesh Gap Between Spawn Field And Wall
status: In Progress
assignee: SeongminHwang
branch: fix/bug-approach-navmesh-gap
area: SIEGE
depends_on: [SIEGE-40]
created: 2026-11-28
---

# BUG-approach-navmesh-gap - Approach Field NavMesh Gap Between Spawn Field And Wall

## Scope & Completion Criteria

Registered 2026-11-28 by the user: in the basin `Game` scene (`SIEGE-40`) enemies cannot walk from the spawn field to the north wall. `NavMesh.CalculatePath` from the spawn (about world (10, -18.85, 219.5)) to the wall front or the gate front returns `PathPartial`, ending near (-11.50, -18.44, 209.47).

Cause (diagnosed 2026-11-28): the hidden flat build slab (`ConstructionWorld/Geometry/Ground`) covers only the 12-row build band (world z 118.6 to 178.6). The far field's `ApproachGround` pieces with enabled colliders start around z 188 to 226, and the near pieces (`SM_Generic_Ground_*`) between them have their MeshColliders disabled (already so in the basin draft scene). So there is no collider at all between the band's north edge and the field, and the NavMesh splits into two islands.

- Fix chosen by the user (2026-11-28): a hidden, collision-only bridge mesh that follows the visible ground from the band's north edge to the field, blending from the flat build plane height at the band edge into the visible ground height. The build band itself stays unchanged.
- Apply through an Editor setup (no hand edits of serialized files); re-running gives the same result.
- Done when paths from the spawn field to the wall front and the gate front are `PathComplete`, and the PlayMode tests that need enemies to approach (`GameBattlePlayTests` stage-2 approach, `GameCannonPlayTests.RealCannon_ArmsOnBattleStart_FiresAtEnemies_AndDisarmsAfterward`, `GameWavePlayTests` retreat tests, `GameBossPlayTests` retreat test) are re-checked and reported.
