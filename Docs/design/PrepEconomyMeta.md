# Game Design: Preparation, Economy, Rewards & Meta

Last Updated: 2026-11-13 13:17:37 (KST)

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
