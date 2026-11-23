---
id: SIEGE-27
title: Interior Stair Rooms for Steps to Three-Storey Walls
status: In Progress
assignee: youngho
branch: feature/siege-27
area: SIEGE
depends_on: []
created: 2026-11-23
---

# SIEGE-27 - Interior Stair Rooms for Steps to Three-Storey Walls

## Scope & Completion Criteria

Registered 2026-11-23 from [Q-59](../history/Q/Q-59.md): walls reach 3 storeys, and the automatic wall stairs (`CastleKitRules.AddStairs`) connect only walls one storey apart, so a one-storey wall beside a three-storey wall is cut off. Revised by the user the same day after reviewing the spike: an outside 45° flight takes a whole 5m cell of the lower walkway, so every step up to a three-storey wall (1↔3 and 2↔3) uses an interior room, and only 1↔2 keeps the outside stairs ([Decisions-Resolved](../plan/Decisions-Resolved.md)). Build and verify this in the 5m spike scene (`SyntyGridSpike`) before the Game migration (`SIEGE-18` waits for it). Rules: Interior stair rooms in [WallsAndSiege](../design/WallsAndSiege.md).

- Pure rule (EditMode): find steps up to a three-storey wall column from a one- or two-storey neighbour, and choose the room cell, the room storeys (from the lowest touching walkway floor up), door faces and stair layout per storey. Keep the stair layout reusable for towers (`SIEGE-29`).
- Dressing (`CastleKitDresser`, `CastleKitRules`): room storeys with floors (5m and 10m beside a one-storey walkway; only 10m beside two-storey walkways, with solid storeys below), switchback stairs with a mid landing per room storey, a roof opening onto the top walkway, and a door on every face that meets a walkway at the same floor height (a 1-3-2 row: lower door on the one-storey side, upper door on the two-storey side). No arrow slits. Outside automatic stairs only for 1↔2 steps. Improving the shared modular dressing for this is allowed.
- Placement: on the 5m grid the player-built stair piece goes on the ground only (no stairs on wall tops); update the placement rule, its HUD message and EditMode tests.
- Colliders and NavMesh: player and units can walk from the low walkway through the room to the top walkway and back; rebuild on placement, removal and collapse.
- Collapse follows the no-valley rule (the storeys above go with it, the room and stairs disappear); a fallen lower neighbour leaves the door as an opening.

Done when, in the spike scene, a 1-3, a 2-3 and a 1-3-2 row each get the right rooms and doors that the player and an allied unit can climb in both directions, a 1-2 step keeps the outside stairs, a stair piece cannot be placed on a wall top, the EditMode rule tests pass and the full EditMode/PlayMode suites stay green. Enemy target choice for these rooms is `SIEGE-26`.
