# SIEGE-33 | 2026-11-24 | SIEGE - Cannon Cluster/Threat Targeting, Trajectory Obstacle Check & Flyer Lead Velocity

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

- **Assignee**: `SeongminHwang` | **Branch**: `feature/siege-33` | **Claim commit**: `03f0b5d`
- **Design**: [WallsAndSiege § Cannon mechanics, Auto targeting](../../design/WallsAndSiege.md) (revised 2026-11-24, user decisions: weights ladder/siege breaker 3, ranged 1.5; blocked arc tries the next-best candidate; led point out of arc/range falls back to the current position).

## Changes

- `ICannonTargetSelector.Select` now takes `CannonTargetCandidate` (position, `TargetThreat`, `Excluded`). `NearestCannonTargetSelector` kept and skips excluded candidates.
- New default `ClusterThreatCannonTargetSelector`: per in-arc candidate, sums blast falloff (1 at centre, `EdgeDamageRatio` at edge, 3D distance) × threat weight over all living enemies in the blast radius (excluded and out-of-arc enemies count). Highest score, then nearest horizontal, then earlier index. Built from `CannonSettings` unless `SetSelector` installs a custom one.
- `CannonSettings` new fields (field initializers cover existing assets): `ladderClimberThreatWeight` 3, `siegeBreakerThreatWeight` 3, `rangedThreatWeight` 1.5, `maxTargetAttempts` 3, `trajectoryCheckSegments` 12, `trajectoryImpactTolerance` 1.5m; also in `Create(...)`.
- `CannonModule.TryAutoFire`: candidates tagged with `CharacterActor.Threat`; `TryAim` leads (`CannonLead.Predict` with `MotionVelocity.Of`), revalidates (`CannonLead.TryResolve`), scatters, swivels, solves and checks `IsArcClear` (sampled arc raycasts; units, footing, `ShotCover` and own weapon ignored; hits within the tolerance of the aim count as arrival). On failure the candidate is excluded, the barrel swivel restored and the next candidate tried; the reload is kept if nothing fires.
- `CannonShotFilter` shares the structure pass rule between `CannonShell` and the arc check.
- `IMotionVelocitySource` / `MotionVelocity` (Combat): common velocity source. `FlyerBrain` implements it (flight velocity per frame, 0 when not flying); NavMesh agents fall back to horizontal agent velocity.

## Verification

- `code-rules-check`: 0 violations (the only serialized change in the tree is the unrelated auto-generated TMP fallback font asset, not committed).
- Unity compile passed; dotnet pre-check skipped (stale generated `.csproj` did not list the new files).
- New tests: EditMode `CannonRulesTests` (cluster vs lone enemy, threat weight vs small group, excluded/out-of-arc counted, settings weights, excluded skipped, lead incl. vertical, lead fallback); PlayMode `CannonPlayTests` `AutoFire_PrefersDenseGroupOverNearerLoneEnemy`, `AutoFire_SkipsTargetBehindBlockingObstacle_AndHoldsWhenAllBlocked`, `AutoFire_LeadsVelocitySource_AndFallsBackWhenLeadLeavesRange` (test-only `TestMotionVelocitySource`).
- Full run (2026-11-24): EditMode 914/914; PlayMode 371/375. All cannon and flyer tests pass (34). The 4 failures fail identically on `develop` without these changes; causes and fixes are in `QA-ui07-test-drift` (history record on branch `feature/qa-ui07-test-drift`).
- Manual play: user asked to finish on 2026-11-24 after the manual check guide (`Game.unity` cluster/threat/blocked arc, `FlyerVerification.unity` flyer lead).
- Not verified: standalone build; frame cost of the O(n²) cluster score in very large battles (`LargeBattleVerification`).
