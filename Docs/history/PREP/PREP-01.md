# PREP-01 | 2026-11-13 | Prep - Blacksmith Equipment Upgrade Levels

Last Updated: 2026-11-14 14:06:56 (KST)

> Purpose: Implementation progress and verification record. | Parent: [history/README](../README.md) | Related: [Backlog](../../Backlog.md), [Design](../../design/PrepEconomyMeta.md), [Codemap](../../codemap/FlowRunEconomy.md)

## Current integrated result (2026-11-14)

- Owner: 신용준. Status: Done on user authorization (2026-11-14). User explicitly requested completion, develop merge, and local/remote task-branch deletion after presentation of the integrated result. Physical-input upgrade play and standalone build remain unverified; no separate manual confirmation was claimed.
- Resumed after PREP-03 completed. Brought origin/develop `51de986` into `feature/prep-01` using a no-commit merge. Documentation conflicts were resolved while retaining the latest teammate records and PREP-01 history. No serialized scene/prefab merge conflicts occurred. The user approved committing/pushing the reviewed merge and implementation together.
- User decisions: per-target-level upgrade materials = crafting materials × target level, stored on ItemDefinition; equipped items must be unequipped into the stash before upgrading. Recorded in design and resolved decisions.
- `RunConfig.EquipmentUpgrades` and `WithEquipmentUpgrades` bind the settings; `RunState.SetupEquipmentUpgrades`, `CheckUpgradeEquipment`, `TryUpgradeEquipment`, `MaxEquipmentUpgrade` expose run rules. Pending battles reject upgrades. The Game facility UI additionally requires Preparation phase and the Smithy popup.
- `ItemInstance` retains the data settings used for its upgrades and exposes effective `EquipModifiers`. `Character.TryEquip` always reads those modifiers under the same item source, so direct Character calls and RunState calls both apply upgrades without callers passing a service. Equip/unequip and subsequent battles keep the same item instance. `GameEquipment` displays the upgrade suffix and effective modifier values.
- `ItemShop.GetInstanceSellPrice` and actual `TrySell` payout include actual upgrade gold expenditure. Both Shop and Smithy labels/messages use this value; materials are not refunded. PREP-02 re-roll and PREP-03 locks remain intact.
- The existing Smithy inventory row now upgrades on its main area and sells through its separate right-side button. Shows current/target level, gold/material requirements and the cap; a capped row still allows selling. Two-line row labels stay within the existing row size. Shortened the Smithy body text after finding a heading overlap during visual QA.
- Editor-only `EquipmentUpgradeSetup.Apply` / menu `ProjectNeo/Setup/Configure Equipment Upgrades` creates `Data/GameFlow/EquipmentUpgrades.asset`, binds `Build/RunStart.asset` and fills empty upgrade-material data on the five Smithy equipment definitions in `Data/GameFlow/Rewards/`. Settings default to +10%, 50%, caps +1/+3/+5. Each item gets five target-level entries; explicit empty material steps are gold-only (wood shield). Existing configured lists and settings are preserved on rerun. `GameFacilitySetup.Apply` also calls this setup.
- Asset edits used Editor APIs exclusively. No additional Game scene/prefab changes beyond the incoming develop merge were needed. Regeneration audit returned unchanged hashes for all 8 relevant assets.

## Integrated verification

- Final review found and fixed a direct Character.TryEquip path that would otherwise miss upgrade scaling when no service was passed. EquipmentUpgradeTests now asserts direct equip, unequip, further upgrade and re-equip stats; final full regression after this change passed: compile, EditMode 380/380 (7.13s), PlayMode 182/182 (94.29s).

- Scoped run rules: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode edit -Filter RunUpgradeTests -SkipDotnet` — compile passed; 6/6 passed (3.95s).
- Scoped Game UI: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter GameFacilityPlayTests -SkipDotnet` — compile passed; 10/10 passed (4.45s). Includes two new upgrade/equip/refund/material cases plus existing craft/sale, facility upgrade, re-roll and lock tests.
- Full regression before the final direct-Character path review: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode all -SkipDotnet` — compile passed; EditMode 380/380 (1.11s), PlayMode 182/182 (95.21s), no failed/skipped cases reported. Dotnet precheck skipped; Unity compilation and test results are the evidence.
- Editor-API-driven visual QA: Game frames advanced (4525 → 21136 in final session); Smithy was opened, wood shield crafted/upgraded, facility raised and steel sword upgraded to +4. The next +5 quote showed `200골드 · 뼈 조각 20 · 깃털 10` with TMP overflow false. Captured the final layout at 1280×720 and confirmed the body/header no longer overlap. Exited Play Mode and restored previous background execution setting (false).
- Not verified: human physical mouse/key play, standalone build. Automated callbacks and API-driven Editor observation are distinguished from those checks.

- Final documentation checks passed: history exists, broken links 0, no unarchived Done rows, working-tree diff check and code/docs diff check against origin/develop passed; no unresolved merge entries. Incoming develop serialized whitespace was left unchanged; no manual edits to generated metadata/scenes.

## Manual Game verification

1. Open `Assets/_Project/Scenes/Game.unity`, enter Play Mode, approach the Smithy and press F. Start with 100 gold, Smithy Lv1.
2. Click the wood-shield recipe on the left: gold 100 → 80. Click the shield's main row on the right: +1, gold 80 → 68. The row now shows max +1; trying another upgrade must not charge. The separate Sell button remains usable.
3. Esc, then I: equip the +1 shield. Its item modifier is 110% of the original; displayed stats and scene character stats should agree. Unequip and re-equip: the +1 level and stats must remain, without stacking twice. Unequip it back into the stash.
4. Esc, reopen the Smithy with F, sell through the right-side button: refund is 18 gold (floor((25 base price + 12 actual upgrade spend) × 0.5)), gold 68 → 86. The item disappears. Shop sale uses the same quote/payout.
5. With enough earned resources, upgrade the Smithy: its current cap must become +3, then +5. Iron-sword material steps are hide 2/4/6/8/10; insufficient materials must not charge gold or advance the level. Equipped items are absent from the Smithy list until unequipped.
6. For state carry-over, equip upgraded gear, complete a battle and enter the next prep: level/stat effect remain. RunUpgradeTests checks this data lifecycle automatically; normal Game battle play still needs human confirmation.
7. Reset by exiting Play Mode and reopening Game; temporary gold/items/upgrades are discarded. If tests were interrupted, exit Play Mode before reopening the scene.

The sections below retain the earlier independent-phase evidence; their pending-integration notes describe that historical phase and are superseded by the integrated result above.

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

## Historical pending scope after the independent phase

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
