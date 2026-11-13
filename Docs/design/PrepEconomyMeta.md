# Game Design: Preparation, Economy, Rewards & Meta

Last Updated: 2026-11-13 23:28:23 (KST)

> Purpose: Rules for courtyard facilities, unit deployment, currencies, rewards, and meta progression. | Parent: [design/README](README.md) | Related: [WallsAndSiege](WallsAndSiege.md), [OpenQuestions](OpenQuestions.md)

## Preparation Phase & Facilities

- **Default Perspective** (2026-11-11): 1st-person courtyard navigation. Facing a building and pressing `F` opens its modal pop-up (`Esc` or Close exits; movement/look frozen; disabled during combat). Construction station switches to orthographic build mode on `F`, `Esc` returns to 1st-person at matching pose.

| Facility | Function | Upgrade Benefit |
|---|---|---|
| Tavern | Hire randomly appearing heroes | Higher tier heroes appear |
| Shop | Buy/sell items; randomized inventory | Inventory variety expands |
| Barracks | Recruit and train grunts | Grunt stat enhancements |
| Blacksmith | Craft, upgrade, sell hero equipment | Higher tier equipment crafting |
| Siege | Wall/weapon management: add, mount, repair, reinforce | Facility-specific data modules |

- **Refresh & Upgrades** (2026-11-12): Shop and Tavern refresh inventories on entering each prep phase (gold re-rolling is a follow-up). Facility upgrades pay next-level gold; Shop tier reflects on next prep phase (prevents free in-phase re-roll).
- **Shop & Equipment Prototypes** (2026-11-12): Shop rolls from tier pools excluding locked items; purchased items leave stock; sell price = `50%` of purchase gold (data-configurable, applies to combat/reward items too). Press `I` during prep for player equipment UI (3 generic slots, immediate stat updates; hero equipment follows Tavern).
- **Blacksmith** (Q-35, 2026-11-13): *Fixed recipes* — every unlocked equipment recipe allowed by the current blacksmith level (recipe list per level in data; higher levels add higher-tier gear) is always craftable, with no random roll. Cost = Gold + Monster Materials from recipe data. *Upgrades* — each equipment item carries its own level (+1 to +N); each level adds +10% to that item's equip stat modifiers; max level by blacksmith level Lv1 +1 / Lv2 +3 / Lv3 +5; step cost = item gold price × 50% × target level, plus materials (all data-configurable); always succeeds. *Selling* — same rule as Shop (equipment, `50%` of purchase gold); upgraded items also refund the same ratio of gold spent on upgrades.
- **Upgrade material ownership** (PREP-01, user decision 2026-11-13): Store required materials per target upgrade level on `ItemDefinition`, alongside the item's other data. Common gold ratio, modifier bonus and smithy level limits belong to shared upgrade settings. Implement independent upgrade rules first; defer shared run/shop/facility UI integration until PREP-02 is ready, as agreed by the user.
- **Shop Re-roll & Locks** (Q-46, 2026-11-13): Pay gold to re-roll the current offers; cost rises with each re-roll in one prep phase (e.g., 10g, 20g, 30g; data) and resets each prep phase. Locking is free; max locked offers = Shop level. Locked offers survive re-rolls and the next prep restock; buying a locked offer removes its lock.
- **Consumables** (2026-11-13): Combat use of consumables (e.g., potions) is deferred; keys and effects to be planned with Q-12.
- **Barracks** (Q-37, 2026-11-13): Training is automatic by barracks level (no separate paid training action), granting stat bonuses to all grunts immediately on upgrade (Lv1 none, Lv2 +10% max HP/attack, Lv3 +20% max HP/attack). Melee (15g) and ranged (20g) grunts recruitable from Lv1 (data asset values).
- **Unit Deployment** (Q-36, 2026-11-13):
  - *Barracks recruits*: Pick target squad in UI (default: first with free space). If all full, auto-creates a new squad up to cap, then blocks.
  - *Tavern heroes*: Pick squad to captain; swaps existing captain to reserve (excluded from battle until D-8 reorganization).
  - *Scene spawn*: Recruited/hired units spawn in front of their facility during current prep and walk to their squad.

## Currencies, Items & Rewards

- **Currencies**: `Gold` (primary), `Monster Materials` (secondary, multiple sub-types dropped by defeated monsters = "Material Items"), `Items` (equipment and consumables; equipment shared interchangeably between player and heroes).
- **Dual-slot Costs**: All costs hold Gold + Materials (unused slots = 0).
- **Victory Rewards** (2026-11-12): Randomized Gold (base + stage scaling), Monster Materials & bounty gold per kill, drop-chance Finished Equipment, Commander EXP, Unlock Points. Granted once per victory. Prisoner rescue rewards deferred until recruitment is online; Unlock Point spending policy (Q-07) pending.

## Meta Progression

Two elements survive run failure: **Perk Point Count** (spent allocations reset on death; pool persists for re-allocation) and **Unlocks** (heroes/items persist even if hero dies).

- **Commander Level & Perks**: EXP gained from stage clears and Reincarnation Bonus (% of run score on run end). Perks allocatable anytime during a run (mid-combat allocation undecided). Categories: `Combat` (player), `Economy` (gold/items/prep), `Unit Enhancement` (squads/heroes), including stat buffs and special traits (e.g., morale boost).
- **Unlocks**: Spend unlock points for randomized gacha unlocks with duplicate protection (guaranteed initial appearances supported).
- **Balance Risks**: Mitigate pool dilution via tavern re-rolls/locked slots; mitigate power creep via point caps and trait-oriented perks.
