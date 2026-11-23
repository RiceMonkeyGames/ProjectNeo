---
id: SIEGE-35
title: 7.5m Footprint Grid Spike (5m Storey Height)
status: In Progress
assignee: youngho
branch: feature/siege-35
area: SIEGE
depends_on: [SIEGE-27]
created: 2026-11-23
---

# SIEGE-35 - 7.5m Footprint Grid Spike (5m Storey Height)

## Scope & Completion Criteria

Registered 2026-11-23 from the user's `SIEGE-27` review. On the 5m grid a storey is as tall as a cell is wide, so a 45° stair needs a whole cell: outside stairs have no floor in front of their foot, stair rooms need narrow turns bridged by NavMesh links, and a stair entered only through a centred room door must be boarded from its side. The user proposes widening the cell footprint (top and bottom faces) to 7.5m × 7.5m while keeping the storey height at 5m. This is a spike to see whether that is worth it before the Game migration (`SIEGE-18`); it decides nothing by itself. Build it in `SyntyGridSpike` (or a copy of it) after `SIEGE-27`, which edits the same dressing files (`CastleKitRules`, `CastleKitDresser`, `SyntyGridSpikeSetup`).

- Separate the horizontal cell size from the storey height in the spike grid and dressing: today the kit pieces are scaled by `cell size ÷ 5m` on every axis, so a 7.5m cell would also make storeys 7.5m tall. Piece height stays 5m.
- Kit pieces are 5m modules, so a 7.5m face needs either a stretch or a composition. Show both side by side behind a spike switch so the user can compare:
  - (a) stretch: wall panels, battlements, doors, arrow slits, gate and floors stretched 1.5× along the face (height and panel thickness unchanged);
  - (b) compose: one full module plus one half module per face (`Wall_Cut_01`, `Battlements_01_Half`, `Floor_Wood_03` half floor), with joint pillars, accent spacing and gate side fillers redone for 2.5m steps.
- Check the stair cases that motivated this with the 7.5m cell: outside straight and corner stairs with floor in front of the foot, stair rooms (`StairWell` with wider turns; whether the turn NavMesh links are still needed), the 2-storey / 1-storey corner / 3-storey corner entered through a room door.
- List what else would change if the Game moved to 7.5m (towers 2×2 = 15m, bastions, ladder spacing, machicolations per gate cell, siege weapon mounts, squad frontage, battlement cover sizes, camera, grid size and castle area, costs), without changing the Game.

Done when the spike scene shows a 7.5m-footprint castle with 5m storeys in both looks (stretch and compose), the motivating stair cases are walkable by the player and an allied unit, the user has chosen keep 5m / move to 7.5m (and which look), the decision is recorded in `Docs/design/WallsAndSiege.md` and `Docs/plan/Decisions-Resolved.md`, `SIEGE-18` is updated to match, and the full EditMode/PlayMode suites stay green.
