---
id: SIEGE-19
title: Castle Kit Dressing in the Game Scene
status: In Progress
assignee: youngho
branch: feature/siege-19
area: SIEGE
depends_on: [SIEGE-18]
created: 2026-11-23
---

# SIEGE-19 - Castle Kit Dressing in the Game Scene

## Scope & Completion Criteria

Registered 2026-11-23 from the team decision to move the Game to the 5m castle kit ([Decisions-Resolved](../plan/Decisions-Resolved.md)); source: [SIEGE-14](../history/SIEGE/SIEGE-14.md) breakage list items 14, 15 and 18.

- Port `CastleKitDresser` / `CastleKitRules` from `SyntyGridSpike` to `Game.unity` (kit prefabs, URP material copies with normal maps, enclosed set from the default castle), replacing the placeholder wall visuals and the `CastleBattlements` boxes with kit battlements (exact colliders, allied `ShotCover`).
- Add convex corner pieces, and keep breach V ruins, stair-end rubble and rubble-only outwork cells working with Game collapses.
- Draw cost: about 10 kit instances per dressed bay; add static batching or per-cell mesh combining and measure it in the Game.
- Make `SyntyGridSpikeSetup` update its piece definitions in place so running the menu no longer rewrites `RunContent.asset` with new GUIDs.

Done when the Game castle looks like the spike in play, collapses and rebuilds re-dress correctly, and the frame cost is measured and acceptable.
