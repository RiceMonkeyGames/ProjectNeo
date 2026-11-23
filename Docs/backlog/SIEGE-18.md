---
id: SIEGE-18
title: Game 5m Grid & Default Castle Layout
status: Waiting for Prerequisite
assignee: youngho
branch:
area: SIEGE
depends_on: [SIEGE-14, SIEGE-27]
created: 2026-11-23
---

# SIEGE-18 - Game 5m Grid & Default Castle Layout

## Scope & Completion Criteria

Registered 2026-11-23 from the team decision to move the Game to the 5m castle kit ([Decisions-Resolved](../plan/Decisions-Resolved.md)); source: [SIEGE-14](../history/SIEGE/SIEGE-14.md) breakage list items 1, 3, 11 and 12. First migration step; the other kit tasks depend on it.

- Switch the Game grid to 5m cells and 5m tiers (`Editor/GameFlowSetup`: 80x100 cells of 3m become about 48x60; the 96m ring becomes 95m or 100m; gate column, courtyard facility and squad cells re-laid out). Record the chosen sizes in the map scale standard of [WallsAndSiege](../design/WallsAndSiege.md).
- Apply [Q-59](../history/Q/Q-59.md) (decided 2026-11-23): Game grid `maxLevels` 4 with walls, bastions, stairs and the gatehouse column limited to storey 3 (only tower tiers on storey 4; add a per-piece storey cap with EditMode tests); one-storey (5m) default ring built from the same modular kit pieces as player walls; two-storey gatehouse; `forbidValleys` on in the Game grid. Steps up to three-storey walls (1↔3, 2↔3) use the interior stair rooms, and the player stair piece is ground-only, from [SIEGE-27](SIEGE-27.md), which is built and verified in the spike first.
- Tune fall damage (`FallSettings`) and jump checks for 5m storeys, and the construction camera zoom and grid lines (`BuildCameraSettings`).
- Regenerate or explicitly retire the 3m prototype scenes (`ConstructionPrototype`, `SiegePrototype`, `WallCommandPrototype`).

Done when the Game builds, plays and saves a new run on the 5m grid with placeholder visuals, and EditMode/PlayMode suites pass (Game test fixes are tracked in `QA-castle-kit-tests`).
