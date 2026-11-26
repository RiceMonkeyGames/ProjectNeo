---
id: SIEGE-22
title: Main Gate & Gatehouse on the 7.5m Grid
status: Waiting
assignee: youngho
branch:
area: SIEGE
depends_on: [SIEGE-18]
created: 2026-11-23
---

# SIEGE-22 - Main Gate & Gatehouse on the 7.5m Grid

## Scope & Completion Criteria

Registered 2026-11-23 from the team decision to move the Game to the 5m castle kit ([Decisions-Resolved](../plan/Decisions-Resolved.md)); source: [SIEGE-14](../history/SIEGE/SIEGE-14.md) breakage list item 6. Rescoped 2026-11-26 (user) to the 7.5m cells with 5m storeys of [SIEGE-35](../history/SIEGE/SIEGE-35.md) and [SIEGE-18](../history/SIEGE/SIEGE-18.md).

Already in place: the Game gate is one 7.5m cell, 5m tall, with a fixed wall on top (two-storey gatehouse) (SIEGE-18); kit dressing, gate-column stair rooms and the portcullis phase link (raised in preparation, lowered at battle start) (SIEGE-19).

- Fit the gate to the kit look on 7.5m cells: the `Wall_Gate_01` frames keep the 5m module, centred, with 1.25m fillers (SIEGE-35). Match the door collider and opening, breach and repair logic to that opening.
- Resize machicolations for 7.5m cells and 5m storeys (`MachicolationSettings` still uses a 3m shaft and a 2.4m overhang).
- Check the enemy gate approach on the 26-cell ring (SIEGE-36), and that the doors swing open while the gate is held open.

Done when the Game gate opens, breaks, repairs and animates as in the spike, with machicolations sized for 7.5m cells and gate tests passing.
