# META-05 | 2026-11-19 | Meta - Unlock Track Rules, Data & Save

Last Updated: 2026-11-19 21:42:48 (KST)

> Purpose: Task history. | Parent: [history/README](../README.md) | Related: [PrepEconomyMeta](../../design/PrepEconomyMeta.md#meta-progression), [Q-76](../Q/Q-76.md), [FlowRunEconomy codemap](../../codemap/FlowRunEconomy.md)

Branch: `feature/meta-05`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim META-05`).

## Background

Q-76 replaced the between-run unlock gacha with Darkest Dungeon 2-style unlock tracks. `UnlockGacha` existed only as a rule with EditMode tests; nothing in the game called it, and the Q-07 pity rule was never implemented. Every hero and item asset currently has `unlockedByDefault` on, so real saves hold no gacha unlocks.

## Decisions (user, 2026-11-19)

- Entries have their own ids, separate from the hero or item they open; the payload is kind + target id + amount. The same item can sit on two tracks, and starting bonuses need no content id.
- Order is an explicit list of prerequisite entry ids per entry. `UnlockTrackDefinition.CreateStraight` chains each entry to the previous one; several prerequisites all have to be opened.
- The gacha's "newly unlocked appears for sure next run" list (`guaranteedNextRunIds`) is removed.

## Changes

- Added (`Runtime/Meta/`): `UnlockTrackCategory` (Heroes, EquipmentRecipes, ShopItems, StartingBonuses), `UnlockPayloadKind`, `UnlockPayload`, `UnlockEntry` (`Create`, `ForContent` copies the target's `UnlockedByDefault` flag into `OpenedByDefault`), `UnlockTrackDefinition` and `UnlockCatalog` ScriptableObjects with `Create(...)` factories, `UnlockCatalog.Validate()`, `UnlockOpenCheck`, and the pure rule `UnlockTracks` (`IsOpened`, `Check`, `TryOpen`, `ImportLegacyUnlocks`).
- `MetaProgress`: added `openedEntryIds`; removed `guaranteedNextRunIds`. `unlockedIds` stays as the legacy content-id list because shop, tavern, crafting and perks still read it through `IsUnlocked` until META-07.
- `MetaSaveStore.CurrentVersion` 1 -> 2. Version 1 files still load (`openedEntryIds` empty, `unlockedIds` kept); the leftover `guaranteedNextRunIds` field is ignored and dropped on the next save.
- Removed `UnlockGacha` (script and `.meta`), `MetaConfig` per-category unlock costs (`UnlockCost`, `defaultUnlockCost`, `GetUnlockCost`) and the `defaultUnlockCost` parameter of `MetaConfig.Create`. The leftover `defaultUnlockCost` value in `Data/GameFlow/Rewards/Meta.asset` is unused and disappears when Unity next saves the asset.
- Tests: new `UnlockTrackTests` (11); removed the four gacha tests from `MetaProgressionTests` and added `LegacyUnlockedHero_StaysUnlocked`; `MetaSaveStoreTests` saves `openedEntryIds` and adds `Version1File_LoadsWithExistingUnlocksAndNoOpenedEntries`; `ItemAndShopTests.Roll_HidesLockedHero_AndPutsGuaranteedOneFirst` no longer uses the removed list.

## Not in Scope / Follow-ups

- No catalog asset or real entries yet: first entries and costs are proposed with META-07, which also calls `ImportLegacyUnlocks` once a catalog exists and switches run content from `unlockedIds` to opened entries.
- Unlock screen: META-06. Earning unlock points at run end: META-08.

## Verification

- 2026-11-19, `Tools/Agent/unity-verify-local.ps1 -SkipDotnet` (the `dotnet build` pre-check was skipped because the Unity-generated project files were stale while the editor was closed): `COMPILE=PASSED`; `-Mode edit` `EDITMODE=PASSED (Total=501 Passed=501 Failed=0 Skipped=0)`; `-Mode play` `PLAYMODE=PASSED (Total=238 Passed=238 Failed=0 Skipped=0)`.
- Verified scope: rules and save format (EditMode) and existing integration (PlayMode). There is no player-facing change to check by playing until META-06/META-07; a manual check covers loading an existing slot.
- Manual check (user, 2026-11-19): an existing pre-META-05 slot loaded on the Title screen with its summary, and the file was rewritten as version 2 with `openedEntryIds` and without `guaranteedNextRunIds`. Standalone build not run.
