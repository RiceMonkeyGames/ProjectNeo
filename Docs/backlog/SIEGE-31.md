---
id: SIEGE-31
title: Autonomous Ally Wall-Top Ladder Push & Upper-Exit Chokepoint Defense
status: Waiting
assignee: Unassigned
branch:
area: SIEGE
depends_on: []
created: 2026-11-23
---

# SIEGE-31 — Autonomous Ally Wall-Top Ladder Push & Upper-Exit Chokepoint Defense

## Scope & Completion Criteria

Extend wall-top allied defense in [SiegeLadder](../codemap/WallsAndSiege.md) and [UnitCombatAI](../codemap/CombatAndSquads.md) per [design/WallsAndSiege](../design/WallsAndSiege.md):

- Allow `Hold` melee defenders on a wall top to detect nearby raised enemy ladders within a configurable radius and move to the upper endpoint to push them down cooperatively (up to the 3-pusher cooperation cap from `SIEGE-05`).
- Do not abort ladder pushing solely because an unreachable or distant enemy below the wall enters targeting range.
- When an enemy climber is near the top or blocking pushes, position surplus defenders in a chokepoint arc around the upper exit instead of idling on distant posts.
- Verify with EditMode rule tests and PlayMode wall-top ladder defense tests.
