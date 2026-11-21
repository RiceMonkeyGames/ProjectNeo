# META-10 | 2026-11-21 | Meta - Save Migration for Renamed or Removed Content

## Scope and decisions

- Branch: `feature/meta-10`; SeongminHwang. Follows the Q-13 decision ([history](../Q/Q-13.md)): a save whose content ids were renamed or removed by an update continues instead of being set aside.
- User decisions (2026-11-21): full-price refund of removed content (items: buy price plus upgrade gold; heroes: hire cost, gear to the stash; soldiers: recruit cost; castle pieces and siege weapons: build cost; meta unlock entries: unlock points); shop and tavern offers restock, perks return their point, equipped skills are only renamed; developer log only; one shared rename asset; a removed player hero is swapped for the default player hero, keeping grade and gear.
- Decisions at start (2026-11-21): removed content has no definition to read a price from, so every save records each bought thing's value (gold and materials) at that save, and the meta save records unlock points spent per opened entry. Saves made before META-10 refund 0; opened entries without a record get the current cost (legacy gacha imports 0). Rules: [PrepEconomyMeta](../../design/PrepEconomyMeta.md), [Decisions-Resolved](../../plan/Decisions-Resolved.md).

## Implementation

- See the META-10 row in the [FlowRunEconomy codemap](../../codemap/FlowRunEconomy.md).
- Rename data: `Core/ContentRenameTable` asset `Data/GameFlow/ContentRenames.asset` (empty for now), linked from `RunContent.asset` and `UnlockCatalog.asset` by `Editor/ContentRenameSetup` (menu `ProjectNeo/Setup/Link Content Rename Table (META-10)`); `RunSaveSetup` and `UnlockCatalogSetup` re-link it after rebuilding their catalogs.
- Run saves: `RunState.Capture` writes `ValueRecord`s (castle values from `GameRunLink.BuildCosts`); `GameRunRestorer.Restore` runs the pure `RunSaveMigration.Migrate` before the precheck. The set-aside rule now applies only to saves that still fail validation (for example damaged counters).
- Meta saves: `UnlockTracks.TryOpen` and `ImportLegacyUnlocks` record spent points; `UnlockTracks.Migrate` runs from `GameFacilities.OnRunReady` and `UnlockScreen.Open` (two-line hook in the UI file).

## Verification

- Automated: compile passed; EditMode 683/683 (new `RunSaveMigrationTests`, 13 tests); PlayMode 299/299 (new `GameRunSavePlayTests` removed-item continue test; the set-aside test now uses negative gold, since a removed item no longer discards the run); `code-rules-check` passed.
- Manual: confirmed by the user (2026-11-22).
- Not verified: standalone build.
