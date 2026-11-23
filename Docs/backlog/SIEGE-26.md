---
id: SIEGE-26
title: Enemy AI on Kit Walls, Towers & Blocking Stairs
status: Waiting for Prerequisite
assignee: youngho
branch:
area: SIEGE
depends_on: [SIEGE-24]
created: 2026-11-23
---

# SIEGE-26 - Enemy AI on Kit Walls, Towers & Blocking Stairs

## Scope & Completion Criteria

Registered 2026-11-23 from the team decision to move the Game to the 5m castle kit ([Decisions-Resolved](../plan/Decisions-Resolved.md)); source: [SIEGE-14](../history/SIEGE/SIEGE-14.md) breakage list item 17 (enemy side). Follow-up of [SIEGE-15](../history/SIEGE/SIEGE-15.md), which was merged before the kit decision.

- Enemy archer positioning (the SIEGE-15 wall-normal / radial approach) on 5m kit walls and around 2x2 towers that jut out of the wall line; towers are protruding outworks like bastions.
- Target choice: whether Wall Breakers attack a tower's bottom tier, how enemies treat tower roof occupants and roof cannons, and attacking stairs that stay in a breach and block it (weaker than walls).
- Interior stair rooms at steps up to three-storey walls (`SIEGE-27`, decided 2026-11-23 under Q-59, widened the same day to 2↔3 steps) and tower storey rooms (`SIEGE-29`): they are part of the NavMesh, so enemies who reach the walls walk through them like allies; decide whether enemies treat a room (its doors or the cell) as a target.
- Coordinate with the teammate who owns enemy AI (SIEGE-15, SIEGE-16, SIEGE-17) before starting.

Done when enemies engage kit walls, towers and blocking stairs sensibly in play, with EditMode/PlayMode coverage.
