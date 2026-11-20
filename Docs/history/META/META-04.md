# META-04 | 2026-11-20 | Meta - Run Score Calculation & Display

Last Updated: 2026-11-21 02:11:18 (KST)

> Purpose: Task history. | Parent: [history/README](../README.md) | Related: [PrepEconomyMeta](../../design/PrepEconomyMeta.md#meta-progression), [META-08](META-08.md), [FlowRunEconomy codemap](../../codemap/FlowRunEconomy.md)

Branch: `feature/meta-04`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim META-04`).

## Background

Q-70 fixed the run score formula and Q-78 (FLOW-03) replaced the post-ending loop multiplier with the Ascension level multiplier. The ending screen (FLOW-03) and the defeat screen (META-08) already showed the run-end unlock points; no score was computed. Open before start: whether a best score is saved.

## Decisions

- User, 2026-11-20: each slot keeps its best run score in the meta save; the result screens show `New best!` or the current best and the Title slot button shows `Best N`.
- Agent defaults kept: stages cleared = battles won; enemies killed count in every battle (retreats do not); "gate intact" = the main gate was not destroyed when the battle ended; living heroes exclude the player; gold scores 1 point per full 10 gold; the score uses the same Ascension multiplier as unlock points and is rounded; the counts are kept in the run save; boss points stay 0 until SIEGE-09.

## Changes

- Run: `StageRewardTable` score values (1,000 / 500 / 10 / 200 / 100 / 10 / 10 gold per point / 5,000); `RunScoreTally` recorded by `RunState.Apply`; `RunState.ScoreTally`, `LivingHeroCount`, `LivingGruntCount` (`RunStateScore.cs`); `ScoreRecord` in `RunSnapshot`; `BattleResult.GateIntact`; pure `RunScore` (`Calculate`, `Parts`, `Breakdown`, `Number`).
- Meta: `MetaProgress.bestRunScore` (older saves read 0).
- Flow/UI: `GameBattleController` passes `!GateDestroyed` to `GameRunLink.TryFinish(..., gateIntact)`; `GameRewardController.TryGrantRunEnd` also settles the score (`LastScore`, `IsNewBest`) and `DescribeRunEnd` adds `Run score N - New best!` / `(best N)` and the breakdown; `TitleScreen` slot text adds `Best N`.
- Data: `StageRewards.asset` re-saved through Editor APIs with the score fields.
- Tests: new EditMode `RunScoreTests` (5); `GameRunEndPlayTests.Defeat_GrantsOnePointPerStageClearedOnce` now also checks the score and the saved best.

## Not in Scope / Follow-ups

- Boss kills: SIEGE-09 calls `RunState.ScoreTally.AddBossKills` (noted on its Backlog row).

## Verification

- 2026-11-20, `Tools/Agent/unity-verify-local.ps1 -Mode all`: `DOTNET_PRECHECK=PASSED`, `COMPILE=PASSED`, `EDITMODE=PASSED (Total=545 Passed=545 Failed=0 Skipped=0)`, `PLAYMODE=PASSED (Total=253 Passed=253 Failed=0 Skipped=0)`.
- Manual check: deferred by the user (2026-11-20) to merge first, then replaced at the user's request (2026-11-21) by PlayMode assertions on the on-screen text.
- 2026-11-21, on-screen text automated (test-only change, no runtime code touched): `GameRunEndPlayTests.Defeat_GrantsOnePointPerStageClearedOnce` reads `GameFlowHud.StatusText` for the defeat score, `New best!` mark and breakdown; new `GameRunEndPlayTests.Defeat_BelowBest_KeepsAndShowsTheSavedBest` checks a lower score keeps the saved best and shows `(best N)`; the `GameAscensionPlayTests` ending test checks the ending HUD score and saved best; new `TitleUnlockPlayTests.SlotText_ShowsTheSavedBestRunScore` checks the Title slot `Best 12,345` (and none when the best is 0). `Tools/Agent/unity-verify-local.ps1 -Mode play -Filter <class>`: `GameRunEndPlayTests` 3/3, `GameAscensionPlayTests` 2/2, `TitleUnlockPlayTests` 3/3 passed. Full suites were not re-run.
- Verified scope: formula, multiplier, tally and run save (EditMode); run-end settlement, best score and the displayed result/Title text in the Game and Title scenes (PlayMode). Visual layout (text overflow/clipping) is not covered by tests. Standalone build not run.
