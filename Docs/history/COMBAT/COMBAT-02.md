# COMBAT-02 | 2026-11-13 | Characters - Player sprint

Last Updated: 2026-11-13 21:17:31 (KST)

Owner: 신용준. Branch: `feature/combat-01`. Implemented together with [COMBAT-01](COMBAT-01.md) at the user's request; IDs remain separate.

## Approved behavior and implementation

- Hold left/right Shift to move at current MoveSpeed × 1.5. Release to restore ordinary speed. No stamina cost.
- Ctrl overrides Shift: crouched movement is 50%, including when an overhead obstacle defers standing. Movement remains normalized diagonally; character stats are not mutated.
- Sprint clears on movement suspension, disable, death update or focus loss. Existing ladder/cannon movement paths stay in control while suspended.
- Multipliers are configurable in PlayerControlSettings and its Create factory. See COMBAT-01 for shared runtime files.

## Verification

- Unity compile and CombatPrototypePlayTests: 9/9 passed (6.23s), including synthetic keyboard holds/releases, speed ratios and suspension checks.
- Full regression: Unity compile passed; EditMode 315/315 (6.73s), PlayMode 163/163 (87.61s) passed. Editor exited PlayMode successfully. Command: powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode all -SkipDotnet. Physical user play and standalone build: unverified.

## Manual check

1. Open `Assets/_Project/Scenes/Game.unity`, press Play, then click the Game view.
2. Walk with W. Hold Shift: faster movement. Release: normal speed. Repeat diagonally and with right Shift.
3. Add Ctrl while holding Shift: crouch and slow down. Release Ctrl in open space: sprint resumes. Release both: ordinary movement.
4. Repeat during battle (hold B for one second from prep). Stop Play Mode to reset.

Reproduce automation: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter CombatPrototypePlayTests -SkipDotnet`.

- User authorized Done/archive/commit/push after the COMBAT-01 camera smoothing refinement on 2026-11-13. Refined camera user play and standalone build remain unverified; follow-up remains in QA-game-loop.

## Completion handoff

- Done/archive/task-branch commit and push authorized by the user, including the smooth first-person crouch transition. develop merge and remote branch deletion remain user-owned.
- Final code: Unity compile passed; EditMode 315/315 (6.66s), PlayMode 164/164 (86.62s). Command: powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode all -SkipDotnet.
- Manual follow-up: Game scene -> Play -> click Game view -> WASD with Ctrl/Shift; check gradual lowering/raising, reverse Ctrl mid-transition, crouch priority, and blocked standing beneath a ceiling. Stop Play Mode to reset. Refined physical play and standalone build remain unverified, tracked in QA-game-loop.
