# META-07 | 2026-11-19 | Meta - Apply Unlocks to Run Content

Last Updated: 2026-11-19 22:12:53 (KST)

> Purpose: Task history. | Parent: [history/README](../README.md) | Related: [PrepEconomyMeta](../../design/PrepEconomyMeta.md#meta-progression), [META-05](META-05.md), [Q-76](../Q/Q-76.md), [FlowRunEconomy codemap](../../codemap/FlowRunEconomy.md)

Branch: `feature/meta-07`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim META-07`).

## Background

META-05 added the unlock track rules and save, but no track data existed and the shop, tavern and smithy still filtered content with `MetaProgress.IsUnlocked` (the hero/item `unlockedByDefault` flag plus the legacy `unlockedIds`). Every hero and item had the flag on, so nothing was locked in play.

## Decisions (user, 2026-11-19)

- First tracks and costs approved as proposed:
  - Heroes: Swordsman, Archer (default) -> Knight 2 -> Paladin 3.
  - Equipment recipes: Wood Shield, Iron Sword, Leather Armor (default) -> Chain Armor 1 -> Steel Sword 2.
  - Shop items: Potion, Wood Shield, Iron Sword, Leather Armor (default) -> Chain Armor 1 -> Steel Sword 2.
  - Starting bonuses: gold +50 (1) -> potions x2 (2) -> grunts +2 (3) -> gold +100 (5).
- Content that no track entry opens stays available.
- Agent defaults stated in the proposal and kept: the facility level still decides which content a pool offers; opened starting bonuses add up and are granted once to a new run only (not again on Continue).

## Changes

- `UnlockTracks.IsContentAvailable` (no entry for the target -> available; otherwise one opened entry is needed) and `UnlockTracks.StartingBonus` returning the new `UnlockStartingBonus` (gold, grunts, item counts).
- `RunConfig.UnlockCatalog` (+ `WithUnlockCatalog` for tests); `RunStart.asset` now references `Data/GameFlow/Unlocks/UnlockCatalog.asset`, built with four track assets by the new `Editor/UnlockCatalogSetup` (`ProjectNeo/Setup/Create Unlock Tracks (META-07)`; re-running overwrites the track contents and keeps asset references).
- `GameFacilities`: shop restock/reroll, tavern restock and smithy recipes use the catalog when one is set (fallback to `MetaProgress.IsUnlocked` without a catalog). New `OnRunReady(restored)`, called by `GameBattleController` on the first preparation right after `GameBuildEconomy.OnRunReady`: imports legacy `unlockedIds` into opened entries (saving meta if anything changed) and, for a new run, adds starting gold, starting items through `RunLink.Content`, and starting grunts recruited for free at the barracks (first recruitable soldier type).
- Tests: `UnlockTrackTests` +2 (`ContentAvailability_FollowsEntries_AndAllowsContentOffTrack`, `StartingBonus_SumsOnlyOpenedEntries`); new PlayMode `GameUnlockPlayTests` (3: bonuses on a new run, no bonuses by default, RunStart catalog wired and Knight locked).

## Not in Scope / Follow-ups

- Buying entries on screen: META-06 (until then entries can only be opened through code or a meta save edit).
- Unlock point earning at run end: META-08 (the per-victory 5 points from C-4 still apply).
- Hero/item `unlockedByDefault` flags are left as they are; with tracks present they no longer decide availability.

## Verification

- 2026-11-19, `Tools/Agent/unity-verify-local.ps1`: `DOTNET_PRECHECK=PASSED`, `COMPILE=PASSED`; `-Mode all -SkipDotnet` `EDITMODE=PASSED (Total=503 Passed=503 Failed=0 Skipped=0)`, `PLAYMODE=PASSED (Total=241 Passed=241 Failed=0 Skipped=0)`.
- Manual check (user, 2026-11-19): with starting bonus entries opened in the meta save, New Game started with the expected bonuses, and Continue did not grant them again. Standalone build not run.
- Verified scope: rules (EditMode) and Game scene integration for starting bonuses and catalog wiring (PlayMode). Tavern, shop and smithy filtering in play and the Continue path are covered by the manual check below. Standalone build not run.
