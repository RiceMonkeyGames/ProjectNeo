# FLOW-01 | 2026-11-13 | Flow - Hold to Start Battle at Current Position

Last Updated: 2026-11-13 15:35:52 (KST)

Status: Done. Owner: Codex. Branch: `feature/flow-01`. Claim synchronized to develop in `747d562`. User authorized completion, archive, commit/push and develop merge on 2026-11-13. Physical-key playtest was not separately reported; automatic verification and agent visual inspection are recorded below.

## Approved behavior and implementation

Q-40 follow-through approved by user on 2026-11-13: hold B for 1 second in preparation exploration to start battle without gate return or relocation. Show the key hint and hold percentage in the existing bottom HUD. Release, focus loss, disabled input, or leaving exploration cancels the hold. Construction, facility and equipment screens block starting; closing a modal or entering next preparation with B still held requires a release and fresh press. F/I interactions take priority over a simultaneous B input. Existing battlefield readiness and living-player guards remain in force.

`PreparationInteractionSettings` stores the B InputAction and 1-second duration (factory supports explicit duration). `PreparationInteraction` owns input lifecycle and the unscaled hold timer, then calls `GameBattleController.TryStartBattle`. Its explicit battle reference was added to saved `Assets/_Project/Scenes/Game.unity` through Editor APIs; `GameBattleSetup` also supplies this reference for regenerated scenes. Existing settings assets receive the new field defaults without resetting their previous values. The old `BattleStartStation` marker remains scenery: it is excluded from interaction targeting and cannot instantly start battle via F. No player teleport or camera orientation reset was added.

## Verification

- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode compile`: dotnet precheck and Unity compilation passed.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode all -SkipDotnet`: compile passed; EditMode 216/216 (5.72s); PlayMode 120/120 (67.85s), no failures/skips.
- New `GameFlowPlayTests.BattleHold_CancelsOnReleaseAndStartsOnceAtCurrentWallPose` exercises partial hold/release cancellation, percentage display, wall-top position and camera orientation preservation, one start, active enemies, reward guard and held-B next-stage prevention. `BattleHold_BlocksConstructionFacilitiesEquipmentAndRequiresFreshPress` covers all three modal types, cancellation after a partial hold, fresh press requirement and simultaneous I+B priority. Synthetic Keyboard devices and focus settings are restored by teardown.
- Existing `GameBattlePlayTests` banner regression now verifies that neither targeting nor direct legacy interaction starts battle, while preserving enemy activation/approach checks through the controller API.
- Initial hold test used a 300-frame timeout; it expired before one real second in the fast test runner. Changed only the test wait to a bounded real-time deadline; the final full suite above passed.
- Agent visual check: Game frames advanced from 13782 to 15330, confirming a live player loop. Captured 1280x720 Game view with the two-line Korean hint visible and contained in its background. Artifact outside Assets: `C:/Users/JUNGLE/.codex/visualizations/2026/11/13/01a1d37e-1619-7c71-9fac-70b68a5acd27/flow-01-preparation.png`. Screen layout inspected under the existing uGUI skill conventions.
- Play Mode exited; Run In Background restored to false. No standalone build or user physical-key playtest claimed. Focus-loss cancellation was inspected in code, not separately exercised by a focus-changing automated test.

## User manual verification

1. Open `Assets/_Project/Scenes/Game.unity`, press Play. Walk with WASD, look with mouse, and reach a desired wall-top position via the stairs. Confirm the bottom hint shows `B 길게 누르기 · 전투 시작`.
2. Hold B briefly (less than 1 second): percentage rises. Release: percentage disappears and preparation continues. Press/hold B again for 1 second: battle starts once at the same position and view; enemies activate. Keep B held: no additional battle starts.
3. Stop Play and Play again to reset. At the construction station, use F to open building; B must not start battle. Use Esc to return. Repeat while holding B through closing: release and press again before it can start.
4. During preparation open equipment with I and a facility with F at its entrance; B must not start battle in either screen. Esc closes. Test I+B together: equipment opens and battle does not start.
5. Approach the old red gate banner and press F: it no longer starts battle. Move to another position and hold B to start.
6. Reset by stopping Play Mode and reopening `Assets/_Project/Scenes/Title.unity` (or Game for direct Stage 1 prep). Runtime progress is discarded. Physical interaction feel, all screen resolutions, and standalone builds remain user verification/follow-up scope.

## Develop integration (2026-11-13)

Implementation commit: `dc048b9` on `feature/flow-01`, pushed with user authorization. Integrated onto latest shared develop `21aa87b` (includes ART-01 and ART-02). Game scene merged without conflict; its staged delta against latest develop is only the added battle reference. Four document conflicts resolved by retaining ART completion/shadow documentation and FLOW completion/controls documentation together. User explicitly authorized develop merge and push.

Post-integration `unity-verify.ps1 -Mode all`: DOTNET_PRECHECK=PASSED; Unity compilation readiness timed out while the existing Editor process remained alive but Pipeline was unreachable. No post-integration EditMode/PlayMode verdict was produced; 216/216 and 120/120 above are pre-integration results. No forced Editor termination. A normal `unity open` recognized the existing process without launching a second editor. Windows UI inspection could not proceed because app approval timed out. Document links, staged whitespace and conflict-marker checks passed. Re-run the same wrapper after restoring the Editor/Pipeline connection.
