---
id: COMBAT-12
title: Ranged Engage Occlusion Fix & Melee Engagement Spread
status: In Progress
assignee: SeongminHwang
branch: feature/combat-12
area: COMBAT
depends_on: []
created: 2026-11-22
---

# COMBAT-12 — Ranged Engage Occlusion Fix & Melee Engagement Spread

## Scope & Completion Criteria

Priority P1 task from [Q-60](../history/Q/Q-60.md) (AI improvement pass). Covers shared combat engagement in `UnitCombatAI` and `CombatPhysics` ([Combat](../design/Combat.md), [CombatAndSquads](../codemap/CombatAndSquads.md)).

- **Living-enemy shot occlusion (Q-60 Option A)**: Keep `CombatPhysics.IsClear` blocking line of sight through another living enemy (matches `CombatProjectile` impact). Fix `UnitCombatAI.UpdateEngage` so a ranged unit whose line of sight is blocked only by another living enemy does not collapse `stoppingDistance` to `0` and rush into melee; prefer switching to the blocking enemy when in range or holding firing range.
- **Melee engagement spread**: Distribute melee engagement approach points around a target (ring/angular slots or crowd fallback) instead of routing every melee unit to the single target center, reducing chokepoint deadlocks at breached gates and narrow wall walkways.
- **Pure rules & tests**: Extract pure slot/occlusion decision helpers with EditMode tests plus PlayMode verification of ranged `Engage` occlusion and multi-unit melee surrounding.

## Decisions (user, 2026-11-22)

Recorded in [Combat](../design/Combat.md) (COMBAT-12 details):
- Ranged shot blocked by a living enemy: switch to the blocker when it is in range and can be shot, otherwise hold the firing distance and wait. Structure blocking keeps the walk-closer behavior.
- Melee: nearest free slot on a ring around the target; off-NavMesh slots skipped; centre fallback when none is usable.
- Ring full: extra melee attackers wait on an outer ring.
- Applies to allies and enemies (every `UnitCombatAI` unit).
