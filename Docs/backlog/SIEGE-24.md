---
id: SIEGE-24
title: Towers in the Game
status: In Progress
assignee: youngho
branch: feature/siege-24
area: SIEGE
depends_on: [SIEGE-19, SIEGE-23, SIEGE-29, SIEGE-30]
created: 2026-11-23
---

# SIEGE-24 - Towers in the Game

## Scope & Completion Criteria

Registered 2026-11-23 from the team decision to move the Game to the 5m castle kit ([Decisions-Resolved](../plan/Decisions-Resolved.md)); source: [SIEGE-14](../history/SIEGE/SIEGE-14.md) breakage list item 17. Design: Towers in [WallsAndSiege](../design/WallsAndSiege.md). Rescoped 2026-11-26 (user) to the 7.5m cells with 5m storeys of [SIEGE-35](../history/SIEGE/SIEGE-35.md) and [SIEGE-18](../history/SIEGE/SIEGE-18.md).

- Add the 2x2 `Tower` piece (15m × 15m on 7.5m cells) to the Game: definition and prefab, cost in `BuildCosts.asset` (60 gold temporary), HUD button in `SiegeHud` with the tower placement messages, `TowerOccupantBuff` on the Game construction world, morale on tower loss (`GameSquadMorale`, like bastions), and saving and loading tower tiers.
- Towers reach up to 4 storeys (20m) on the Game grid, one above 3-storey walls (2 storeys beside the default walls) ([Q-59](../history/Q/Q-59.md)); the grid level limit comes from `SIEGE-18`. Keep the height rule easy to swap: `SIEGE-28` later sets every tower's height from the castle wall tier instead of the touching walls.
- Bring over the 7.5m spike's (`SyntyGrid75Spike`) tower storey rooms and doors at every touching walkway height ([SIEGE-29](../history/SIEGE/SIEGE-29.md)) and the tower collapse stump and wall scars ([SIEGE-30](../history/SIEGE/SIEGE-30.md)).
- The default castle already has 2×2 two-storey corner towers made of wall cells (SIEGE-36); ask the user whether they stay wall cells or become `Tower` pieces.
- Tune HP (400 per tier temporary), the roof mounts and the +20% range and attack buff after playtest.

Done when towers can be built, used by archers and cannons, and lost in a Game run, with tests for placement, buff and saving. Enemy behaviour toward towers is in `SIEGE-26`.
