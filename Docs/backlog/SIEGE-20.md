---
id: SIEGE-20
title: Kit Battlements, Cover Heights & Crenel Positions
status: Waiting for Prerequisite
assignee: youngho
branch:
area: SIEGE
depends_on: [SIEGE-19]
created: 2026-11-23
---

# SIEGE-20 - Kit Battlements, Cover Heights & Crenel Positions

## Scope & Completion Criteria

Registered 2026-11-23 from the team decision to move the Game to the 5m castle kit ([Decisions-Resolved](../plan/Decisions-Resolved.md)); source: [SIEGE-14](../history/SIEGE/SIEGE-14.md) breakage list item 4.

- Kit `Battlements_01` is a 1.38m merlon with a 0.76m crenel sill and a merlon at the module middle, versus `BattlementSettings` 1.1m sill / 2.2m merlons with a crenel at every edge middle. Decide the cover rule on the kit shape (2m units) and update `BattlementRules` patterns, crenel stand points, crouch-reload (COMBAT-10), the ladder hook point (`SillHeightAt`) and `ShotCover`.
- Check wall-top archer sight lines through kit crenels and the gun port rule on bastion and tower faces.

Done when wall-top defenders take cover and fire through kit crenels with the agreed rule, covered by EditMode/PlayMode tests. Coordinate with the teammate who owns combat cover.
