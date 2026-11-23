---
id: SIEGE-29
title: Tower Storey Rooms & Doors at Every Walkway Height
status: Waiting for Prerequisite
assignee: youngho
branch:
area: SIEGE
depends_on: [SIEGE-27]
created: 2026-11-23
---

# SIEGE-29 - Tower Storey Rooms & Doors at Every Walkway Height

## Scope & Completion Criteria

Registered 2026-11-23 from the user's spike review ([Decisions-Resolved](../plan/Decisions-Resolved.md)): a tower has a room and door only in its top storey (`TowerRules.RoomOf`), so a tower more than one storey above a touching wall (for example 15m beside a one-storey wall) has no door from that walkway. Tower height from the castle wall tier (`SIEGE-28`) will make this common. Rules: Towers in [WallsAndSiege](../design/WallsAndSiege.md). Build and verify in `SyntyGridSpike`.

- Pure rule (EditMode): every tower storey from the lowest touching walkway floor up is a room; each room storey gets a door on every face that meets a walkway at its floor height; storeys below the lowest touching walkway stay solid with no stairs down (a 3-storey tower between two 2-storey walls has rooms only from 10m up).
- Dressing: interior stairs between room storeys reuse the `SIEGE-27` stair module; the top storey keeps its arrow slits, roof opening and the roof mount points clear of the stair opening.
- Colliders and NavMesh: player and units can enter from each touching walkway and climb to the roof and back; rebuild on placement, removal, wall height changes and collapse.

Done when, in the spike scene, a tower beside one-storey walls (2, 3 and 4 storeys tall) and a 3-storey tower between two 2-storey walls get the right rooms, doors and stairs, the player and an allied unit can climb from each walkway to the roof and back, the EditMode rule tests pass and the full EditMode/PlayMode suites stay green. Game integration is `SIEGE-24`.
