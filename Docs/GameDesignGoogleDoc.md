# Siege Defense Roguelike Game Design Document (Google Docs Mirror)

Last Updated: 2026-11-12 21:51:07 (KST)
Source: [Google Docs](https://docs.google.com/document/d/1zyEvUQFRwFjPP0i5M34nC7sStyZT3s60drXyg_ctz_Y/edit?tab=t.0#heading=h.c9sft4i3oi7)
Original Author: Shin Yong-jun (As of 2026-11-11)

> Note: The Development Backlog table from the top of the Google Doc is maintained in [Docs/Backlog.md](Backlog.md).


### Overview

A siege defense roguelike where players prepare heroes, minions, weapons, and castle walls during the Preparation phase, and defend against enemy waves in the Battle phase. Stages are fixed, while heroes, rewards, and shop inventories are randomized per run.

The core loop is: Preparation → Battle (after wave preview) → Random Rewards → Next Stage Preparation (wall HP carries over).
- Victory in battle → Random rewards → Next stage (persisting wall HP) → Preparation.
- Defeat in battle → Run terminates (perk allocations reset) → Meta progression (unlocks persist via unlock points) → New run.

### Run Structure

Mastery is cultivated through fixed stages and wave memorization, while strategic variety across runs stems from randomized heroes, rewards, and shop inventories. Run length, ending conditions, stage counts, and boss encounters will be determined as content expands.

| Element | Fixed / Random | Notes |
|---|---|---|
| Stage (Wave composition) | Fixed | Mastery through memorization and counterplay |
| Tavern Heroes | Random | Primary driver of build variety across runs |
| Battle Rewards | Random | Gold, materials, finished equipment, rescued prisoners, EXP, unlock points |
| Shop Inventory | Random | Randomized to prevent static optimal builds |

### Wave Preview

- Displays upcoming enemy types and headcounts by default.
- Certain enemies are obscured as hidden ("?"), revealed upon appearing on the battlefield or when scouted.
- High reconnaissance traits (e.g. archer heroes) or scouting perks identify hidden enemies prior to arrival. Without scouting, they are encountered without prior warning.

### Win / Loss Conditions

| Result | Conditions |
|---|---|
| Victory | All enemies killed or routed |
| Defeat | Castle wall HP depleted AND all defenders killed (or routed), OR Player character dies |

*Combat continues even if castle walls collapse, provided surviving defenders remain.*

### Castle Wall System

- Wall damage sustained during battle carries over to subsequent battles.
- Additional wall pieces can be placed during preparation.
- Mounted structures (cannons, artifacts, siege weapons) can be installed on walls.
- Walls can be repaired, reinforced, and modified. Repair costs initially require gold only.
- Construction completes instantly currently. Delayed construction duration will be supported in the future (e.g. completing after multiple battles; unfinished walls enter combat with partial HP).

---

## Detailed Mechanics by System

Enemies pose tactical challenges via traits; players craft counter-strategies during preparation using gold and monster materials.

### Combat

Direct player combat combined with squad commanding (Mount & Blade Bannerlord style).
- Player organizes squads and designates heroes as squad captains. No separate commands are given directly to heroes during siege combat.
- Orders are issued at the squad level: Move, Attack, Hold. Squads without captains receive the same orders.
- Squad count and member caps are data-driven. Default squad size is 10+ members. Impact of captain death on squad efficiency will be finalized later.

| Entity | Composition |
|---|---|
| Player | Direct control, perks, character progression |
| Units | Heroes (squad captains), Grunts (minions) |
| Weapons | Cannons, artifacts, rampart structures |

*Morale rallies are treated as perk or skill effects rather than standalone systems.*

Heroes die permanently upon falling in battle; there is no revival mechanic. Choosing whom to protect creates the central tension of combat.

### Preparation Phase Facilities

| Facility | Function | Upgrade Benefit |
|---|---|---|
| Tavern | Hire heroes (random pool, persistent unlocks) | Higher tier heroes appear |
| Shop | Buy items with currency (random inventory) | Inventory variety expands |
| Barracks | Recruit & train grunts | Grunt stats increase |
| Blacksmith | Craft & upgrade hero equipment | Higher tier equipment craftable |
| Siege | Wall & weapon preparation (add, mount, repair, reinforce) | Construction duration added later |

Facilities are upgradable with extensible data-driven modifiers.

### Currencies & Items

Uses both gold and monster materials without restricting facilities to a single exclusive currency.

| Currency | Description |
|---|---|
| Gold | Primary currency |
| Monster Materials | Secondary currency dropped by defeated monsters; links to future foraging |
| Items | Equipment and consumables; unlocks persist across runs |

*Implementation Note: All cost data contains dual slots for gold and materials (unused slots set to 0), maintaining a unified data schema across all transactions.*

### Characters (Player & Heroes)

Composed of Stats, Skills, and Items. Player and heroes share an identical character data architecture.

### Enemies

| Classification | Details |
|---|---|
| Traits | Aerial, corpse explosion, etc. |
| Enemy Heroes | Elite units with enhanced capabilities |
| Bosses | Custom dedicated AI bosses |

### Morale System

Applies to both allies and enemies, managed per squad.

| Morale Shift | Effect |
|---|---|
| Increase | Slight stat boost |
| Decrease | Stat reduction; triggers rout |

Enemies can be routed rather than slain to achieve victory.
- **Decrease**: Sustaining heavy casualties drops squad morale.
- **Rout**: Falling below threshold causes units to flee the battlefield.
- **Increase**: Triggered by player skills or captain hero traits.
- **Rally**: Routed ally units return for subsequent battles.
- **Enemies**: Maneuver in squads and obey identical morale rules.

### Meta Progression

Unlocks persist across runs; active perk allocations reset upon death to encourage diverse build paths in new runs. Unlocking perks via unlock points expands the available perk draft pool.

Hero and item unlocks persist permanently regardless of run outcome. Unlocks utilize a Darkest Dungeon 2 style progression: runs award unlock points spent to unlock perks, items, and skills.

| Element | Upon Death / Run End |
|---|---|
| Hero Unlocks | Retained |
| Item Unlocks | Retained |
| Unlock Points | Retained |
| Allocated Perks | Reset |

### Perk Categories

| Category | Effect | Profile |
|---|---|---|
| Combat | Enhances player character directly | Immediate power spike |
| Economy | Boosts gold, items, preparation mechanics | Late-game scaling |
| Unit Enhancement | Buffs minions or heroes | Squad-centric |

*Design Notes:*
- Expanding unlock pools dilutes specific hero roll chances; mitigate with tavern re-rolls or locked slots.
- Power creep from permanent unlocks can trivialize fixed stages; balance via point caps or trait-oriented perks over raw stat boosts.

### Future Systems & Open Decisions

Foraging expeditions outside walls deferred due to timeline.
- **Future Systems**: Hero loyalty, difficulty modes.
- **Pending Decisions**: Specific perk varieties, morale parameters, captain death penalties, run duration, boss pacing, clear conditions.

### Recommended Implementation Sequence

1. **Project Setup**: New project, repository, folder hierarchy, CLI test/build pipeline.
2. **Data Architecture**: Extensible stat modifier system, dual-currency cost data, facility upgrade schemas, squad caps.
3. **Combat Prototype**: Castle walls, enemy wave spawning, squad commanding (Move/Attack/Hold), squad morale/routing, win/loss conditions with placeholder graphics.
4. **Run Loop**: Prep → Battle → Reward loop, wall damage carry-over, wave scouting.
5. **Preparation Facilities**: Minimal UI for Tavern, Shop, Barracks, Blacksmith, Siege.
6. **Meta Progression**: Unlock points, persistent save data, per-run perk resets.
