---
id: SIEGE-20
title: Kit Battlements, Cover Heights & Crenel Positions
status: In Progress
assignee: youngho
branch: feature/siege-20
area: SIEGE
depends_on: [SIEGE-19]
created: 2026-11-23
---

# SIEGE-20 - Kit Battlements, Cover Heights & Crenel Positions

## Scope & Completion Criteria

Registered 2026-11-23 from the team decision to move the Game to the 5m castle kit ([Decisions-Resolved](../plan/Decisions-Resolved.md)); source: [SIEGE-14](../history/SIEGE/SIEGE-14.md) breakage list item 4. Rescoped 2026-11-26 (user) to the 7.5m cells with 5m storeys of [SIEGE-35](../history/SIEGE/SIEGE-35.md) and [SIEGE-18](../history/SIEGE/SIEGE-18.md).

- Kit `Battlements_01` is a 1.38m merlon with a 0.76m crenel sill and a merlon at the module middle, versus `BattlementSettings` 1.1m sill / 2.2m merlons with a crenel at every edge middle. On the 7.5m grid the kit battlement module is stretched 1.5 times along the face (SIEGE-35 stretch look), while the rule pattern is five 0.75m crenels per 7.5m edge at a 1.5m cycle (COMBAT-15). Decide the cover rule on the kit shape (2m units) and update `BattlementRules` patterns, crenel stand points, crouch-reload (COMBAT-10), the ladder hook point (`SillHeightAt`) and `ShotCover`.
- Check wall-top archer sight lines through kit crenels and the gun port rule on bastion and tower faces. Recheck the COMBAT-15 ranged front ranks on wall tops if crenel stand points move.

- **Decided 2026-11-27 (user)**: option B. Scale only the kit battlement pieces vertically to the cover heights, take crenel stand points, `ShotCover` and the ladder sill from the scaled kit crenels, and move the pre-placed ladder slots from the edge middle onto kit crenels. Ladder carriers are split out to [SIEGE-37](SIEGE-37.md). Scale factor (user, 2026-11-27): 1.6x, giving a 1.2m crenel sill and 2.2m merlons (1.45x for a 1.1m sill was rejected). Second round (user, 2026-11-27): the kit look follows the level; Lv1 is `Battlements_01` sunk into the wall (low stones 1.216m, gaps 0.224m), Lv2 is `Battlements_03` (about 1.4x, 0.45m out past the wall), gun ports keep the Lv1 look; the rule follows each look ([Decisions-Resolved](../plan/Decisions-Resolved.md)).

Done when wall-top defenders take cover and fire through kit crenels with the agreed rule, covered by EditMode/PlayMode tests. Coordinate with the teammate who owns combat cover.
