# BUG-stage2-enemies-idle | 2026-11-15 | Stage 2 Enemies Do Not Advance

Last Updated: 2026-11-15 01:03:19 (KST)

Branch: `fix/bug-stage2-enemies-idle`. Owner: Hwang Seong-min. Claim synchronized to develop (`docs: claim BUG-stage2-enemies-idle`).

## Symptom

Reported 2026-11-14 after the QA-game-loop recheck: in `Game.unity`, after winning Stage 1 and `Claim Reward & Next Stage`, the Stage 2 battle starts but enemies stand still in the northern field.

## Root cause

Wall and gate damage carry over between stages. When Stage 1 ends with the gate door destroyed (or a wall breach), Stage 2 starts with an open walking path from the field into the castle. `SiegeAttackerBrain.Decide` then sees a reachable ally and switches every enemy to `Assault`, which called `UnitCombatAI.SetDirective(Engage)` **without an advance point**. Plain `Engage` only looks for targets inside `DetectionRange` and otherwise waits in place. Enemies spawn more than 70m away, so no target is ever found and all of them stay with a stopped agent. In Stage 1 the same switch only happens after a breach, when attackers are already near the wall, so the problem was hidden.

Reproduced in PlayMode before the fix: all 12 debug-roster enemies were active, on the NavMesh, decided, and `isStopped=True`; 0 of 12 moved.

## Fix

- `SiegeAttackerBrain.AnyReachableEnemy(out nearest)` returns the straight-line nearest enemy that has a complete path (it only path-checks candidates closer than the current best).
- Entering `Assault` now uses `SetDirective(Engage, nearest position)`. While assaulting with no `UnitCombatAI.Target`, the advance point is refreshed every decision tick, so attackers walk toward the castle and fight whatever enters detection range on the way.
- No change to role-specific siege behavior, settings, assets, or the Game roster.

The touched file is in the enemy-assault domain (teammate area per [Team](../../plan/Team.md)); the user should share this change with the teammate.

## Verification

- New PlayMode regression `GameBattlePlayTests.스테이지_1에서_성문이_부서진_채로_넘어간_스테이지_2에서도_적이_성으로_다가온다`: destroys the gate in Stage 1, wins, continues, starts Stage 2, and requires every enemy to move at least 1m toward the castle within 120 frames. Failed before the fix (0/12 moved), passes after.
- Full `unity-verify-local.ps1 -Mode all -SkipDotnet`: compile passed, EditMode **394/394** (3.33s), PlayMode **194/194** (113.62s). Dotnet precheck skipped (stale Unity-generated `.csproj`), not a separate dotnet claim.
- Manual play in the Editor and standalone build: not claimed as verified by this record.

After the results and manual verification guide were presented, the user authorized completion, archive, and commit/push of the task branch on 2026-11-15; develop merge remains with the user.
