---
id: SIEGE-30
title: Collapse Look for Towers, Bastions & Three-Storey Breaches
status: Waiting for Prerequisite
assignee: youngho
branch:
area: SIEGE
depends_on: [SIEGE-29]
created: 2026-11-23
---

# SIEGE-30 - Collapse Look for Towers, Bastions & Three-Storey Breaches

## Scope & Completion Criteria

Registered 2026-11-23 from the user's spike review ([Decisions-Resolved](../plan/Decisions-Resolved.md)). Rules: Collapse look in [WallsAndSiege](../design/WallsAndSiege.md). Build and verify in `SyntyGridSpike`; it edits the same dressing files as `SIEGE-27` and `SIEGE-29` (`CastleKitRules`, `CastleKitDresser`), so it runs after them.

- Tower and bastion collapse: today a fallen tower or bastion leaves only rubble (`CastleKitDresser.RememberBreach`) and the touched wall faces are re-dressed as plain outer faces (buttresses). Leave rubble plus a low jagged stump (with collider and NavMesh) and torn-off scars on the touched wall faces (broken panels and cross-sections, look only, wall HP unchanged). Building a new tower or bastion there clears the stump and scars.
- Rebuild on ruins: the user could not rebuild a tower on a fallen tower's spot in the spike, although the rubble is look only. Reproduce, find the failing placement check, and fix it so a tower or bastion can be rebuilt there in the spike (Game towers come with `SIEGE-24`).
- Three-storey breaches: today the V breach pieces are stretched to the fallen height (`CastleKitRules.AddBreach`, `scaleY` up to 3). Add a second look (jagged end pieces stacked per storey, V slope only in the top storey) and a spike test switch between the two, so the user can compare and pick; then record the choice in the design doc and remove the other look.

Done when, in the spike scene, a fallen tower and a fallen bastion leave a stump and wall scars, a new tower or bastion can be built on the same spot and clears them, both breach looks can be toggled on 1-, 2- and 3-storey breaches, the user has picked one and the other is removed, and the full EditMode/PlayMode suites stay green.
