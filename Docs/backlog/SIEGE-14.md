---
id: SIEGE-14
title: Autonomous Ally Ladder Push & Upper-Exit Chokepoint Defense
status: Waiting
assignee: Unassigned
branch:
area: SIEGE
depends_on: []
created: 2026-11-22
---

# SIEGE-14 — Autonomous Ally Ladder Push & Upper-Exit Chokepoint Defense

## Scope & Completion Criteria

Priority P1 task from [Q-60](../history/Q/Q-60.md) (AI improvement pass); also addresses the upper-exit chokepoint standoff deferred in `SIEGE-05` ([WallsAndSiege](../design/WallsAndSiege.md), [WallsAndSiege codemap](../codemap/WallsAndSiege.md)).

- **Autonomous ladder pushing**: Allow wall-top `Hold` allied units (configurable radius on the same wall level, respecting `HoldMaxDescent` and `FriendlyUse` safety) to actively move to nearby raised enemy ladders and push them down instead of only pushing when already standing within `LadderPushRadius`.
- **Upper-exit chokepoint defense**: When an enemy climber is nearing or stepping over the top sill, position nearby `Hold` melee defenders at the upper exit to intercept without leaving the wall.
- **Preserved rules**: Keep `SIEGE-04` friendly-use topple protection, `SIEGE-05` weight/cooperation timings, and `COMBAT-04` stagger interruption intact.
- **Pure rules & tests**: Add EditMode selection/eligibility tests and PlayMode wall-top ladder defense tests.
