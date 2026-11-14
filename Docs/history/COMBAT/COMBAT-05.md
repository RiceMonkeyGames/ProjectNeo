# COMBAT-05 | 2026-11-15 | Player Jump

Last Updated: 2026-11-15 01:20:51 (KST)

Branch: `feature/combat-05`. Owner: Hwang Seong-min. Claim synchronized to develop (`docs: claim COMBAT-05`). Design record: [Combat](../../design/Combat.md) (Player jump).

## Decision (user, 2026-11-15)

| Item | Choice |
|---|---|
| Key / height | Space, **1.2m** (cannot reach a 3m wall tier) |
| Air control | Partial: keep 70% of takeoff velocity, blend 30% of input |
| Crouch / sprint | No jump while crouched; sprint jump keeps sprint takeoff speed; crouch ignored until landing |
| Phases | Preparation and battle; unavailable while movement is suspended (ladder, cannon, construction view, falls) |

## Implemented scope

- `PlayerLocomotionRules`: `JumpSpeed(height, gravity)` = √(2gh), `AirHorizontal(takeoff, desired, airControl)`, `CanJump(grounded, crouching, suspended, jumping)`.
- `PlayerControlSettings`: `jumpHeight` 1.2, `airControl` 0.3 (and `Create` parameters). Existing `.asset` files were not edited; the new fields use code defaults.
- `PlayerInputController`: runtime-copy `Jump` action bound to `<Keyboard>/space`; `TryJump()`, `IsJumping`, `Settings`. Airborne horizontal velocity uses `AirHorizontal`; landing or `SetMovementSuspended` ends the jump; ceiling contact stops the rise; crouch input ignored while airborne.
- No enemy, ladder, cannon or scene changes.

## Verification

Compile passed. Full `unity-verify-local.ps1 -Mode all -SkipDotnet` passed EditMode **395/395** (3.27s) and PlayMode **196/196** (115.46s). New cases: EditMode `CharacterTests.JumpRules_SpeedReachesHeight_AirBlendsTakeoffAndInput_AndBlocksInvalidStates`; PlayMode `CombatPrototypePlayTests.Jump_RisesToConfiguredHeight_LandsAndCannotDoubleJump` (peak within 0.1m of 1.2m, lands, no double jump) and `Jump_BlockedWhileCrouchingOrSuspended_KeepsSprintTakeoffWithPartialAirControl`. Dotnet precheck skipped (stale generated `.csproj`).

Physical-key manual play (Space feel, jumping near walls/stairs in `Game.unity`) and standalone build are not claimed as verified.

After the results and manual verification guide were presented, the user authorized completion, archive, and commit/push of the task branch on 2026-11-15; develop merge remains with the user.
