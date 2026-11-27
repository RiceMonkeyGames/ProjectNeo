---
id: SIEGE-40
title: Basin Map & North Wall Build Grid
status: Waiting
assignee: Unassigned
branch:
area: SIEGE
depends_on: [SIEGE-38]
created: 2026-11-27
---

# SIEGE-40 - Basin Map & North Wall Build Grid

## Scope & Completion Criteria

Registered 2026-11-27 from the basin map decision ([Decisions-Resolved](../plan/Decisions-Resolved.md), [WallsAndSiege](../design/WallsAndSiege.md)). The cell size comes from `SIEGE-38`.

- `Game` scene: close the west, east and south sides with impassable mountain terrain (colliders and NavMesh blocking), keep the northern approach and enemy spawns, and remove the 26-cell wall ring and its corner towers.
- Shrink `BuildGridDefinition` / `BuildLayout` / `PlacementController` to the north wall band (wall line plus a front and back strip). Placement outside the band is rejected; the construction camera and `PlacementMapHud` show only the band.
- New run-start north wall (one storey, gate in the middle; tower and stair layout to confirm with the user). Ladder lanes and spawn points re-laid for the new wall length.
- `FlowFieldGrid` and enemy pathing: treat mountains as blocked; check that enemies reach the north wall and the gate. Coordinate with the teammate who owns enemy AI.
- Run save version bump; old saves dropped.

Open before start (ask the user): north wall length and band depth, basin size, gate position, whether the run-start wall has towers.

Done when the Game runs a full wave against the north wall only, building works only in the band, and EditMode/PlayMode tests for grid limits, layout and pathing pass.
