# PREP-03 | 2026-11-14 | Prep - Shop Offer Slot Locks

Last Updated: 2026-11-14 11:01:42 (KST)

> Purpose: Verification & completion log. | Parent: [history/README](../README.md) | Related: [PrepEconomyMeta](../../design/PrepEconomyMeta.md), [FlowRunEconomy codemap](../../codemap/FlowRunEconomy.md)

### 2026-11-14: PREP-03 - Shop Offer Slot Locks

- Assignee: youngho. Branch `feature/prep-03` from develop `8b294e4` (claim pushed first). Decision Q-46 (2026-11-13): free lock toggle per offer; max locked = current Shop level (immediately reflects upgrades); locked offers survive re-rolls and next prep restock; newly rolled offers may duplicate locked items; buying a locked offer removes its lock.
- Rules (pure C#): `ItemShop.Locked`, `LockedCount`, `IsLocked(index)`, `TryToggleLock(index, maxLocked)`, `TryBuyAt(index, wallet, inventory)` (index-accurate purchase so duplicate items remove the clicked slot and its lock). `ItemShop.Restock` keeps locked offers and rolls only `max(0, count - lockedCount)` remaining slots. `RunState.ShopMaxLocked` (`shop.Level + 1`), `CheckShopLock` (`BattlePending -> NotStocked -> InvalidSlot -> LimitReached`, enum `ShopLockCheck`), `TryToggleShopLock(index)`, `TryBuyAt(index)`.
- Scene/UI: `GameFacilities.TryToggleLock`, `TryBuy` updated to `TryBuyAt`, `OfferLockButton(index)` helper, and `AddShopOfferRow` attaching a per-row `Button_Lock` (`잠금`/`해제`) at the right side of each shop offer row while keeping the row click as buy. No `.unity` scene edits needed.
- Automated: Unity compile passed; EditMode `RunShopTests` 20/20 passed (+4 new lock tests); PlayMode `GameFacilityPlayTests` 8/8 passed (new `상점_팝업에서_매물을_잠그면_리롤에도_남고_사면_잠금이_풀린다`). `dotnet build` pre-check skipped (no csproj).
- Not verified yet: manual play in `Game.unity`, full EditMode/PlayMode suite, standalone build.
- User confirmed `Done` and authorized commit, push, and `develop` merge on 2026-11-14.
