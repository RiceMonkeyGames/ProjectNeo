---
id: SIEGE-18
title: Game 5m Grid & Default Castle Layout
status: Waiting
assignee: youngho
branch:
area: SIEGE
depends_on: [SIEGE-14]
created: 2026-11-23
---

# SIEGE-18 - Game 5m Grid & Default Castle Layout

## Scope & Completion Criteria

Registered 2026-11-23 from the team decision to move the Game to the 5m castle kit ([Decisions-Resolved](../plan/Decisions-Resolved.md)); source: [SIEGE-14](../history/SIEGE/SIEGE-14.md) breakage list items 1, 3, 11 and 12. First migration step; the other kit tasks depend on it.

- Switch the Game grid to 5m cells and 5m tiers (`Editor/GameFlowSetup`: 80x100 cells of 3m become about 48x60; the 96m ring becomes 95m or 100m; gate column, courtyard facility and squad cells re-laid out). Record the chosen sizes in the map scale standard of [WallsAndSiege](../design/WallsAndSiege.md).
- Decide the default wall height on 5m tiers with [Q-59](Q-59.md) (one storey is already demo height; Q-01 had chosen two 3m tiers) and whether the valley rule (`forbidValleys`) is on in the Game.
- Tune fall damage (`FallSettings`) and jump checks for 5m storeys, and the construction camera zoom and grid lines (`BuildCameraSettings`).
- Regenerate or explicitly retire the 3m prototype scenes (`ConstructionPrototype`, `SiegePrototype`, `WallCommandPrototype`).

Done when the Game builds, plays and saves a new run on the 5m grid with placeholder visuals, and EditMode/PlayMode suites pass (Game test fixes are tracked in `QA-castle-kit-tests`).
