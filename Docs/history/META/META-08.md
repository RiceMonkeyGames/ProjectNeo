# META-08 | 2026-11-20 | Meta - Unlock Currency Earning

Last Updated: 2026-11-20 11:41:20 (KST)

> Purpose: Task history. | Parent: [history/README](../README.md) | Related: [PrepEconomyMeta](../../design/PrepEconomyMeta.md#meta-progression), [META-06](META-06.md), [META-07](META-07.md), [Q-76](../Q/Q-76.md), [FlowRunEconomy codemap](../../codemap/FlowRunEconomy.md)

Branch: `feature/meta-08`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim META-08`, 2026-11-19).

## Background

C-4 added 5 unlock points to the meta progress on every victory. Q-76 instead grants unlock points once when a run ends, from how far the run got (stages reached, bosses killed), with amounts in data proposed at task start.

## Decisions (user, 2026-11-19)

- Amounts: 1 unlock point per stage cleared and 1 per boss killed. A full 7-stage run with 7 bosses gives 14; a defeat on stage 1 gives 0.
- Agent defaults kept: stages cleared = the stage number reached minus 1; the points are added once per run and saved at once; the defeat result screen shows the stages cleared and the points gained with the new total.

## Changes

- `StageRewardTable`: `unlockPoints` (5 per victory) replaced by `unlockPointsPerStageCleared` and `unlockPointsPerBossKilled` (both 1) and `RunEndUnlockPoints(stagesCleared, bossesKilled)`; `Create(...)` takes the two values. `StageRewards.asset` re-saved through Editor APIs with the new fields.
- `StageReward`: `UnlockPoints` / `AddUnlockPoints` removed.
- `GameRewardController`: the victory reward no longer adds unlock points (nor shows them); new `TryGrantRunEnd(int bossesKilled = 0)` on the RunResult phase, `RunEndUnlockPoints`, `RunEndStagesCleared`, `DescribeRunEnd()`.
- `GameFlowHud`: on the RunResult phase the battle status panel shows `DescribeRunEnd()`.
- `Editor/GameRewardSetup` passes the new values.
- Tests: `StageRewardTests.RunEndUnlockPoints_CountStagesClearedAndBossesKilled` (new) and the old 5-point assertion removed; `GameBattlePlayTests` now expects no unlock points after a victory; new PlayMode `GameRunEndPlayTests` (2: two victories then a defeat on stage 3 grant 2 points once with the result text; a defeat on stage 1 grants 0).

## Not in Scope / Follow-ups

- Final end of a run after the last stage: FLOW-03 calls `TryGrantRunEnd` (noted on its Backlog row).
- Boss kills: SIEGE-09 passes the run's boss count to `TryGrantRunEnd` (noted on its Backlog row).
- Existing meta saves keep the points they already earned per victory.

## Verification

- 2026-11-20, `Tools/Agent/unity-verify-local.ps1`: `-Mode all` `DOTNET_PRECHECK=PASSED`, `COMPILE=PASSED`, `EDITMODE=PASSED (Total=510 Passed=510 Failed=0 Skipped=0)`; that run stopped before reporting PlayMode (exit code 4) and the editor restarted. Unity then created `Assets/_Recovery/0.unity` (scene recovery); it is left untouched and not committed. Re-run `-Mode play -SkipDotnet`: `PLAYMODE=PASSED (Total=245 Passed=245 Failed=0 Skipped=0)`.
- Manual check (user, 2026-11-20): the Title and Game scenes display and the run progresses in play. An earlier "no UI, only a flying camera" report was the local verify wrapper leaving the Game view on Play Unfocused (editor setting, fixed locally; not a game change).
- Verified scope: amount rule (EditMode) and Game scene victory/defeat flow with the result text (PlayMode). On-screen display is covered by the manual check. Standalone build not run.
