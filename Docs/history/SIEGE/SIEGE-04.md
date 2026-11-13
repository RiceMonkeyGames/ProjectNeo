# SIEGE-04 | 2026-11-13 | Siege - Shared Ladder Traversal

Last Updated: 2026-11-13 17:33:45 (KST)

Branch: `feature/siege-04`. Owner: Codex. Prerequisite: [Q-41](../Q/Q-41.md). User confirmed shared-code edit availability and selected tap F boarding / hold F toppling. Claim only was synchronized to develop as `369a27d`. After presentation of implementation, test results and manual verification instructions, the user authorized completion/commit/push on 2026-11-13. Q-41 and SIEGE-04 are archived as Done on this work branch; develop merge remains with the user. This authorization does not imply physical-key/HUD manual verification was performed.

## Implemented scope

- `LadderTraffic<T>`: pure FIFO queue with one occupant, shared across teams and directions; repeated requests do not reorder and cancellation releases requests/occupancy.
- `SiegeLadder`: enemy API compatibility, actor queue/occupancy, allied raising, dead/absent request cleanup and friendly-use topple protection.
- `LadderTraveller`: runtime participant component, allied ordered route selection when no full walking path exists, automatic lower-end raising, bidirectional movement, deferred latest order at endpoint, interruption/death/collapse fall handling and controller restoration.
- Player tap F at either end boards, W/S traverses; hold F at lower end raises, hold F at top topples. Long-hold release does not board. Temporary tap threshold 0.25s lives in `SiegeSettings`; existing raise/climb/topple timings remain in that data asset.
- `CharacterActor.IsOnLadder` cancels pending impacts and rejects attacks; damage and death still work. `UnitCombatAI`/`PlayerInputController` delegate traversal and clean up when disabled. Existing HUDs display contextual hints.
- No hand-edited scene, prefab, ScriptableObject or meta files; Unity generated meta files for new scripts. No asset migration is required because participants are attached at runtime and the new settings field has its data default.

## Verification

Unity compile passed. Scoped `-Mode all -Filter Siege -SkipDotnet` passed EditMode 15/15 and PlayMode 20/20 (12.4s), including five new shared-ladder cases and existing enemy siege behavior. After adding cancellation/death coverage and controller cleanup, full `-Mode all -SkipDotnet` passed EditMode **252/252** (5.88s) and PlayMode **132/132** (79.2s).

Final review added Unity destroyed-object occupancy cleanup (`ReferenceEquals` distinguishes an actual null from Unity's destroyed-object null) and extended the mixed-queue test to destroy the occupant and admit the next participant. Final compile and `-Mode all -Filter SharedLadder -SkipDotnet` passed PlayMode **6/6** (4.11s); EditMode had 0 matching cases in this last filtered run. The full suite predates only that final cleanup. The two pure LadderTraffic tests passed as part of the full EditMode suite. Dotnet precheck was skipped because newly created scripts were verified directly by Unity; this is not a separate dotnet verification claim.

The initial scoped attempt found a Unity constructor restriction on `NavMeshPath`; creation was moved to Awake, after which all four initial new PlayMode cases passed. No unresolved failure from that attempt remains.

Physical-key manual play, visual HUD inspection and standalone build are not claimed as verified.

## Manual verification

1. Open `Assets/_Project/Scenes/Title.unity`, start Game (`Assets/_Project/Scenes/Game.unity`), prepare a wall at a ladder slot and hold B for one second to start battle. For isolated checks use `Assets/_Project/Scenes/SiegePrototype.unity`, close wall gaps and start battle via its construction HUD.
2. At the lower end hold F until raised (3s). Release; expect no boarding from this long press. Tap F, use W to ascend, release W to pause, S to descend. From the upper endpoint tap F and use S to descend; normal walking resumes at either endpoint.
3. Select a squad with the current squad hotkeys and issue a move/attack destination across an elevation requiring the ladder, with no complete walking alternative. Expect automatic approach, raising if needed, FIFO use and continuation toward the destination. Change its order while aboard; expect the new order only after endpoint arrival.
4. Queue units and player from both ends in a known arrival order. Expect one occupant, no player priority, and no automatic toppling while a friendly occupant/request exists. Move a waiting player away or disable/remove a waiting unit; expect its request to clear.
5. With no friendly use and no nearby hostile blocking the upper defender, hold F at the top for the 2s push. Expect toppling and no boarding when F is released. Repeat with an enemy aboard; expect its fall and existing fall damage.
6. While an ally/player is aboard, attempt an attack and receive enemy damage. Expect no occupant attack but normal incoming damage. For reproducible destructive checks, open Window > General > Test Runner > PlayMode and run `SiegePrototypePlayTests.SharedLadder_CollapseReleasesOccupancy_AndRestoresNavigation` and `SharedLadder_DeathAndDisabledControllerReleaseOccupancy`. Expect falling, no stuck ladder occupancy, and surviving AI/player movement restoration after landing/navigation rebuild. These tests load and unload SiegePrototype automatically.
7. Stop Play Mode and reopen Title (or SiegePrototype for isolated tests) to reset. Do not save temporary test/play changes to scene/assets.

CLI regression: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode all -SkipDotnet`. Scoped repeat: `-Mode play -Filter SharedLadder -SkipDotnet`; pure FIFO cases: `-Mode edit -Filter LadderTrafficTests -SkipDotnet`. Run sequentially in an idle editor.
