---
id: BUG-basin-retreat-outward
title: Retreat Point Must Lead Away From The Wall On The Basin Map
status: In Progress
assignee: SeongminHwang
branch: fix/bug-basin-retreat-outward
area: SIEGE
depends_on: [SIEGE-40, BUG-approach-navmesh-gap]
created: 2026-11-28
---

# BUG-basin-retreat-outward - Retreat Point Must Lead Away From The Wall On The Basin Map

## Scope & Completion Criteria

Registered 2026-11-28 by the user: on the basin `Game` map (`SIEGE-40`) enemies spawn about 35m beyond the build grid's outward edge (spawn cell (29, 19), world z about 213). `SiegeAttackerBrain.TryGetRetreatPoint` (and `FlyerBrain.RetreatPoint`) send retreating enemies to the grid's outward edge (local z = `Depth * CellSize - RetreatEdgeMargin`, world z about 178), so an enemy already past the edge walks back toward the wall.

Rule (user decision 2026-11-28): the retreat point is the farther of the grid's outward edge and a point a fixed distance further outward from the enemy's current position. If that point is off the NavMesh, search back toward the enemy for the nearest walkable point. Flyers follow the same rule.

Done when `GameBattlePlayTests.공성병이_없고_진입로가_없으면_남은_궁수가_퇴각하고_승리한다` passes and the `GameWavePlayTests` and `GameBossPlayTests` retreat tests still pass.

Enemy assault is the teammate's area; the user took this fix on 2026-11-28.
