---
id: SIEGE-34
title: Enemy Siege Role Coordination, Focused Wall Breach & Archer Blocked-Target Switch
status: Waiting
assignee: Unassigned
branch:
area: SIEGE
depends_on: [SIEGE-17]
created: 2026-11-23
---

# SIEGE-34 — Enemy Siege Role Coordination, Focused Wall Breach & Archer Blocked-Target Switch

## Scope & Completion Criteria

Enhance enemy siege coordination in [SiegeAttackerBrain](../codemap/WallsAndSiege.md) and [WallBreakerAttack](../codemap/WallsAndSiege.md) after `SIEGE-17`:

- **Archer ladder/machicolation suppression**: Weight defenders near raised ladder upper exits and active machicolation openings higher in `DecideArcher` so enemy archers cover ladder climbers and gate breakers.
- **Archer spatial index & stuck-target switch**: Replace full `CharacterActor.ActiveActors` scans in `DecideArcher` with `CombatSpatialIndex`, and rotate away from a fully occluded nearest defender after sustained failed reposition searches to engage another exposed defender.
- **Focused breach wall strategy**: Add a data-selected `WallTargetStrategy` ScriptableObject that favors damaged or already-engaged outer wall segments so multiple wall breakers open one breach faster.
- **Ladder selection scoring**: Factor `LadderPhase.Up` bonus and upper-exit defender density into `ChooseLadder` so attackers exploit weakly defended ladders.
- Verify with EditMode rule tests and PlayMode siege coordination tests.
