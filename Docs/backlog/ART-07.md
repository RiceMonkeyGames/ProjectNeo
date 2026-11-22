---
id: ART-07
title: Dynamic Castle Wall Trim (Buttresses, Plinth, Corbels, Corner Turrets)
status: In Progress
assignee: youngho
branch: feature/art-07
area: ART
depends_on: [ART-06, Q-01]
created: 2026-11-22
---

# ART-07 - Dynamic Castle Wall Trim (Buttresses, Plinth, Corbels, Corner Turrets)

## Scope & Completion Criteria

Registered 2026-11-22 from the user's request: the castle walls are textured boxes with no relief. Add `PolygonFantasyKingdom`-style relief that is generated from the current wall layout and appears or disappears as the player extends or removes walls (no hand-placed decoration).

User decisions (2026-11-22):

- Elements: buttress pilasters, a low plinth at the wall base, a corbel band below the top parapet, and corner turrets on convex outer corners.
- Visual only: no colliders, no NavMesh, cover, line-of-sight or combat rule changes.
- Outer faces only (faces that `BattlementRules` treats as outside); courtyard faces stay plain.
- Separate from `SIEGE-13` (battlement rules, 6m gate); this task does not wait for the cover-height decision.

Done when: the trim rebuilds on every layout change (place, remove, stack, collapse, save load), stays stable when a run is extended (grid-aligned spacing), follows stacked tiers as one height, skips stairs, gate and pieces under construction, and is verified in the Game scene. Coordinate the corbel band look with `SIEGE-12` gate machicolations (teammate) so the two do not overlap.
