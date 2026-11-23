---
id: SIEGE-18
title: Game 7.5m Grid & Default Castle Layout
status: In Progress
assignee: youngho
branch: feature/siege-18
area: SIEGE
depends_on: [SIEGE-14, SIEGE-27, SIEGE-35]
created: 2026-11-23
---

# SIEGE-18 - Game 7.5m Grid & Default Castle Layout

## Scope & Completion Criteria

Registered 2026-11-23 from the team decision to move the Game to the 5m castle kit ([Decisions-Resolved](../plan/Decisions-Resolved.md)); source: [SIEGE-14](../history/SIEGE/SIEGE-14.md) breakage list items 1, 3, 11 and 12. First migration step; the other kit tasks depend on it. *Updated 2026-11-23 by [SIEGE-35](../history/SIEGE/SIEGE-35.md)*: the user chose 7.5m × 7.5m cells with 5m storeys and the stretch kit look; read that history's "what else would change" list before re-laying the Game out.

- Switch the Game grid to 7.5m cells with 5m storeys (`Editor/GameFlowSetup`: 80x100 cells of 3m become about 32x40; the 96m ring becomes 97.5m (13 cells) or is re-decided; gate column, courtyard facility and squad cells re-laid out). Piece heights stay 5m per storey; check Game code that assumes storey height = cell size (`SiegeLadder` offsets, `MachicolationLayout`, `CastleFootprint`, `CastleBattlements`, construction camera and grid lines). Record the chosen sizes in the map scale standard of [WallsAndSiege](../design/WallsAndSiege.md).
- Port the 7.5m spike dressing (`CastleKitLook.Stretch`, unstretched centred gate frames, 5m-wide centred player stair prefab, stair-room sizes 2m landing / 2.16m flights, breach wedges) and rebalance per-cell costs and HP for the 1.5× wall length per piece.
- Apply [Q-59](../history/Q/Q-59.md) (decided 2026-11-23): Game grid `maxLevels` 4 with walls, bastions, stairs and the gatehouse column limited to storey 3 (only tower tiers on storey 4; add a per-piece storey cap with EditMode tests); one-storey (5m) default ring built from the same modular kit pieces as player walls; two-storey gatehouse; `forbidValleys` on in the Game grid. Steps up to three-storey walls (1↔3, 2↔3) use the interior stair rooms, and the player stair piece is ground-only, from [SIEGE-27](../history/SIEGE/SIEGE-27.md) (built and verified in the spike).
- Tune fall damage (`FallSettings`) and jump checks for 5m storeys, and the construction camera zoom (the 7.5m spike used 1.5×) and grid lines (`BuildCameraSettings`).
- Regenerate or explicitly retire the 3m prototype scenes (`ConstructionPrototype`, `SiegePrototype`, `WallCommandPrototype`).

Done when the Game builds, plays and saves a new run on the 7.5m grid with placeholder visuals, and EditMode/PlayMode suites pass (Game test fixes are tracked in `QA-castle-kit-tests`).
