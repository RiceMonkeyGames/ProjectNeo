# FLOW-03 | 2026-11-20 | Flow/Run - Run Ending & Ascension Levels

Last Updated: 2026-11-20 13:57:33 (KST)

Branch: `feature/flow-03`. Owner: 신용준. Claim synchronized to develop (`docs: claim FLOW-03`). Decision: [Q-78](../Q/Q-78.md). Rules: [MoraleAndWave](../../design/MoraleAndWave.md), [PrepEconomyMeta](../../design/PrepEconomyMeta.md#meta-progression).

## Background

- Registered from Q-69/Q-75 (a harder loop inside the same run after the ending). At task start the user separated ending the run from raising difficulty: clearing all stages ends the run normally and unlocks a Slay the Spire-style Ascension level for later runs. Recorded as Q-78 (supersedes Q-69/Q-75; teammate review pending).
- Approved values: 10 stacking rules (A1 enemy HP +15%, A2 starting gold -25%, A3 enemy attack +15%, A4 prep budget -2h, A5 victory gold -20%, A6 enemies per group +20% rounded up, A7 squad starting morale -30, A8 boss HP +30% / attack +20%, A9 repair time x2, A10 enemy move speed +15%), score and unlock point multiplier x(1 + 0.1 x level), only clearing the highest unlocked level opens the next, level picked after `New Game` on the Title screen and kept in the run save.

## Changes

- Pure rules (`Runtime/Run`): `AscensionRuleKind` (explicit values), `AscensionRule`, `AscensionLevel`, `AscensionTable` (ScriptableObject, `Create`, `Clamp`, `LevelAt`, `DescribeUpTo`), `AscensionEffects` (sums levels 1..N; `StartingGold`, `VictoryGold`, `PrepBudgetHours`, `RepairHours`, `StartingMorale`, `EnemyCount`, `EnemyModifiers(isBoss)`, `RewardMultiplier`, `ScaleReward`; rounding guarded against float error). `Runtime/Meta/AscensionProgress` (`HighestUnlocked`, `CanStart`, `RecordClear`); `MetaProgress.ascensionUnlocked` (old saves read 0).
- Run state and save: `RunState.SetupAscension` / `AscensionLevel` / `Ascension` (`RunStateAscension.cs`); `RunSnapshot.ascensionLevel` captured and restored (`RunRestoreOptions.Ascension`, filled by `GameRunRestorer`); `RunConfig.Ascension`; prep clock budget uses the Ascension budget.
- Flow: `GamePhase.Ending = 5`; `RunProgress.AscensionLevel` / `StageCount` / `IsFinalStage`; `GameFlow.StartRun(state, level)`, `ResumeRun(stage, level)`, `SetStageCount`, `FinishRun`; `ContinueToNextStage` is refused on the last stage; `RestartRunAtFirstStage` keeps the level; `GameSession.ContinueRun` passes the saved level.
- Game scene wiring: `GameBattleController` sets the stage count (from `GameBattleSettings.Stages`) and the run's Ascension right after `RunLink.OnPreparation`, scales encounter group counts (`EncounterPlan.WithCounts`), adds enemy stat modifiers when a wave is released and refills HP, and on `Ending` deletes the run save, records the clear and saves meta (`AscensionUnlockedAtEnding`). Other hooks: `GameBuildEconomy` starting gold (unlock-track starting bonus gold is not reduced), `GameRewardController` + `StageReward.ScaleGold` victory gold, `GameSquadMorale.MoraleOf` new squad morale, `GameConstructionWork.OrderRepair` repair hours.
- UI: `GameFlowHud` shows `Finish run` instead of the next-stage button on the last stage reward, `Return to Title` and the ending text (`EndingText`) on `Ending`, and `- Ascension N` in the phase line. `UI/AscensionPicker` window on the Title screen; `TitleScreen.StartGame` opens it when the slot has Ascension 1+ unlocked, otherwise starts right away (`BeginRun(level)`); slot rows and `Continue` show the Ascension.
- Data and scenes via Editor API: `Editor/AscensionSetup.ApplyAll` (menu `ProjectNeo/Setup/FLOW-03 Ascension (table, title picker, finish button)`) created `Data/GameFlow/Ascension.asset`, linked it in `RunStart.asset`, added `AscensionOverlay` to `Title.unity` and `Button_Finish run` to the `GameFlowHUD` panel in `Game.unity`.
- Not applied yet: boss rules (no bosses until SIEGE-09); the score and unlock point multiplier (`AscensionEffects.ScaleReward` is ready for META-04 and META-08).
- Implementation detail beyond the approved spec: the Title picker opens only when there is a choice (Ascension 1+ unlocked); with only the base game `New Game` starts directly as before.

## Verification

- EditMode: 521/521 passed (new `AscensionTests` 10 tests; `RunSaveTests.CaptureThenRestore_KeepsTheAscensionLevel`).
- PlayMode: 245/245 passed (185.7s). New `GameAscensionPlayTests` (Ascension 10 rules on start gold, prep clock, enemy count, released enemy stats and squad morale in the real Game scene; Stage 7 reward -> `Finish run` -> `Ending` unlocks Ascension 1). `GameStageLoopPlayTests` now ends the 7-stage run with `FinishRun` instead of a stage 8.
- Not verified: manual play of the Title picker and ending screen; standalone build.
