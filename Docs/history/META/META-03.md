# META-03 | 2026-11-20 | Meta - Remove Reincarnation Bonus

Last Updated: 2026-11-20 11:20:53 (KST)

> Purpose: Task history. | Parent: [history/README](../README.md) | Related: [PrepEconomyMeta](../../design/PrepEconomyMeta.md#meta-progression), [Q-71](../Q/Q-71.md), [FlowRunEconomy codemap](../../codemap/FlowRunEconomy.md)

Branch: `feature/meta-03`. Owner: 신용준. Claim synchronized to develop (`docs: claim META-03`).

## Background

Q-71 (team meeting, 2026-11-19) dropped the reincarnation bonus: the run score is no longer converted to Commander EXP, and Commander EXP comes from stage clears only. The conversion code from the early meta rules was still present, although nothing at runtime called it.

## Changes

- Removed `CommanderProgression.AddRunScoreBonus` (and the now unused `UnityEngine` using).
- Removed `MetaConfig.runScoreToXpRate` / `RunScoreToXpRate` and its inspector header; `MetaConfig.Create` now takes `(baseXpPerLevel, xpGrowthPerLevel, perkPointsPerLevel)`.
- Removed the EditMode test `MetaProgressionTests.AddRunScoreBonus_ConvertsPartOfScoreToXp` and updated the test setup.
- `Data/GameFlow/Rewards/Meta.asset` re-serialized through the Editor (`AssetDatabase.ForceReserializeAssets`). This dropped the stale `runScoreToXpRate` key and two keys left over from the META-05 gacha removal (`defaultUnlockCost`, `unlockCosts`), which no code reads.
- Docs: removed the reincarnation conversion from [Status](../../plan/Status.md), [Goal](../../plan/Goal.md), [Milestones](../../plan/Milestones.md), [FlowRunEconomy codemap](../../codemap/FlowRunEconomy.md) and the `META-NN` domain note in [Collaboration](../../workflow/Collaboration.md). The design rule already stated the decision.

## Meta Save Compatibility

`MetaProgress` (the meta save) never stored anything for the bonus (fields: `commanderLevel`, `commanderXp`, `perkPoints`, `unlockPoints`, `unlockedIds`, `openedEntryIds`), so existing `meta_slotN.json` files load unchanged. No migration needed.

## Verification

- 2026-11-20, `Tools/Agent/unity-verify.ps1 -Mode all -SkipDotnet` (an earlier run gave `DOTNET_PRECHECK=PASSED`): `COMPILE=PASSED`, `EDITMODE=PASSED (Total=508 Passed=508 Failed=0 Skipped=0)`, `PLAYMODE=PASSED (Total=243 Passed=243 Failed=0 Skipped=0)`.
- Verified scope: rules (EditMode) and existing scene integration (PlayMode). Manual check (user, 2026-11-20): Meta.asset Inspector shows only the Commander level values, and an existing meta slot loads with the same level and perk points. Standalone build not run.
