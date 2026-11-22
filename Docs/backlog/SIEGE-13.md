---
id: SIEGE-13
title: Synty-Scale Battlements, Sloped Stair Battlements & 6m Main Gate
status: Waiting
assignee: Unassigned
branch:
area: SIEGE
depends_on: [Q-01, ART-06]
created: 2026-11-22
---

# SIEGE-13 - Synty-Scale Battlements, Sloped Stair Battlements & 6m Main Gate

## Scope & Completion Criteria

Registered 2026-11-22 from the user's decisions during ART-06 (split because these change combat and siege rules, not only visuals):

- **Battlements**: use the `PolygonFantasyKingdom` battlement meshes (`SM_Bld_Castle_Battlements_01` family) at natural proportions; the user allows changing the current wall structure to fit. Synty proportions differ from the current rules (sill 0.7m / merlon top 1.38m / merlon ~0.83m on a 5m module with a merlon at the middle, versus `Battlements.asset` 1.1m / 2.2m / 0.75m with a crenel at every edge middle). Affects cover rules (2m units), `BattlementRules` pattern, ladder hook point (`SillHeightAt`), crenel stand points (COMBAT-10), `ShotCover`, NavMesh. Coordinate with the teammate.
- **Sloped stair battlements**: stairs on a wall top currently get no battlements, leaving the outside side open. Add stepped battlements that follow the stair slope, with collision and allied shot cover (user approved, 2026-11-22). No Synty asset exists for this; build it to match the chosen battlement look.
- **Main gate**: keep `SM_Bld_Castle_Wall_Gate_01` proportions by raising the gate to two tiers (6m; the mesh is about 5.7x5.9m and fits a 2-cell x 6m opening) and raising the default walls to two tiers. Affects the gate definition height, door collider and breach logic, default castle layout, ladder lengths, enemy pathing and tests. The user asked to be told before starting if the ripple is large.

Open before start: Q-01 piece-spec decision (cell size, tier height, cover heights), whether default walls become 2 stacked tiers or a taller single piece.
