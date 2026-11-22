---
id: SIEGE-15
title: Enemy Assault Transition Thresholds & Archer Positioning Pass
status: Waiting
assignee: Unassigned
branch:
area: SIEGE
depends_on: []
created: 2026-11-22
---

# SIEGE-15 — Enemy Assault Transition Thresholds & Archer Positioning Pass

## Scope & Completion Criteria

Priority P1 task from [Q-60](../history/Q/Q-60.md) (AI improvement pass). Improves enemy siege decision-making in `SiegeAttackerBrain` and `SiegeSettings` ([WallsAndSiege](../design/WallsAndSiege.md), [WallsAndSiege codemap](../codemap/WallsAndSiege.md)).

- **Assault transition thresholds**: Prevent `AnyReachableEnemy` from pulling every enemy off siege duties when a single ally steps outside the walls:
  - Keep enemy `Archer` units in ranged support positioning rather than charging into courtyard melee when a path opens.
  - Switch siege breakers (`GateBreaker`, `WallBreaker`, `Ladder`) to `Assault` only when a castle breach path opens or a hostile enters a local threat/engagement leash (data-configurable in `SiegeSettings`).
- **Archer positioning & reload-crouch tolerance**:
  - Replace the single-axis `OutwardWorld` approach calculation with target-relative wall-normal / radial positioning so enemy archers engage flank walls and protruding bastions (`SIEGE-11`) properly.
  - Prevent premature side-step repositioning when an allied wall-top archer briefly ducks behind a sill during `COMBAT-10` reload crouch.
- **Pure rules & tests**: Add EditMode tests for transition/positioning rules and PlayMode tests covering kiting resistance, flank shooting, and crouched defenders.
