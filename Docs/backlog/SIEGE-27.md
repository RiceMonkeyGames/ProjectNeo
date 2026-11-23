---
id: SIEGE-27
title: Interior Stair Rooms for Two-Storey Wall Steps
status: Waiting
assignee: youngho
branch:
area: SIEGE
depends_on: []
created: 2026-11-23
---

# SIEGE-27 - Interior Stair Rooms for Two-Storey Wall Steps

## Scope & Completion Criteria

Registered 2026-11-23 from [Q-59](../history/Q/Q-59.md): walls reach 3 storeys, and the automatic wall stairs (`CastleKitRules.AddStairs`) connect only walls one storey apart, so a one-storey wall beside a three-storey wall is cut off. Build and verify this in the 5m spike scene (`SyntyGridSpike`) before the Game migration (`SIEGE-18` waits for it). Rules: Interior stair rooms in [WallsAndSiege](../design/WallsAndSiege.md).

- Pure rule (EditMode): find two-storey steps between neighbouring wall columns (one storey beside three) and choose the room cell, door faces and stair layout per storey.
- Dressing (`CastleKitDresser`, `CastleKitRules`): in the three-storey room cell, hollow storeys with floors at 5m and 10m, switchback stairs with a mid landing per storey, a roof opening onto the top walkway, and a door on every face that meets a walkway at the same floor height (a 1-3-2 row: lower door on the one-storey side, upper door on the two-storey side). No arrow slits. Improving the shared modular dressing for this is allowed.
- Colliders and NavMesh: player and units can walk from the low walkway through the room to the top walkway and back; rebuild on placement, removal and collapse.
- Collapse follows the no-valley rule (the storeys above go with it, the room and stairs disappear); a fallen lower neighbour leaves the door as an opening.

Done when, in the spike scene, a one-storey wall beside a three-storey wall gets a room (and a 1-3-2 row gets both doors) that the player and an allied unit can climb in both directions, one-storey steps keep the outside stairs, the EditMode rule tests pass and the full EditMode/PlayMode suites stay green. Enemy target choice for these rooms is `SIEGE-26`.
