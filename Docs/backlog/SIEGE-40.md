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

**Progress 2026-11-28 (`feature/siege-40`, user checked it in play):**
- Rules: `BuildGridDefinition.castleBehindSouthEdge` / `blockedSideCells`, used by `BattlementRules`, `CastleFootprint`, `GamePrepPlacement`, `BuildLayout`, `TowerRules`; tests `NorthWallBasinRulesTests`.
- Game scene rebuilt by menu `ProjectNeo/Setup/Apply Basin To Game Scene (SIEGE-40)` (`Editor/GameBasinSetup`): world reconfigured in place (references kept), basin environment moved under `Geometry`, ring courtyard art, citizens and the battle start flag removed, facilities attached to the user's demo houses with NPCs at the user's door spots (`NpcSpots`), spawn cell (29, 19), build camera, ladders.
- Player: ground stick and 0.15s jump grace for uneven ground (`PlayerInputController`).
- Run save version 4 (old saves dropped).
- Left for follow-ups: citizens on the demo paths, ladders by carriers (`SIEGE-37`), minimap (`UI-14`), kit module audit (`SIEGE-41`), Game lighting/fog vs the demo look, NavMesh bake time over the whole town.

**Scope (user, 2026-11-28)**: done together with [SIEGE-39](SIEGE-39.md) — the Game uses the basin draft (demo town and keep) as its world, with facilities, NPCs and citizens re-placed in the demo town. Squad deployment in preparation: the wall and everything south of it.

**Layout draft settled with the user (2026-11-28, on `feature/siege-38`, scene `Assets/_Project/Scenes/Preview/FantasyKingdomBasin.unity`)** — the Game layout should copy it:
- Basin: the Synty `PolygonFantasyKingdom` demo town and keep, rotated -90° under `DemoWorld` so the enemy side is +z; town walls and everything outside them removed by hand.
- Grid `NorthWallGrid`: 42 × 12 cells of 5m at (-125, -17.73, 118.64), 4 levels (`Data/BasinPreview/Grid.asset`). The wall row is the innermost row (z 118.6–123.6); the other 11 rows face the enemy.
- Run-start wall: one storey on cells 1–40 of the wall row, gatehouse (fixed gate + fixed wall) on cell 29 where the town road meets the wall. Cells 0 and 41 stay empty and are covered by decorative round end towers (Synty demo recipe, centres on the grid edges x -125 / 85); `CastleKitDresser.closedSideCells = 1` keeps kit dressing out of them. Block building on those end cells when moving to the Game.
- Ground: the grid slab keeps collision and navigation but its renderer is off; `ApproachGround` (FK `SM_Generic_Ground_*` pieces, colliders off over the band, mud patches and a mud road from the gate) covers it and the field north to the rim mountains; `NorthWallGrid/Foundation` adds kit `Wall_01` panels under the wall on both faces.
- Edit-mode preview: `ConstructionEditPreview` on the grid draws the run-start walls without Play.

Done when the Game runs a full wave against the north wall only, building works only in the band, and EditMode/PlayMode tests for grid limits, layout and pathing pass.
