# PREP-02 | 2026-11-13 | Prep - Shop Gold Re-roll

Last Updated: 2026-11-14 00:04:02 (KST)

> Purpose: Verification & completion log. | Parent: [history/README](../README.md) | Related: [PrepEconomyMeta](../../design/PrepEconomyMeta.md), [FlowRunEconomy codemap](../../codemap/FlowRunEconomy.md)

### 2026-11-13: PREP-02 - Shop Gold Re-roll

- Assignee: youngho. Branch `feature/prep-02` from develop `2be8ffb` (claim pushed first). Decision Q-46 plus details confirmed by user on 2026-11-13: cost rises by a fixed step with no cap or count limit; a re-roll refills bought slots too. (Lock details for PREP-03 recorded in the same design entry.)
- Rules (pure C#): `EconomyConfig.rerollBaseCost` / `rerollCostStep` (default 10 / 10) and `GetRerollCost(rerollsDone)`. `RunState.ShopRerolls`, `ShopRerollCost`, `ShopStockedLevel`, `ShopRerollSeedFor(preparation, reroll)`, `CheckShopReroll` (BattlePending → NotStocked → NotEnoughFunds, enum `ShopRerollCheck`), `TryRerollShop(isAvailable)`. `RestockShopIfNeeded` resets the re-roll count each prep. A re-roll draws the full offer count from the level that stocked this prep, so a mid-prep upgrade still shows from the next prep.
- Scene: `GameFacilities.TryReroll` + `Button_Reroll` (bottom-left of `FacilityPanel`, shown only for the Shop, label `리롤 · N골드`). Added to `Game.unity` through Editor API menu `ProjectNeo/Setup/Add Shop Reroll Button To Game Scene` (`GameFacilitySetup.ApplyRerollButton`; adds only the button, scene diff is additions only). `Connect Facilities To Game Scene` also builds it.
- Data: existing `Data/GameFlow/Economy.asset` was not rewritten; it uses the field defaults (10 / 10) until edited in the Inspector.
- Automated: Unity compile passed; EditMode 334/334 (`RunShopTests` +7); PlayMode 169/169 (new `GameFacilityPlayTests.상점_팝업에서_골드를_내고_리롤하며_다른_시설에는_리롤_버튼이_없다`). `dotnet build` pre-check skipped (no csproj).
- Manual play in `Game.unity`: user confirmed on 2026-11-14 (shop re-roll button, rising cost, refill, other facilities hide the button).
- Not verified: standalone build.
- User confirmed `Done` on 2026-11-14.
