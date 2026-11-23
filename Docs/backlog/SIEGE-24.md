---
id: SIEGE-24
title: Towers in the Game
status: Waiting for Prerequisite
assignee: youngho
branch:
area: SIEGE
depends_on: [SIEGE-19, SIEGE-23]
created: 2026-11-23
---

# SIEGE-24 - Towers in the Game

## Scope & Completion Criteria

Registered 2026-11-23 from the team decision to move the Game to the 5m castle kit ([Decisions-Resolved](../plan/Decisions-Resolved.md)); source: [SIEGE-14](../history/SIEGE/SIEGE-14.md) breakage list item 17. Design: Towers in [WallsAndSiege](../design/WallsAndSiege.md).

- Add the 2x2 `Tower` piece to the Game: definition and prefab, cost in `BuildCosts.asset` (60 gold temporary), HUD button in `SiegeHud` with the tower placement messages, `TowerOccupantBuff` on the Game construction world, morale on tower loss (`GameSquadMorale`, like bastions), and saving and loading tower tiers.
- Towers reach up to 4 storeys (20m) on the Game grid, one above 3-storey walls (2 storeys beside the default walls) ([Q-59](../history/Q/Q-59.md)); the grid level limit comes from `SIEGE-18`. Keep the height rule easy to swap: `SIEGE-28` later sets every tower's height from the castle wall tier instead of the touching walls.
- Tune HP (400 per tier temporary), the roof mounts and the +20% range and attack buff after playtest.

Done when towers can be built, used by archers and cannons, and lost in a Game run, with tests for placement, buff and saving. Enemy behaviour toward towers is in `SIEGE-26`.
