---
id: SIEGE-14
title: Spike - 5m Construction Grid with the Kingdom Castle Kit
status: In Progress
assignee: youngho
branch: feature/siege-14
area: SIEGE
depends_on: [Q-01]
created: 2026-11-22
---

# SIEGE-14 - Spike: 5m Construction Grid with the Kingdom Castle Kit

## Scope & Completion Criteria

Registered 2026-11-22 from the user's request: keep the `PolygonFantasyKingdom` demo walls at their own proportions. Analysis of `PolygonFantasyKingdom/Scenes/Demo.unity` (3,601 castle instances) showed the kit is a 5m grid: 5x5m wall panels (`Wall_01/03/04/05`, 0.5m thick) on both faces of a bay, `Pillar_Stone_05` at every 5m joint, `Floor_Wood_01` walkway between the panels, `Battlements_01` (1.38m) and `Battlements_Pillar_02` on top, 5m storeys, `Stairs_Straight_Wide_01` 5x5x5m and `Wall_Gate_01` 5.7m. The Kingdom soldier model is 2.0m tall, the same as our units, while the game grid is 3m cells x 3m tiers (0.6 of the kit).

User decision (2026-11-22): before changing the game grid, build a prototype with a 5m grid and kit-dressed wall bays, look at it in play, and list what breaks; then decide with the teammate.

Spike scope (no change to `Game.unity` or shared rule values):

- A separate prototype scene on a 5m grid (5m wall tiers, 5m stairs) built from the construction prototype, where walls can be placed, stacked and removed.
- A visual-only kit dresser that assembles each wall column from kit pieces according to its neighbours (outer and inner panels, joint pillars, walkway floor, battlements on outward top faces) so placing and removing keeps the pattern continuous.
- First-person walk on the walls with the 2m player to judge scale.
- A breakage list: every system that assumes 3m cells or 3m tiers (default castle layout and map, ladders, cover and battlement heights, siege weapon mounts, machicolations, formations on the walkway, costs, tests), with a rough size for each.

Done when the prototype is playable, captures are reviewed with the user, and the breakage list is recorded here or in a history record for the team decision. `ART-07` (box trim on the 3m grid) is parked in a local stash meanwhile.
