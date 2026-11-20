# META-02 | 2026-11-20 | Meta - Runtime Perk Injection

Last Updated: 2026-11-20 14:02:55 (KST)

> Purpose: Task history. | Parent: [history/README](../README.md) | Related: [PrepEconomyMeta](../../design/PrepEconomyMeta.md#meta-progression), [FlowRunEconomy codemap](../../codemap/FlowRunEconomy.md), [META-09](../../Backlog.md)

Branch: `feature/meta-02`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim META-02`).

## Background

`PerkDefinition` (unit stat effects, special effects) and `PerkLoadout` (point spending) existed with EditMode tests, and COMBAT-07 let `GameSquadMorale` raise `BattleStart` for a loadout handed to it in tests. Nothing in the game created a loadout, attached perk stats to units, raised the other triggers, or offered Economy-tree effects.

## Decisions (user, 2026-11-20)

- The run's allocation lives in the run data and is kept in the run save (Continue keeps it; a new run starts empty).
- Economy-tree effects are added now: victory gold +%, shop buy price % and preparation hours +.
- Until the perk window (META-09) exists, an editor-only F11 shortcut with sample perk assets is used to try perks in the Game scene.
- Agent defaults kept: perks are refused while a battle is pending (Q-06); a saved perk that no longer exists is skipped instead of discarding the whole run save; restored perks beyond the current points are dropped; a prep-hours perk also extends the preparation already running; shop price changes apply to gold only (materials unchanged), rounded and never below 0.

## Changes

- Meta: `PerkRunEffectKind` (`RewardGoldPercent`, `ShopPricePercent`, `PrepHours`), `PerkRunEffect`; `PerkDefinition.RunEffects` / `RunEffectTotal` and new `Create(...)` parameters (`runEffects`, `displayName`); `PerkLoadout.BonusPoints` (unsaved, tests/dev) and `RunEffectTotal`.
- Run: `RunStatePerks.cs` (`SetupPerks`, `CanAllocatePerk` / `TryAllocatePerk`, `PerkAllocated`, `PerkRunEffect`, `PerkRewardGoldBonus`, save/restore of perk ids); `RunSnapshot.perks`; prep clock budget adds perk hours; `ItemShop.PriceChangePercent` / `GetBuyPrice` used by buying; `IRunContentLookup.Perk(id)` and `RunContentCatalog.Perks` / `SetPerks`.
- Flow: `GameRunLink` creates the loadout from `GameSession.Meta` on the first preparation and `ApplyPerks()` re-attaches stat effects (source = perk, no stacking, full-health units refilled) on preparation, allocation, recruit and hire; `GameBattleController` hands the run's loadout to `GameSquadMorale` each preparation; `GameSquadMorale` raises `AllySquadMemberDied` for squad members and captains; `GameRewardController` adds the victory gold bonus and shows it; the shop window shows and charges the perk price; editor-only `GameDevPerkShortcut` (F11 in preparation/reward).
- Editor/data: `PerkSampleSetup` (`ProjectNeo/Setup/Create Sample Perks (META-02)`) created seven sample perks and a morale effect in `Data/GameFlow/Perks/`; `RunSaveSetup` now fills perks into `RunContent.asset`.
- Tests: new EditMode `RunPerkTests` (8) and PlayMode `GamePerkPlayTests` (1). `GameMoralePlayTests.BattleStartPerk_RaisesEverySquadOnce_SkipsRouted_AndStopsAfterBattle` (COMBAT-07) now allocates its perk in the run's loadout, because the Game scene re-attaches the run's perks to morale every preparation.

## Not in Scope / Follow-ups

- Perk tree data, allocation window and skill perks: META-09 (remove `GameDevPerkShortcut` there).
- `PlayerSkillUsed` trigger: COMBAT-06.

## Verification

- 2026-11-20, `Tools/Agent/unity-verify-local.ps1`: `DOTNET_PRECHECK=PASSED`, `COMPILE=PASSED`; `-Mode all -SkipDotnet` `EDITMODE=PASSED (Total=519 Passed=519 Failed=0 Skipped=0)`; after updating the COMBAT-07 morale test, `-Mode play -SkipDotnet` `PLAYMODE=PASSED (Total=247 Passed=247 Failed=0 Skipped=0)`.
- One compile run reported stale `CS1061` lines from `Logs/Editor.log` after the fix; `EditorUtility.scriptCompilationFailed` was `False` and the next runs compiled.
- Manual check (user, 2026-11-20): in the Game scene F11 allocated the sample perks; prep time grew by 2 hours at once, shop prices dropped 20% and were charged, the victory reward showed the perk gold bonus, and Continue kept the perks.
- Verified scope: rules and save (EditMode) and stat injection in the Game scene (PlayMode). Economy effects in play and the F11 shortcut are covered by the manual check. Standalone build not run.
