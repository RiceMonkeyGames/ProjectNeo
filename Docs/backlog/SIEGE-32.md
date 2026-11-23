---
id: SIEGE-32
title: Flyer Target Lock Cooldown, Ranged LoS Revalidation & Shelter Edge Cases
status: Waiting
assignee: Unassigned
branch:
area: SIEGE
depends_on: []
created: 2026-11-23
---

# SIEGE-32 — Flyer Target Lock Cooldown, Ranged LoS Revalidation & Shelter Edge Cases

## Scope & Completion Criteria

Stabilize [FlyerBrain](../codemap/WallsAndSiege.md) combat and retreat behavior ([design/WallsAndSiege](../design/WallsAndSiege.md)):

- Add target lock cooldown and distance hysteresis in `FlyerBrain.Decide` to prevent mid-air target/hover oscillation between similarly distant wall-top defenders.
- Revalidate `CombatPhysics.HasClearShot` periodically inside `StrikeActor` for ranged flyers and invalidate `hoverTarget` when sightlines become occluded by battlements or pillars.
- Align `CanReachAnyEnemy` with `WeaponTarget` availability when all allies are sheltered under roofs (`IsSheltered`) and review retreat behavior with the user if design ambiguity arises.
- Add visible ballistic/direct projectile parity for ranged flyers striking siege weapons.
- Verify with EditMode selection/hysteresis tests and PlayMode `FlyerSiegePlayTests` extensions.
