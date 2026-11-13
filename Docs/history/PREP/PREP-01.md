# PREP-01 | 2026-11-13 | Prep - Blacksmith Equipment Upgrade Levels

Last Updated: 2026-11-13 23:28:23 (KST)

> Purpose: Implementation progress and verification record. | Parent: [history/README](../README.md) | Related: [Backlog](../../Backlog.md), [Design](../../design/PrepEconomyMeta.md), [Codemap](../../codemap/FlowRunEconomy.md)

## Ownership and scope

- Owner: 신용준. Branch: `feature/prep-01`. Claim synchronized to develop in `0a29023` before implementation. Status: In Progress; not Done.
- User confirmed that required materials belong on the item data. `ItemDefinition.UpgradeMaterials` holds one entry per target level (+1, +2, etc.). Common modifier bonus, gold ratio and smithy caps live in `EquipmentUpgradeSettings` (defaults 10%, 50%, +1/+3/+5).
- User approved independent implementation first and shared `RunState`/`GameFacilities`/`ItemShop` integration after youngho's PREP-02. These three files are unchanged in this phase.

## Implemented independent rules

- Moved the existing `ItemInstance` type from `Inventory.cs` to its own file without changing its namespace or constructor. Added instance-owned `UpgradeLevel` and `UpgradeGoldSpent` (actual cumulative gold expenditure).
- `ItemUpgradeMaterials` defines the materials for one step; `ItemDefinition.CreateUpgradable` is the test factory. Existing item definitions keep empty upgrade data until the data integration step. An explicitly present empty step requires gold only; a missing step blocks upgrading to avoid treating incomplete data as a free upgrade.
- `EquipmentUpgrade.Check`/`TryUpgrade` operate on a stash item, wallet and zero-based smithy level (`FacilityState.Level`, 0/1/2 = displayed Lv1/2/3). They reject consumables, unowned/equipped items, unsupported/capped levels, missing/invalid costs and insufficient funds. Inventory ownership is required by this independent API; the final UI flow for equipped items is still to be connected.
- Cost = floor(base item gold price × settings ratio × target level) plus the item's material entry. Duplicate materials are summed by the existing Wallet. Failed checks leave item state, gold and materials unchanged.
- `GetEquipModifiers` returns scaled copies of this item's flat/percent modifiers using `1 + level × bonus`; definition data and other item copies stay unchanged. This computes the values; `Character.TryEquip` does not yet consume them.
- `GetSellPrice` calculates floor((base item gold price + actual upgrade gold spend) × EconomyConfig.SellRatio), with no material refund. Existing `ItemShop` still uses the previous sale path until integration.
- Equip/unequip preserves the same instance and its upgrade state. No MonoBehaviour, scene, prefab or existing ScriptableObject asset changes were made.

## Verification

- Unity compile passed.
- Scoped command: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode edit -Filter EquipmentUpgradeTests -SkipDotnet` — 16/16 passed, 4.1s.
- Regression command: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode edit -SkipDotnet` — 343/343 passed, 6.77s.
- Dotnet pre-check skipped because new source files were not yet in the generated project files; Unity compilation and EditMode are the verification evidence.
- Not run: PlayMode, manual Game play, standalone build. This phase has no Game UI or actual combat stat integration to playtest.

## Manual verification for this phase

1. In the Unity project, open Window > General > Test Runner > EditMode and filter `EquipmentUpgradeTests` from `Assets/Tests/EditMode/EquipmentUpgradeTests.cs`.
2. Run Selected. Expect 16 passed cases, including level caps 1/3/5, no partial payments, separate state for duplicate items, equip/unequip retention and actual-spend refund calculation.
3. Tests destroy their temporary definitions/settings in teardown; rerun to reset. They do not load or modify a scene or persistent data asset.
4. CLI alternative: run the scoped wrapper command above from the repository root, with the editor idle and no other test/compile session active.

## Remaining integration after PREP-02

- Bind settings to run services and gate upgrading by preparation phase/battle state.
- Connect upgraded modifiers to equipment stat application/display and verify equip/unequip plus battle/run transitions.
- Connect actual sale payouts and UI quotes to the instance refund helper, preserving PREP-02 re-roll behavior.
- Add Smithy upgrade controls/messages and configure item material steps/settings via Editor APIs. Concrete prototype material quantities are not yet selected.
- Add scoped run/PlayMode tests and Game manual checks for upgrade, insufficient funds, facility caps, stats, sale and next-prep retention.
- Keep In Progress until the integrated result is verified. User requested "마무리해" after reviewing the independent-phase result; this authorizes committing/pushing that verified phase, without marking the unfinished task Done or archiving it.

## Independent-phase handoff (2026-11-13)

- Refreshed origin before handoff: origin/develop `6646500` still lists PREP-02 as In Progress by youngho; no remote PREP-02 task branch was available. Shared integration remains deferred as agreed.
- User authorized wrapping up the presented independent-phase result. Publish the current `feature/prep-01` branch; retain the task as In Progress with the remaining integration scope above.
- Documentation pre-check: history exists, broken links 0, unarchived Done rows 0, Git diff check passed. Changes since the 343/343 verification are documentation only, so tests were not repeated.
- Develop has newer tavern/squad UI changes. No develop merge or rebase was performed in this handoff; regression against that later integrated baseline remains for the integration phase.
