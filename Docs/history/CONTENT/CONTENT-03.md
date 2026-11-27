# CONTENT-03 | 2026-11-27 | Content - Helmet & Accessory Prototype Equipment Set

> Purpose: Task history. | Parent: [history/README](../README.md) | Related: [PrepEconomyMeta](../../design/PrepEconomyMeta.md), [FlowRunEconomy codemap](../../codemap/FlowRunEconomy.md), [EditorAndTests codemap](../../codemap/EditorAndTests.md), [CONTENT-04](CONTENT-04.md)

Branch: `feature/content-03`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim CONTENT-03`).

## Background

UI-07 added the `Helmet` (`투구`) and `Accessory` (`장신구`) slots, but no item used them. Final production item counts stay with Q-61.

## Decisions (user, 2026-11-27)

1. Accessories: keep both candidates in each tier (Traveler Charm and Iron Ring at tier 1, Marksman Clasp and War Banner Charm at tier 2).
2. Helmet max HP is a flat bonus (+15 / +30), not a percentage.
3. Unlock tracks: tier 1 entries open by default; tier 2 is appended after Steel Sword on both item tracks (Iron Helm 2 -> Marksman Clasp 3 -> War Banner Charm 3), so the existing order and meta saves are unchanged.

## Items

| Id | Display name | Slot | Tier | Modifiers | Price | Recipe |
|---|---|---|---|---|---|---|
| `item_leather_cap` | `가죽 모자` | Helmet | 1 | MaxHealth +15, Defense +1 | 30g | 20g, hide 2 |
| `item_iron_helm` | `철투구` | Helmet | 2 | MaxHealth +30, Defense +3 | 65g | 45g, hide 2, bone 3 |
| `item_traveler_charm` | `여행자의 부적` | Accessory | 1 | MoveSpeed +10% | 30g | 20g, hide 1, feather 1 |
| `item_iron_ring` | `철반지` | Accessory | 1 | Attack +2 | 30g | 20g, bone 2 |
| `item_marksman_clasp` | `명사수의 걸쇠` | Accessory | 2 | AimSpread -1, Attack +2 | 70g | 50g, bone 2, feather 3 |
| `item_war_banner_charm` | `전투 깃발 부적` | Accessory | 2 | Attack +4, Defense +3 | 75g | 55g, hide 3, bone 3 |

Shop weights: Lv1 tier 1 at 1 each; Lv2 tier 1 at 1, Iron Helm 1, the two tier 2 accessories 0.5; Lv3 tier 1 at 0.5, Iron Helm 1.5, tier 2 accessories 1. Victory drop pool: tier 1 at 0.5 each (Iron Sword 1, Potion 2 unchanged). Icons are placeholders from the team packs (Fantasy Warrior helmet/necklace/ring/arrow/flag icons and `ICON_SM_Char_Attach_Helmet_01`).

## Changes

- Editor: new `HelmetAccessorySetup` (menu `ProjectNeo/Setup/Create Helmet And Accessory Set (CONTENT-03)`, idempotent) creates the items and appends missing Shop, Smithy and drop entries, then runs the unlock, icon, upgrade-material and run-save setups. `GameFacilitySetup.Apply` and `UnlockCatalogSetup.Apply` call into it so full rebuilds include the set. `UnlockCatalogSetup` gains the six entries per item track. `EquipmentWindowLayout` maps the slots and icons. `GameFacilitySetup.SetModifiers` is now `internal`.
- Fix: `UnlockCatalogSetup.SaveOver` blanked `m_Name` of overwritten track assets (`EditorUtility.CopySerialized` copies the new instance's empty name); it now names the asset after its file again.
- Runtime: the `I` window equips through new `GameRunLink.TryEquip` / `TryUnequip`, which refill a scene unit that was at full health to its new maximum (a hurt unit keeps its HP).
- Fix found by the new PlayMode test: on a new run, the hero grade bonus (the player is always top grade) raised max HP after health was created, so the first prep started the player at 100 / 130. `GameBattleController` now calls `GameRunLink.RestoreAllHealth` on a new run's first prep right after `SetupHeroPromotion`.
- Assets updated through Editor APIs: six new item assets, `Facility_Shop`, `Facility_Smithy`, `StageRewards`, `RunContent`, the four unlock tracks (re-serialization added the `icon` field to hero and starting-bonus entries; no values changed). `UnlockCatalog.asset` is unchanged.
- Tests: new EditMode `HelmetAccessoryTests` (19). PlayMode `GameEquipmentPlayTests` gains a helmet full-health case. `GameFacilityPlayTests` updated: Smithy Lv1 now lists 6 recipes, and the shop lock test checks the refusal line `잠금 한도: 1개` instead of text that depended on which offer was rolled (this was one of the pre-existing failures).

## Verification

- Code rules check: passed. dotnet build and Unity compile: passed.
- EditMode: 1037/1037 passed.
- PlayMode: 386/395 passed. The 9 failures are the pre-existing ones listed in [CONTENT-04](CONTENT-04.md) (battle HUD overlap, enemy list vs BattleStatus, O-key close, PrepHud x2, first-person camera offset x2, `OneCellGroundStairs(0)/(2)`). The former shop lock text failure now passes. `GameEquipmentPlayTests` 4/4 and `GameFacilityPlayTests` 13/13.
- Play Mode visual capture was not possible because the editor was minimized; the equipment window with the new items was checked through script (worn/storage names, stat lines). Manual play in `Assets/_Project/Scenes/Game.unity` was left to the user.
