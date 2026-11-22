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

- **Collapsed wall remains** (added 2026-11-22 by youngho after `BUG-battlement-inner-faces`): when a wall piece collapses in battle the cell is now left clean and empty, and the neighbouring wall ends get no battlements. Place a ruined/broken castle wall asset (Synty `PolygonFantasyKingdom` broken wall or rubble mesh, chosen at task start) in the collapsed cell so the breach reads as destroyed. Visual only unless decided otherwise: it must not block NavMesh or enemy pathing through the breach, must not count as a wall piece for `BattlementRules`, and must be cleared when the cell is rebuilt or the castle is restored from a save (decide whether the rubble itself is saved). Match it to the final wall height from the Q-01 / two-tier decision.

Candidate approach (user idea, 2026-11-22): slice `SM_Bld_Castle_Battlements_01` / `_03` in an editor tool into one repeat module (one merlon + one crenel, plus end caps) and tile the modules along each face at the game's pattern (crenel at every edge middle, half crenels at the ends), so the pattern never breaks across cells. This fixes horizontal alignment without stretching; vertical proportions still depend on the cover-height decision (Synty merlon top 1.38m vs current 2.2m means about 1.6x vertical stretch unless the rule changes).

Open before start: Q-01 piece-spec decision (cell size, tier height, cover heights), whether default walls become 2 stacked tiers or a taller single piece.
