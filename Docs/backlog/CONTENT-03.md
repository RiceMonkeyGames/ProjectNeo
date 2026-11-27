---
id: CONTENT-03
title: Helmet & Accessory Prototype Equipment Set
status: In Progress
assignee: SeongminHwang
branch: feature/content-03
area: CONTENT
depends_on: []
created: 2026-11-26
---

# CONTENT-03 — Helmet & Accessory Prototype Equipment Set

## Scope & Completion Criteria

Fill the two empty equipment slot kinds introduced by `UI-07` (`Helmet` / `투구` and `Accessory` / `장신구`) so all five character gear slots are usable in preparation, crafting, shopping, and combat ([PrepEconomyMeta](../design/PrepEconomyMeta.md)). Final production item counts remain governed by `Q-61`.

### Planned Design & Scope

1. **Prototype Item Definitions (`ItemDefinition` ScriptableObjects, created via Editor API)**:
   - **Helmets (`Helmet` slot)**:
     - Tier 1 `Leather Cap` (available by default in Shop Lv1 and Blacksmith Lv1): modest max HP + Defense bonus.
     - Tier 2 `Iron Helm` (Shop Lv2 / Blacksmith Lv2, unlockable via `META-07` tracks): higher max HP + Defense bonus, crafted with gold + hide/bone materials.
   - **Accessories (`Accessory` slot)**:
     - Tier 1 `Traveler Boots Charm` or `Iron Ring` (Shop Lv1 / Blacksmith Lv1, available by default): modest MoveSpeed or Attack bonus.
     - Tier 2 `Marksman Clasp` / `War Banner Charm` (Shop Lv2 / Blacksmith Lv2, unlockable via `META-07` tracks): specialized stat modifier (e.g., `AimSpread` reduction or Attack + Defense mix) supporting counter-builds.
   - Populate per-target-level upgrade materials on each new `ItemDefinition` (`PREP-01` contract) so all items can be upgraded at the Blacksmith (+1 / +3 / +5).

2. **Economy, Loot & Meta Integration**:
   - Register new items in `RunContent.asset` (save/restore id lookup, `FLOW-02` / `META-10`).
   - Add to Shop tier pools, Blacksmith level recipe lists, stage victory equipment drop table, and optional follow-up entries in `Data/GameFlow/Unlocks/` (`META-07`).

3. **Equipment UI & Runtime Verification**:
   - Confirm equipping/swapping into the `Helmet` and `Accessory` slots in the `I` window immediately updates player and hero runtime stats and persists across save/load and hero permadeath stash recovery (`PREP-08`).

### Completion Criteria

- EditMode tests verify slot filtering, swap-in-place, stat modifiers, Blacksmith crafting/upgrading, Shop rolling, and save/restore for `Helmet` and `Accessory` items.
- PlayMode verification in `Game` confirms buying/crafting, equipping on player and hero, and battle stat reflection.
- `Docs/design/PrepEconomyMeta.md` and relevant `Docs/codemap/` leaves updated; manual walkthrough provided.
