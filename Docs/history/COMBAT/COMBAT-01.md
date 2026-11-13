# COMBAT-01 | 2026-11-13 | Characters - Player crouch

Last Updated: 2026-11-13 21:17:31 (KST)

Owner: 신용준. Branch: `feature/combat-01`. Implemented together with [COMBAT-02](COMBAT-02.md) at the user's request; IDs remain separate.

## Approved behavior and implementation

- Hold left/right Ctrl to crouch. Current MoveSpeed × 0.5; crouch takes priority over sprint. No stamina cost.
- Capsule height and eye height use a temporary 0.6 ratio in PlayerControlSettings (capsule minimum: diameter). Feet remain fixed; solid overhead clearance is required to stand. Triggers, own colliders and ignored collision pairs do not block standing.
- PlayerInputController uses existing Crouch/Sprint actions on its private runtime input asset copy with Ctrl/Shift bindings; shared input assets remain intact.
- PlayerLocomotionRules resolves speed multipliers. Camera follows actual capsule height ratio. PlaceholderCombatView scales only its Visual child to represent crouching.
- Existing movement suspension (ladders, mounted cannon, falling) still prevents player movement.

## Verification

- Unity compile and CombatPrototypePlayTests: 9/9 passed (6.23s), including 3 new locomotion tests: Ctrl/Shift hold and release on both sides, normalized diagonal speed and stat preservation, capsule feet and restoration, low-ceiling/trigger clearance, camera height and suspension.
- Initial dotnet precheck could not see the new source in its stale generated project; actual Unity import/compile was used with `-SkipDotnet`.
- Full regression: Unity compile passed; EditMode 315/315 (6.73s), PlayMode 163/163 (87.61s) passed. Editor exited PlayMode successfully. Command: powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode all -SkipDotnet. Physical user play and standalone build: unverified.

## Manual check

1. Open `Assets/_Project/Scenes/Game.unity` and press Play. Click the Game view to capture the mouse.
2. Walk with W, hold Ctrl: view lowers smoothly over 0.18s and movement slows; the capsule shrinks immediately. Release Ctrl: normal height/speed in open space.
3. Hold Ctrl and Shift together: remain crouched. Beneath a low solid ceiling, release Ctrl: stay crouched until there is standing clearance.
4. Check the same behavior during battle (hold B for one second during prep to begin). Stop Play Mode to reset; no scene save is needed.

Reproduce automation: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter CombatPrototypePlayTests -SkipDotnet`.

- **User-approved crouch view refinement (2026-11-13)**: First-person eye height transitions with SmoothStep over 0.18 seconds (PlayerControlSettings.CrouchViewTransitionSeconds); capsule changes remain immediate. Reversing Ctrl mid-transition continues from the currently displayed height. Root movement/falls follow immediately; camera re-enable resets the transition. User authorized this refinement and Done/archive/commit/push for COMBAT-01 and COMBAT-02. Physical manual review of the refined transition and standalone build remain unverified.

- Refinement verification: Unity compile and CombatPrototypePlayTests 10/10 passed (6.59s). Added transition midpoint, mid-transition reversal, final crouch/stand height, immediate root following, and camera re-enable reset checks. Final regression: compile passed, EditMode 315/315 (6.66s) and PlayMode 164/164 (86.62s) passed.

## Completion handoff

- Done/archive/task-branch commit and push authorized by the user, including the smooth first-person crouch transition. develop merge and remote branch deletion remain user-owned.
- Final code: Unity compile passed; EditMode 315/315 (6.66s), PlayMode 164/164 (86.62s). Command: powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode all -SkipDotnet.
- Manual follow-up: Game scene -> Play -> click Game view -> WASD with Ctrl/Shift; check gradual lowering/raising, reverse Ctrl mid-transition, crouch priority, and blocked standing beneath a ceiling. Stop Play Mode to reset. Refined physical play and standalone build remain unverified, tracked in QA-game-loop.
