---
id: SIEGE-25
title: Piece Balance Pass for the 7.5m Grid
status: Waiting
assignee: youngho
branch:
area: SIEGE
depends_on: [SIEGE-18, SIEGE-40]
created: 2026-11-23
---

# SIEGE-25 - Piece Balance Pass for the 7.5m Grid

## Scope & Completion Criteria

**Rescoped 2026-11-27 (user, basin map decision): balance against the new cell size (`SIEGE-38`) and the single north wall (`SIEGE-40`) instead of the 7.5m ring. Start after both.**

Registered 2026-11-23 from the team decision to move the Game to the 5m castle kit ([Decisions-Resolved](../plan/Decisions-Resolved.md)); source: [SIEGE-14](../history/SIEGE/SIEGE-14.md) breakage list items 8 and 10. Rescoped 2026-11-26 (user) to the 7.5m cells with 5m storeys of [SIEGE-35](../history/SIEGE/SIEGE-35.md) and [SIEGE-18](../history/SIEGE/SIEGE-18.md).

- One 7.5m cell is 2.5 times as long as a 3m cell (6.25 times the area). SIEGE-18 only scaled wall HP and cost per cell (HP 150, 15 gold, 1.5 times the 5m values); bastions are unchanged (HP 150, 25 gold). Rebalance wall, bastion and gate HP, build and repair cost and time, wall-breaker damage and the cannon blast radius against cells.
- Stairs: the player stair is 5m wide and centred and climbs across the 7.5m cell at about 34 degrees (SIEGE-35). Set stair HP relative to walls (spike 50 vs 100, since stairs that block a breach should be easier to break).

Done when the values are updated in data assets, recorded in the design docs and checked in a full Game run.
