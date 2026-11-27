---
id: SIEGE-39
title: Free-Placed Inner Castle from the Synty Demo
status: Waiting
assignee: Unassigned
branch:
area: SIEGE
depends_on: [SIEGE-40]
created: 2026-11-27
---

# SIEGE-39 - Free-Placed Inner Castle from the Synty Demo

## Scope & Completion Criteria

Registered 2026-11-27 from the basin map decision ([Decisions-Resolved](../plan/Decisions-Resolved.md), [WallsAndSiege](../design/WallsAndSiege.md)).

- Bring the castle from the Synty `PolygonFantasyKingdom` demo scene into the basin as it is (buildings and inner walls), with a repeatable Editor setup script; `Assets/ThirdParty` untouched. Its walls are scenery-level structures, not north wall grid pieces.
- Keep the SIEGE-36 courtyard features that still apply: facility buildings with NPCs and F interaction, citizens and battle hiding, decorations that block or not by size. Re-place facilities into the demo castle.
- Free placement model: positions and rotations instead of cells for anything inside the basin. Decide with the user whether the player can move or add inner castle pieces in this task or later (first version: fixed layout).
- Update NavMesh and player/squad movement inside the castle.

Done when the Game shows the demo castle inside the basin with working facilities, NPCs and pathing, verified in play.
