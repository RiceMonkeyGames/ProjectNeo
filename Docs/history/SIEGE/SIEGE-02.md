# SIEGE-02 | 2026-11-13 | Siege - Cannon Firing, HP, Manual Control

Last Updated: 2026-11-13 17:23:49 (KST)

## Scope and implementation

- Branch: `feature/siege-02`; assignee Hwang Seong-min (agent: Claude). Rules from Q-21 and SIEGE-02 scope decisions in [design/WallsAndSiege](../../design/WallsAndSiege.md).
- Step 1 (pure rules): `CannonSettings` (Q-21 temporary values), low-arc ballistics, ±45° arc / 6–60m horizontal range, blast falloff 100%→50% with defense, friendly-fire switch (off), swappable target strategy (`NearestCannonTargetSelector`), seeded scatter (2m auto), reload timer.
- Step 2 (HP): `SiegeWeaponDefinition.MaxHealth` (cannon 150 via Editor API); HP depletion removes only the weapon (`SiegeWeaponRemoval.Destroyed = 2`); support collapse rule unchanged.
- Step 3 (facing/record/repair): R rotates weapons in 90° steps; `MountedWeapon`/`CastleWeaponState` keep rotation and HP; Repair All includes weapons (heaviest damage first).
- Step 4 (auto-fire): `CannonModule` fires only while armed in Battle and unmanned, leading moving NavMesh targets; `CannonShell` explodes once; `GameBattleController` arms/disarms and clears shells at battle end.
- Step 5 (manual): `CannonOperator` on the player — F to man within 3m/60°, view clamped to arc, left click fires at the crosshair ground point without scatter, F/Esc leave (cursor relocked), auto-leave on disarm/destruction/death. Fixed a bug where a destroyed cannon kept the player manned (Unity null comparison); leaving now happens on the weapon `Removed` event.
- Step 6 (enemy strike): rebased onto develop after B-6.2 (wall breaker) landed. In `SiegeAttackerBrain`, non-archer attackers ≥1m above ground with no reachable ally walk to the nearest reachable cannon and strike it like the gate. Flyer targeting left to B-6.3.
- Step 7 (wiring): `CannonSetup` menu created `CannonSettings.asset`, `CannonShell.prefab` (placeholder ball + blast sphere), added `CannonModule` to `Cannon.prefab`, created `CannonOperator.asset`, wired `CannonOperator` on the Game player and cannon hints in `GameFlowHud`. All serialized changes through Editor APIs.

## Verification

- Compile passed; EditMode 303/303; PlayMode 141/141 (`unity-verify-local.ps1 -Mode all`, 2026-11-13 after step 7), including `CannonRulesTests`, `CannonPlayTests`, `CannonAttackPlayTests`, `GameCannonPlayTests` and existing B-6.2 tests.
- Manual playtest in Game scene: user confirmed no issues on 2026-11-13 (placement with R, auto-fire, manning, manual fire, leaving). Standalone build not run.
- Not covered: ConstructionPrototype/SiegePrototype directors do not arm cannons (prototype scenes only mount them); flyer attacks on cannons (B-6.3); friendly-fire structure damage applies to wall pieces only (cannons have no collider).
