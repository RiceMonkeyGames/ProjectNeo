# BUG-popup-cursor | 2026-11-17 | Flow - Restore Gameplay Cursor Capture

Last Updated: 2026-11-17 16:07:46 (KST)

Owner: 신용준. Branch: `fix/bug-popup-cursor`. Claim synchronized to develop in `1eb698c`.
Status: Done. User authorized completion/archive, implementation commit/push, develop merge and local/remote task-branch cleanup on 2026-11-17.

> Parent: [history/README](../README.md) | Related: [Backlog](../../Backlog.md), [CombatAndSquads](../../codemap/CombatAndSquads.md)

## Problem and Change

- Report: close a building popup in Game and the cursor remains visible; gameplay should capture automatically at startup and when returning from a popup.
- `GamePhaseView` already captures on first-person transitions. `PlayerInputController.Update` could release that capture when it consumed the same Escape after being re-enabled. Its focus callback also released on focus loss without recapturing on return.
- `PlayerInputController` records the capture frame and ignores Escape release during that frame. Subsequent Escape input still releases normally. Focus return captures only while the component is active/enabled, preserving cursor access in facilities, equipment, organization, construction, placement and result screens.
- No scene, prefab, input asset or shared combat-core changes.

## Verification

- Added `GameFacilityPlayTests.CursorCapture_IgnoresTheEscapeThatClosedAPanel` and `CursorCapture_RestoresAfterFocusOnlyWhileGameplayIsEnabled`.
- Both failed before the fix: expected `Locked`, actual `None`. The panel test explicitly executes close before player Update with the same synthetic Escape to cover callback ordering; the focus test invokes the Unity callback directly. These are automated callback/input checks, not physical-key or OS focus tests.
- C# pre-check and Unity compile passed. Final supported filter: `-Mode play -Filter Game -SkipDotnet`; Game PlayMode integration regression passed 83/83 (0 failed/skipped, 41.46 seconds), including both new cursor tests and existing facility, equipment, organization, preparation, title and battle-flow checks. The earlier pre-check ran without `-SkipDotnet`.
- The first combined OR filter selected zero tests; it provides no regression evidence. Verification was restarted with the supported `Game` substring filter.
- Documentation finish check passed: history exists, zero broken links, zero unarchived Done rows, clean `git diff --check`. Physical input and standalone build verification remain pending.

## Manual Feedback Follow-up

- User initially reported a failing Editor manual check, then confirmed that closing the popup with its button locks the cursor correctly. The remaining observed difference is Escape in the Editor, consistent with documented Editor cursor-focus release. Clicking Game after Escape was not separately reported. User subsequently authorized completion and integration; standalone Escape behavior remains unverified.
- Unity documents that pressing Escape in the Editor releases Game-mode cursor focus: [Cursor.lockState](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/cursor/lockstate). This is a possible explanation, not confirmation that the reported failure is Editor-only.
- At investigation time the Editor was stopped (`isPlaying=false`), cursor `None`/visible, application unfocused. This is normal outside Play Mode and provides no evidence about the failed gameplay frame.
- Confirmed manual scope: popup close button in the Editor. Standalone physical input and startup/focus-return behavior remain unverified.

## Manual Verification

1. Open `Assets/_Project/Scenes/Title.unity`, enter Play Mode, select a slot and start a run. Once the Game view has focus, expect a hidden, locked cursor with mouse look available. Repeat by starting directly from `Assets/_Project/Scenes/Game.unity`.
2. Approach the Shop or another facility, look at it and press F. Expect a visible, unlocked cursor usable for popup buttons. Close with Escape, then repeat at least three times; also try the popup close button. Expect hidden, locked cursor and working mouse look immediately after each close.
3. During preparation, open equipment (I), organization (O), construction at its station (F), and placement (P). Verify UI cursor access, then close with Escape and verify capture returns.
4. Switch away from the game and back during first-person preparation/battle: expect automatic capture. Repeat while a popup is open: expect visible UI cursor. In the Editor, Escape can release the Game view focus; click the Game view to restore focus before judging the in-game cursor.
5. Hold B for one second to start battle. Expect capture. On reward/results and after returning to Title, expect visible, unlocked cursor for UI controls.
6. Reset by stopping Play Mode and reopening Title. Use a disposable save slot for run-flow checks; these manual checks can update that slot.

Automated repeat: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter Game` (idle Editor only).
