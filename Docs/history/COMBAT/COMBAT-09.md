# COMBAT-09: Arcing Arrows, Longer Archer Range & Hit Spread

Last Updated: 2026-11-21 16:19:23 (KST)

Branch: `feature/combat-09`. Owner: youngho. Rules: [Combat](../../design/Combat.md) (Arcing arrows, range and spread). Decision log: [Decisions-Resolved](../../plan/Decisions-Resolved.md).

## Decisions (user, 2026-11-21)

- Range about 1.5×: ranged soldiers (allied and enemy) 11 → 16m, hero archer 12 → 18m, Marksman boss 22 → 30m. High-ground +25% kept.
- Arrows fly a gravity arc: launch speed 18 (unchanged), gravity 9.81, low-arc solution (about 14° at 16m), 45° fallback when out of reach. No target leading.
- Spread is a new unit stat `StatType.AimSpread = 6` (degrees) so items and skills can change it with normal stat modifiers. 3° for ranged soldiers and the hero archer, 1° for the Marksman.
- Straight-line line of sight is kept for target acquisition; no lobbing over walls. Walls and battlements can still stop an arrow on its arc.

## Changes

- Runtime `Combat/ArrowBallistics` (new, pure): low-arc launch velocity with 45° fallback, position/velocity over time.
- Runtime `Combat/AimSpreadRules` (new, pure): uniform point on the disk perpendicular to the shot line, radius `distance × tan(degrees)`, capped at 45°.
- `CombatProfile`: new `projectileGravity` (9.81, `Create(..., projectileGravity)`); existing assets read the default.
- `CombatProjectile`: `Launch(source, attack, launchVelocity)` replaces the straight-line `Launch(source, attack, direction, range)`; follows the arc, sweeps each step, faces its velocity, stops on any non-ignored hit or at lifetime.
- `CharacterActor.ResolveImpact`: spread (`StatType.AimSpread`, static `ShotRandom`) → `ArrowBallistics.LaunchVelocity` → launch. `RangeRules.FlightDistance` removed (no longer needed).
- `BossDefinition.aimSpread` + `WithAimSpread`; `BossRules.StatModifiers` targets `AimSpread`.
- Stat labels: Korean label for `AimSpread` in `GameEquipment`, `GameFacilities`, `PerkWindowModel`.
- Editor: `ArcherRangeMigration` (menu `ProjectNeo/Setup/Apply Archer Range And Spread (COMBAT-09)`, idempotent) updated `allied_ranged`, `hostile_ranged`, `hero_archer`, `boss_marksman` assets (8 values; second run 0). `UnitBattleSetup`, `TavernSetup`, `BossSetup` create the same values for fresh data.

## Verification

- Compile passed. EditMode 659/659 passed (new `ArrowBallisticsTests`, `AimSpreadRulesTests`, `BossRulesTests.StatModifiers_AimSpreadTarget_OnlyWhenSet`; removed `RangeRulesTests` FlightDistance cases).
- PlayMode: new `UnitCombatPlayTests.Arrow_FliesArcAboveStraightLine_AndHits`, `AimSpread_ShotAtCircleEdge_Misses`; updated launch call sites in `FlyerPlayTests`, `GameBattlePlayTests`, `GameStageLoopPlayTests`, `UnitCombatPlayTests`. Full PlayMode run: all passed except one assertion edited by mistake in `GameBossPlayTests` (it builds its own 22m test boss, not the asset); reverted and re-run (2/2), `UnitCombatPlayTests` 19/19.
- Manual play: confirmed by the user on 2026-11-21. During the check the user found that wall-top archers directly above the gate hit enemies breaking the gate through their own wall piece (the shooter's footing is ignored for line of sight and arrows); not part of this task.

## Follow-ups

- Tune values after playtest (spread, speed, Marksman range vs wall-top defenders).
- COMBAT-10 (crouch to reload) builds on this aiming.
