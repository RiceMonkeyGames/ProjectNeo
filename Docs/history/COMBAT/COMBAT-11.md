# COMBAT-11 | 2026-11-22 | Combat - Bow Pitch Follows the Arrow's Launch Direction

> Parent: [history/README](../README.md) | Design: [Combat](../../design/Combat.md) | Branch: `feature/combat-11` | Owner: youngho

## Report (2026-11-22, user)

Archers looked like they shot straight ahead wherever the enemy was. Measurement showed the bow clips cover the needed range (Hold `_Down` -48.5 degrees, middle -3.8 degrees, `_Up` +41.5 degrees, arrow line from string hand to bow hand). The cause was `CharacterModelAnimator.UpdateAim` setting `AimPitch` from the straight line to the target (linear over 40 degrees), while COMBAT-09 arrows leave along a ballistic launch direction (level target at 16m: bow 0 degrees, arrow about +14.5 degrees).

## Changes

- `CharacterAnimationRules`: `AimPitch` replaced by `PitchDegrees(direction)` and `BowPitchBlend(degrees, down, middle, up)`, which maps each side by its own clip angle (so the middle clip's -3.8 degree offset is compensated).
- `CharacterAnimationProfile`: `aimPitchRange` replaced by measured clip angles `bowPitchDown`/`bowPitchMiddle`/`bowPitchUp` (defaults -48.5/-3.8/41.5; existing assets load the defaults) and `SetBowPitch` (keeps down <= middle <= up).
- `CharacterModelAnimator.UpdateAim`: bow units use `ArrowBallistics.LaunchVelocity(AimPoint -> target AimPoint)` for the pitch; per-shot aim spread is not included.
- `CharacterModelSetup` (`ProjectNeo/Setup/Apply Character Models`): measures the three Hold clip angles per motion set with `MeasureBowPitch`. The setup menu was not re-run in this task.
- Docs: [Combat](../../design/Combat.md) bow pose rule, [CombatAndSquads codemap](../../codemap/CombatAndSquads.md), [EditorAndTests codemap](../../codemap/EditorAndTests.md).

## Verification

- Code rules check passed. Compile passed; EditMode 685/685 (new `PitchDegrees_MeasuresElevationOfDirection`, `BowPitchBlend_MapsEachSideByItsOwnClipAngle`, `BowPitch_FollowsArrowLaunchAngle_NotTheStraightLine`).
- PlayMode 300/300. The first full run had one failure in the unrelated `GameFlowPlayTests.BattleHold_CancelsOnReleaseAndStartsOnceAtCurrentWallPose` (expected < 0.01, was 48.13); it passed alone and in a full re-run, so it looks timing-dependent.
- All four `CharacterAnimationProfile` assets load -48.5/-3.8/41.5 (script eval, no asset changes). The flyer bow model was not measured separately.
- Manual play: confirmed by the user on 2026-11-22.
- Standalone build: not run.
- Shared combat/animation code (`CharacterModelAnimator`, `CharacterAnimationRules`): teammate review before `develop` merge.
