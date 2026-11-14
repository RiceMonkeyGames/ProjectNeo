# BUG-ladder-fall-clipping | 2026-11-14 | Siege - Ladder Fall Separation & Outward Trajectory

Last Updated: 2026-11-14 16:21:43 (KST)

Owner: youngho. Branch: `fix/bug-ladder-fall-clipping`. Status: Done.

## Summary & Design Decisions

- Preserved existing balance-point dislodging (`ClimberDislodged` at outward vertical), fall-damage start height (`FallDamageRules`), and NavMesh recovery rules.
- Added `LadderFallClearance` (`0.55f`) and `LadderFallOutwardSpeed` (`3.5f`) to `SiegeSettings` (`Assets/_Project/Scripts/Runtime/Siege/SiegeSettings.cs`).
- Added pure separation calculation `SiegeLadder.ComputeFallSeparation` and null-safe `SiegeLadder.TryGetFallMotion` (`Assets/_Project/Scripts/Runtime/Siege/SiegeLadder.cs`), ensuring horizontal clearance along `OutwardWorld` without changing vertical fall height `y`. Guarded `world == null || settings == null || !HasWall` so scene TearDown destruction does not throw `MissingReferenceException`.
- Extended `UnitFallController` (`Assets/_Project/Scripts/Runtime/Walls/UnitFallController.cs`) with `BeginFall(bool resumeAgentAfterLanding, Vector3 fallHorizontalVelocity)` and horizontal obstacle checks (`HasHorizontalObstacle`) during airborne frames.
- Updated both `SiegeAttackerBrain.Drop` and `LadderTraveller.Drop` to apply ladder fall separation and outward horizontal velocity for all ladder occupants (enemies, allies, player).

## Verification

- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode all -Filter Siege`:
  - Compile: `PASSED`
  - EditMode: **21/21 passed** (`4.09s`), including new `SiegeRulesTests.LadderFallSeparation_MovesOnlyInsideClearanceAndPreservesHeight`
  - PlayMode: **26/26 passed** (`16.17s`), including new `SiegePrototypePlayTests.SharedLadder_EnemyFall_ClearsTiltingLadderWithoutPenetration` and existing `SharedLadder_LowEnemyPush_StaysAboveGroundAndLands` / `SharedLadder_EnemyHangsWithTilt_ResumesAfterReturn_AndFallsAtBalance`
- Unverified scope: full non-Siege test suite, manual Editor playtest of this fix, standalone build.

## Manual Verification & Reset

1. Open `Assets/_Project/Scenes/Title.unity`, enter `Assets/_Project/Scenes/Game.unity` (or open `Assets/_Project/Scenes/SiegePrototype.unity`), place a wall at a ladder slot, and start battle (`B` hold 1s in `Game`).
2. Let an enemy raise and climb partway up the ladder, then hold `F` from the wall top until the ladder crosses vertical outward.
3. Expect the falling enemy to separate outward cleanly without passing through the tilting ladder visual, take normal fall damage on landing, and recover navigation.
4. Stop Play without saving temporary scene changes.
