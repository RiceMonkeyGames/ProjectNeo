# BUG-ladder-fall-test-after-gate-arch | 2026-11-14 | Tests - Isolate occupied ladder fall scenario

Last Updated: 2026-11-14 18:25:28 (KST)

Owner: 신용준. Branch: `fix/bug-ladder-fall-test-after-gate-arch`. Claim synchronized to develop in `195df3a`. User authorized completion/archive/commit/push, develop merge and local/remote task-branch deletion on 2026-11-14.

## Diagnosis

The original `SiegePrototypePlayTests.Ladder_AlliesOnTopPushItDown_AndClimberTakesFallDamage` failed on current develop after SIEGE-06, matching the report. Temporary diagnostics showed two topples but **zero `ClimberDislodged` events**. Enemies did climb (maximum observed height 3m), and the gate broke around frame 492–497, but no occupied ladder was toppled. The mixed battle therefore did not establish the precondition for its fall-damage assertion. This evidence does not establish a defect in gate destruction or ladder damage, nor prove that gate timing alone caused the failure.

## Changes

Only `Assets/Tests/PlayMode/SiegePrototypePlayTests.cs` changes:

- `Ladder_AlliesOnTopPushLaddersDown` retains the mixed-roster battle and validates allied automatic ladder toppling independently of whether an enemy boarded.
- The original fall-damage test now isolates one enemy and one defending unit. The enemy uses its real decision/raising/climbing logic; after it boards and climbs at least 1.5m, the test warps the defender to the ladder top. `SiegeLadder.Update` performs the actual automatic push; the test does not tick or topple the ladder state directly.
- Assertions cover live occupancy, climbing height, automatic dislodging, cleared occupancy/on-ladder status, completed toppling and landing, positive fall damage, matching HP loss, and restored NavMeshAgent.
- Temporary diagnostics were removed. Runtime scripts, game rules, settings assets, prefabs and scenes are unchanged. No shared SIEGE-06 owner code was edited.

## Verification

- Before fix: the original focused PlayMode test failed with the reported assertion; diagnostic reruns confirmed no dislodging event.
- After fix: Unity compile passed; focused `-Mode play -Filter Ladder_AlliesOnTop -SkipDotnet` passed **2/2** (2.76s).
- Full PlayMode regression: Unity compile passed; `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -SkipDotnet` passed **185/185** (93.78s).
- Dotnet precheck skipped because generated project files are documented as stale in SIEGE-05. EditMode, physical-input manual play and standalone builds were not run for this test-only change.

## Manual Verification / Reset

1. Open `Assets/_Project/Scenes/SiegePrototype.unity` with Play Mode stopped.
2. Open **Window > General > Test Runner > PlayMode** and filter `Ladder_AlliesOnTop`; run both selected tests.
3. Expected: the mixed battle topples a ladder; the isolated test automatically raises a ladder, boards/climbs an enemy, adds a defender at the top, then observes an actual push, fall damage, landing and navigation recovery. Both tests pass.
4. CLI equivalent from the project root: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter Ladder_AlliesOnTop -SkipDotnet`.
5. Tests unload their additive scene and restore capture delta time/cursor. If interrupted, stop Play Mode and reopen `Assets/_Project/Scenes/SiegePrototype.unity` to reset. No saved scene or data changes are required.
