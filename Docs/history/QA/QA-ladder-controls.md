# QA-ladder-controls | 2026-11-14 | Siege - Ladder Input and HUD Verification

Last Updated: 2026-11-14 15:51:07 (KST)

Owner: 신용준. Branch: `feature/qa-ladder-controls`. Status: Done. Claim synchronized to develop as `a604a71`. On 2026-11-14 the user confirmed manual verification complete and explicitly authorized completion/archive, task-branch commit/push, develop merge and local/remote task-branch deletion. No game code or serialized assets changed.

## Verified scope

- Command: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode all -Filter Siege`.
- Dotnet precheck and Unity compilation passed. EditMode 20/20 (3.82s), PlayMode 25/25 (14.9s). Includes shared FIFO traffic, allied bidirectional/deferred orders, player synthetic tap/hold/W/S input, friendly queue protection, balance return/commit, occupant follow/fall, low-height landing and controller cleanup. These are automated tests, primarily in SiegePrototype; they do not prove physical-key or Game HUD usability.
- Additional Game-scene runtime probe: started battle, paused enemy interference and allied AI, raised Ladder_26, placed player at its lower endpoint, then used synthetic F/W input. Boarding was true; normal ascent ended at (-40.50, 3.00, 29.00), matching TopPoint. Traversal and IsOnLadder were both false after exit, CharacterController enabled, TryAttack returned true. The initial code-reading concern about exit state was resolved: SiegeLadder.Leave clears it.
- Game HUD displayed the contextual boarding prompt. A short OS-injected F tap did not establish boarding; the computer-use tool cannot specify held-key duration. The user subsequently confirmed manual verification complete on 2026-11-14. No per-case measurements or standalone build verification were reported.

## User manual check

Scene: `Assets/_Project/Scenes/Game.unity`. A temporary Play Mode fixture is prepared at Ladder_26 below the north wall: player at (-40.50, 0, 31.20), facing the wall. Enemies inactive and allied AI paused, battle outcome evaluator paused. Ladder starts Down. This isolates player input; it does not verify full live battle. Click Game view to capture input.

1. Below the ladder, hold F for about 3 seconds. Expect continuous raising and a hold-to-raise prompt. Release: no automatic boarding.
2. Tap F in less than 0.25 seconds. Expect boarding and `W/S · 사다리 오르내리기`. Hold W to climb, release to pause, S to move down. Reach the top with W: expect normal movement/attack restored.
3. At the upper endpoint tap F and hold S. Expect descent and normal movement on landing.
4. Return to the top. Hold F briefly, release before the ladder passes vertical outward: expect smooth return toward the wall, without snapping. Repeat and hold past vertical, then release: expect automatic completion to Down. A full push takes about 2 seconds after the tap threshold. Release must not board.
5. During automatic return/settling, tap F. Expect a motion/wait prompt and no boarding. After motion completes, normal F boarding should work.
6. Check HUD readability and whether prompts correctly distinguish raise, board, W/S traversal, wait and push. Known falling-enemy ladder penetration belongs to BUG-ladder-fall-clipping (youngho).

For the remaining live-battle checks, stop Play Mode and replay Game normally; hold B for 1 second to start battle. Use F1-F3 to select a squad, then 1 (Move) / 2 (Attack) to order an elevated destination requiring a ladder with no complete walking alternative. Expect automatic approach/raising, one occupant at a time, FIFO across both endpoints, and the latest changed order applied after exit. Enemies climbing should pause/follow a partial push, resume after return and fall when the outward balance point is crossed. Synthetic regressions passed above. The user reported manual verification complete without a separate per-case live-battle breakdown.

Reset: stop Play Mode. Temporary positions, paused behaviors and ladder state disappear. No scene save is needed. Reopen Game if necessary. Background execution override is restored before handoff; only the active Game view is required for manual play.
