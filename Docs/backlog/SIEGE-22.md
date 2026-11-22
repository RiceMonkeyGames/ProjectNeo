---
id: SIEGE-22
title: Main Gate & Gatehouse on the 5m Grid
status: Waiting for Prerequisite
assignee: youngho
branch:
area: SIEGE
depends_on: [SIEGE-18]
created: 2026-11-23
---

# SIEGE-22 - Main Gate & Gatehouse on the 5m Grid

## Scope & Completion Criteria

Registered 2026-11-23 from the team decision to move the Game to the 5m castle kit ([Decisions-Resolved](../plan/Decisions-Resolved.md)); source: [SIEGE-14](../history/SIEGE/SIEGE-14.md) breakage list item 6.

- The 2x1 placeholder gate of 3m cells becomes a one-cell 5m `Wall_Gate_01` with outer and inner frames and a fixed upper storey (gatehouse), as in the spike. Update the door collider, breach and repair logic, machicolation sizes (`MachicolationSettings` 3m shaft, overhang) and the enemy gate approach.
- Wire the portcullis to the flow: raised during preparation, lowered when battle starts; doors swing open while the gate is held open.

Done when the Game gate opens, breaks, repairs and animates as in the spike, with machicolations sized for 5m and gate tests passing.
