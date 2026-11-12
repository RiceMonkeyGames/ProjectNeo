# Game Design: Siege Defense Roguelike

Last Updated: 2026-11-12 23:42:21 (KST)

This file is the ground truth for game design. The design text in Google Docs is legacy human-readable reference only and may lag behind this file. When game design changes, update this file first. Implementation roadmap, dependencies, and implementation status are managed in [ImplementationPlan.md](ImplementationPlan.md); task selection, assignment, and progress tracking are managed in [Docs/Backlog.md](Backlog.md). Backlog operating procedures follow AGENTS.md (user decision on 2026-11-12).

Completion targets, milestone criteria, and 3-person division of labor are managed in the implementation plan. Release platform, timing, and final content scale remain undecided; suggestions or technology candidates in long-term plans are not treated as finalized design.

## Overview

A siege defense roguelike where players prepare heroes, minions, weapons, and castle walls during the Preparation phase, and defend against enemy waves in the Battle phase. Stages are fixed, while heroes, rewards, and shop inventories are randomized per run.

The core gameplay loop centers on "counter-building" by scouting upcoming waves: enemies pose challenges via their traits, and players craft solutions during preparation using gold and monster materials.

## Core Loop

- Within a run: Preparation → Wave Preview → Battle → Random Rewards → Next Stage Preparation. Castle wall HP carries over to the next battle.
- Defeat in battle ends the run. Active perks reset, reincarnation bonuses are awarded, and a new run begins.
- A run has a maximum stage limit; clearing all stages triggers the ending (direction confirmed on 2026-11-11, implementation later). Number of stages, boss timing, and ending content remain undecided.
- Scene layout (confirmed on 2026-11-11): Title scene and Game scene. The Game scene uses a single castle backdrop where preparation, battle, reward, and results transition seamlessly without scene reloading. The castle encompasses inner courtyard, outer walls, and exterior enemy approach zones. Facilities like shops open as pop-up windows.

## Run Structure

| Element | Fixed / Random | Notes |
|---|---|---|
| Stage (Wave composition) | Fixed | Mastery through memorization and strategic counterplay |
| Castle & Map | Fixed | Entire run takes place in a single castle |
| Tavern Heroes | Random | Primary driver of build variety across runs |
| Battle Rewards | Random | See "Rewards" below |
| Shop Inventory | Random | Fixed shop would lead to static optimal builds |

## Combat

- Default perspective is 3D first-person (confirmed on 2026-11-11). Direct player control combined with squad commanding. Replaces previous third-person plan.
- In the wall-command integration test scene, Tab toggles between orthographic construction view and first-person command view. This toggle is for testing; final preparation/battle phase transition rules are not yet finalized.
- Initial combat prototype uses WASD movement, mouse look, and left-click frontal melee attack. Jump, guard, and directional attacks are follow-ups.
- Player forms squads and assigns heroes as squad captains. No separate commands are given directly to heroes.
- Commands are issued at the squad level: Move, Attack, Hold. Squads without captains receive the same commands.
- Command controls (subject to iteration based on feel): F1–F4 to select squads, backtick (`) to select all squads. Number 1 for Move, 2 for Attack, 3 for Hold. Move targets the ground or wall pointed at by the screen center. Attack commands squads to engage nearby enemies without targeting specific individuals.
- Command Behaviors:
  - **Move**: Disregards enemies and moves to destination. Does not fight during transit. Upon arrival, defaults to Hold behavior.
  - **Hold**: Defends current position (revised on 2026-11-12, inspired by Bannerlord 2 siege defenders). Guard position is where Hold was issued or where Move completed.
    - Melee units: If an enemy comes within a certain distance (currently 6m, data-configurable) and can be reached on foot, units advance to engage. If enemies leave range or path is broken, units do not pursue. Pursuit follows walkable paths, so units atop walls can descend stairs to engage enemies that breached inside.
    - Ranged units: Shoot enemies within range. If outside range but within (range + margin, currently 6m, data-configurable) and walkable on foot, units advance until within firing range (user request on 2026-11-12). Units do not advance toward unreachable enemies (e.g. outside walls). Archers on walls can shoot downward at enemies right beneath the wall.
    - Units return to their post when no valid targets remain. Minor displacement by other units (within 1m) does not trigger return, and units halt nearby if their spot is blocked.
    - Unreachable enemies (e.g., foot of wall without walkable path) are not treated as combat targets. Thus, units push ladders when only unreachable enemies exist.
    - Previous rule (no pursuit, attacking only targets within range) was replaced because units on walls stood idle despite breaches.
  - **Attack**: Advances toward screen-center target point like Move, but engages any enemies spotted in visual range during transit. Resumes march toward target once engaged enemies are eliminated. If no enemies are present, waits at position maintaining Attack posture, engaging new targets as they enter sight.
  - **Vision**: Units detect enemies within a configurable radius. Line of sight occlusion by walls and field-of-view angles are currently omitted.
  - If destination is unreachable or path breaks mid-way, units advance to closest reachable position and halt. If multiple units cluster and get blocked near destination, they are considered arrived and halt.
  - Squads without orders at battle start begin in Hold state.
- Squad count and member caps are data-driven. Default squad size is 10+ members.
- Captain hero death lowers the squad's morale by a large fixed amount (2026-11-12, see Morale).

### Basic Combat Rules

- Damage formula: `Attack − Defense`, minimum damage is 1.
- Auto-targeting acquires closest enemy, re-evaluating when target dies or becomes unreachable.
- No friendly fire.
- Ranged attacks fire linear projectiles upon release. Dodgeable post-launch; blocked by walls, passes through allies.

### Units

| Category | Description |
|---|---|
| Player Character | Controllable hero. Possesses stats, skills, and items identical to heroes. |
| Hero | Hired at tavern. Can act as squad captains. Permadeath upon falling in battle; cannot be revived. |
| Minion (Grunt) | Recruited at barracks. Eliminated upon death; only fleeing minions return for the next battle. |

Tension of deciding whom to protect and sacrifice is central to combat.

- Surviving heroes and minions fully recover HP before the next battle (user decision on 2026-11-12). Only wall HP damage carries over.
- Whether fallen heroes' equipment is lost or recovered remains undecided.
- Fallen ally corpses remain on the ground during the next preparation phase (user decision on 2026-11-12). Excluded from squads and the run.

### Win/Loss Conditions

- Confirmed on 2026-11-12: If victory and defeat occur simultaneously, defeat takes priority. If no siege-capable enemies remain and no walkable path exists to surviving allies, remaining enemies retreat outside the castle. Retreating enemies exited from combat are excluded from victory tallies.

| Result | Conditions |
|---|---|
| Victory | All enemies killed or routed |
| Defeat | Main gate destroyed AND all allies killed (or routed), OR Player character dies |

Combat continues even if main gate is destroyed, provided surviving allies remain.

### Castle Walls

- One main gate at a fixed location. Destroying the main gate counts as "Wall Breached".
- Map scale test standard (user approved on 2026-11-12): Total ground 240×300m; castle including outer walls 96×96m; courtyard 90×90m; northern approach area 120m. 3m grid cells and maximum 3 vertical tiers maintained. Courtyard features placeholder facility buildings, central transit corridors, and troop assembly areas for walk-testing scale.
- Other walls and structures placed freely on the grid. Construction allowed only during preparation; prohibited during combat (confirmed on 2026-11-11).
- Construction stacks pieces into vertical layers on the floor grid (confirmed on 2026-11-11, replacing previous non-stacking rule). Stacks 3m pieces upward rather than fine 1m voxels.
  - Placed on existing piece if mouse points at one; placed on ground if empty. No layer selection hotkey.
  - Placing stairs on tier-1 wall creates a 2-tier staircase to tier-2. Connecting ground stairs enables walking to tier-2.
  - If a lower piece is destroyed, pieces stacked above drop down one level. Units standing on top fall and suffer falling damage.
  - Maximum build height is data-driven. Currently 3 tiers in game scene.
  - Detailed rules: No stacking on stairs. Walls can be stacked above main gate (user revision on 2026-11-11). Player starts in courtyard. If gate is destroyed, walls above drop per standard rules. Gate has fixed position, cannot be demolished, and empty floor cannot receive new placement after destruction. Multi-cell pieces require uniform foundation height across all cells. No stacking on pieces holding siege weapons; no siege weapons placed on walls supporting upper pieces. Dropping pieces retain their HP and mounted weapons. Multi-cell pieces collapsing unevenly due to partial foundation loss are destroyed together.
- No aerial cantilevering (no outward lateral expansion without foundation).
- Pieces to construct show translucent grid-aligned preview. Orthographic construction camera views two grid axes diagonally. Default 45-degree azimuth and 30-degree elevation, tunable via asset. Q/E rotates smoothly by 90 degrees around center to inspect occluded areas. Smooth acceleration/deceleration with tunable transition time. World coordinates of buildings, units, and grid remain unchanged.
- Mouse positioning with Left-click to place, Right-click to cancel, R to rotate piece. Visual feedback indicates placement validity. Q/E camera orbit and R piece rotation are distinct.
- Camera rotation direction matches screen orientation: Q rotates left, E rotates right, aligning with player viewing perspective rather than mathematical angle signs.
- Allies fight atop walls. Walkable paths from courtyard to wall tops are required.
- Stairs are placed manually by the player inside the castle.
- Stairs can be placed prior to connecting to walls. Warning prompt displays lack of connection, allowing subsequent wall attachments.
- Construction permitted even when overlapping units (user revision on 2026-11-11). Living units overlapping placement are moved to top surface of newly built piece without damage. For stairs, moved to corresponding step platform.
- Wall structures (cannons, artifacts, siege weapons) mountable on walls.
- Siege weapon ground footprint handled distinctly from mount point to prevent collision false positives. Specific weapon types and mechanics defined prior to respective tasks.
- Cannons are one type of siege weapon; system must remain extensible for additional weapons.
- If supporting wall piece is destroyed, mounted siege weapons are destroyed simultaneously. They do not drop to ground.
- Reduced wall HP persists into next combat.
- Repair, reinforce, and modify operations supported. Repair initially costs gold only.
- Preparation construction/repair economy (user decision on 2026-11-12, D-2): Gold spent for building pieces and cannons. Demolition available for all pieces/cannons except main gate: 100% refund for pieces built in current preparation phase, partial percentage refund for pieces built earlier (including starting walls). Repair executed via single "Repair All" button costing gold proportional to missing HP; if gold is insufficient, repairs greatest damaged pieces first up to affordable amount. No building/demolishing/repairing during battle. Runs start with starting gold. Costs, refund ratios, and starting gold tuned via data assets; current numbers are prototype placeholders. Rebuilding fully destroyed main gate not yet supported.
- Instant construction currently. Delayed construction timeline architecture planned for future integration (completing after several combat stages; unfinished walls enter combat with partial HP).
- Falling from destroyed walls inflicts fall damage proportional to height. Values tuned via data assets.

### Enemies

| Category | Description |
|---|---|
| Ground | Destroys walls or scales walls with ladders to fight |
| Flying | Directly attacks allies atop walls |
| Traits | Aerial, corpse explosion, etc. |
| Enemy Hero | Elite units with enhanced capabilities |
| Boss | Dedicated custom AI |

Enemies maneuver in squads. Enemy AI designed for extensible categorization.

### Enemy Siege Strategy (User decision on 2026-11-11)

- Melee enemies cannot attack allies on walls from ground level; must scale walls.
- Enemies breach through gaps, un-repaired segments, or collapsed gates.
- User decision on 2026-11-12: In Game scene, battle begins by pressing F at starting point before gate. Ladders attach to top-most stacked wall at corresponding cell. Upon gate destruction, upper walls drop; enemies continue attacking dropped pieces at gate cell to carve a passage.
- If no opening exists, enemies breach gates or climb walls via ladders. Segregated roles: Gate-breakers and Ladder-climbers.
- Wall-destroying enemies will be added as a separate unit type later; until then, only gates can be breached.
- Ladders:
  - Spaced roughly every 5 wall cells. Pre-placed at battle start (Bannerlord style).
  - Erecting ladder takes ~3s (data-configurable).
  - Climbed by one unit at a time.
  - Ladders have no HP; toppled by allies. If climbed by enemies, allies prioritize combat over toppling.
  - Toppling drops climbing enemy, causing fall damage. Climbers remain vulnerable to ranged attacks.
  - Ladder positions fixed on map along wall perimeter. Inactive if no wall piece exists at position.
  - Allies near ladders atop walls topple them autonomously when clear of enemies. Player can also topple with interact key. Toppling takes ~2s.
  - Toppled ladders can be re-erected by enemies in ~3s.
  - Remaining units wait at ladder base until active climber reaches top.
- Ranged combat: Enemy archers can shoot allies atop walls from ground at 50% damage reduction. Ally archers shoot downward with full damage. Ratios data-configurable.
- Enemy archetypes (Siege Prototype): Ladder Grunt (melee), Gate Breaker (melee), Archer.
  - Ladder Grunt: Enters breaches; otherwise raises and climbs ladders.
  - Gate Breaker: Enters breaches; otherwise attacks main gate exclusively. Does not climb ladders.
  - Archer: Fires upward at wall defenders; enters castle once path opens.
  - Upon gate breach, all types charge through closest open routes.
  - Enemies reaching wall top engage defenders and can descend via stairs into courtyard.

### Siege Defense Prototype Scope (User decision on 2026-11-11)

Implemented in new scene copied from `WallCommandPrototype`. Original scene preserved.

- Preparation phase includes building and troop placement. Placement utilizes first-person move commands and top-down click-placement on ground/walls.
- Construction and repairs locked once combat commences. Enemies assault player defenses.
- Starts with breached wall segments and temporary building budget. Budget is prototype-only; persistent run economy developed later. Ample budget provided to test sealing walls vs placing cannons (cannons non-functional currently).
- Win/loss evaluated per Win/Loss Conditions.
- Excluded from initial prototype: Morale/routing, cannon firing, multi-wave sequences, rewards. Enemies arrive in single wave from single direction.
- Squad/troop counts and enemy waves tested via prototype config. Squad limit raised to 10 for testing; selectable via F1–F10. Squad UI indicates Melee/Archer type. Final squad counts and keys undecided.
- Last enemy retreat follows Win/Loss Conditions and Q-30 in ImplementationPlan.

### Morale

Applies to both allies and enemies, managed per squad.

| Change | Cause & Effect |
|---|---|
| Decrease | Sustaining heavy casualties drops squad morale, reducing combat efficiency. |
| Routing | Drops below threshold cause units to flee battlefield. |
| Increase | Boosted by player skills, captain traits, perk effects. Slight combat buff. |
| Rally | Routed ally units return for subsequent battles. |

Enemies can be routed rather than slain. Detailed rules below (2026-11-12). Morale boosts implemented via perks or skills rather than standalone systems.

#### Ally Morale: Breaking Point (User decision on 2026-11-12)

Combines Bannerlord-style squad morale with a Darkest Dungeon-style resolve check. Exact values live in data assets and remain undecided.

All numbers below are starting values kept in data assets and tuned through play.

- **Scale:** Morale ranges 0–200 per squad. A new squad starts at 150.
- **Breaking point check:** When morale falls to 100, a check occurs **once per battle**: the squad **Collapses** (negative effect) or, at a 25% base chance, **Awakens** (positive effect). Captain traits and perks can change the chance. No further check that battle even if morale rises above 100 and falls again.
- **Effect duration:** Collapse and Awaken last until the battle ends. An Awakened squad still routs at 0.
- **Rout:** At 0 the whole squad routs, captain hero included. The player character has no morale. Routing allies run to a fixed retreat point inside the courtyard (e.g., rear of the castle) and leave the battle there; they do not go out the gate. Routing units (ally or enemy) can still be attacked and killed.
- **Starting below 100:** A squad entering battle below 100 gets one Collapse effect at reduced strength from the start and gets no breaking point check that battle (no chance to Awaken).
- **Stat modifier by morale band:** 150 or above: attack +10%. Below 100: attack −10%.
- **Morale loss causes (allies):**
  - Squad member death: proportional to the share of starting members lost.
  - Main gate destroyed: all ally squads, fixed amount.
  - Wall piece collapse: only squads near the collapse, fixed amount.
  - Nearby squad routing: fixed amount.
  - Squad captain death: fixed amount, the largest of these.
  - Not used: enemy hero/boss approach, large explosions (cannon, corpse explosion), continuous ladder climbing, being surrounded.
- **Morale gain during battle:** Only perks/skills and Awaken. No natural recovery over time.
- **"Nearby":** Distance in data, starting at 15m.
- **Awaken effect:** Raises morale of the squad and nearby squads. The earlier "war cry rallying routed allies" idea was dropped as too strong. Last Stand (stronger with fewer members) and Steadfast (never routs) are deferred as possible later Awaken types.
- **Collapse effects (one chosen at random):**
  - Fear: each order is ignored at a chance (start 50%).
  - Panic: periodically leaves its position and falls back toward the castle interior (away from the walls).
  - Contagion: periodically lowers morale of nearby squads by a small amount.
  - Berserk (charging out the gate) was dropped because the gate would have to open.
- **Persistence across battles:** Morale belongs to the squad, not to individual soldiers, and carries over between battles. After each battle, every squad's morale rises by a fixed amount (start +20), capped at 200, like Darkest Dungeon stress relief.
- **Routed squads next battle:** A squad that routed starts the next battle at a floor value (start 80) instead of 0 + recovery. It is still below 100, so it keeps the starting debuff and gets no check, but can recover.
- **Squad reorganization:** Moving soldiers between squads does not change either squad's morale. A newly created squad starts at 150. A squad left with no members is removed along with its morale.
- **Display (minimum, for testing):** The test squad list (`SquadCommandHud`) shows each ally squad's morale number and state (Normal, Collapse: type, Awaken), with a one-line notice when a check happens. Enemy morale is not shown. Final HUD follows Q-11.
- **Later design only:** Prep-phase facilities that restore morale (e.g., tavern). Player character presence affecting the check.

#### Enemy Morale (User decision on 2026-11-12)

- Enemies are handled separately: no breaking point check, no Collapse or Awaken effects. An enemy squad routs when its morale reaches 0, using the same retreat-off-map movement as the last-enemy retreat (Q-30).
- **Morale loss causes (enemies):** Squad member death (proportional), nearby enemy squad routing, enemy hero or squad captain death. Siege failure (ladder toppled, climbers falling) is not used.
- **Starting morale:** Set per enemy type and stage in data. Showing it in the wave preview is decided later.
- **Stat modifier:** Enemies get only the below-100 band (attack −10%), so shaken enemies visibly weaken. No above-150 bonus.
- **Exit counting:** Enemies routed by morale and enemies retreating under Q-30 both count as exited and are excluded from the victory count. The battle result records the two reasons separately.

### Wave Preview

- Displays upcoming enemy types and headcounts by default.
- Certain enemies obscured as hidden ("?"), revealed upon appearing or scouting.
- High reconnaissance traits (e.g., archer heroes) or scouting perks identify hidden enemies prior to arrival.

## Preparation Phase

- Default perspective is 1st-person (confirmed on 2026-11-11). Player traverses courtyard to interact with Tavern, Shop, Barracks, Blacksmith, and Construction facilities. Preparation phase is not top-down by default.
- Construction mode entered by interacting with courtyard Construction building via F key (user request on 2026-11-12). Center-screen cursor proximity triggers interaction prompt. Esc exits construction mode back to 1st-person navigation at matching location/orientation. Interaction range and keybindings data-configurable. Other facilities developed as follow-ups.

| Facility | Function | Upgrade Benefit |
|---|---|---|
| Tavern | Hire randomly appearing heroes | Higher tier heroes appear |
| Shop | Buy and sell items; randomized inventory | Inventory variety changes |
| Barracks | Recruit and train grunts | Grunt stat enhancements |
| Blacksmith | Craft, upgrade, sell hero equipment | Higher tier equipment crafting |
| Siege | Wall/weapon management: add segments, mount weapons, repair, reinforce | Facility-specific data modules |

- Facilities upgradable with data-driven benefits.
- Facility UIs (confirmed on 2026-11-12): Facing building and pressing F opens pop-up; closed via Esc or close button. Player movement/look frozen while open. Placeholder UIs display title, facility level, held gold, and materials. Disabled during combat.
- Shop and Tavern refresh inventories upon entering preparation phase (confirmed on 2026-11-12). Gold re-rolling is a follow-up.
- Facility Upgrades (2026-11-12 prototype): Pay next-level cost in facility UI to upgrade. Upgrades rejected if at max level or lacking funds. Shop inventory tier reflects upgraded level starting from the next preparation phase (does not re-roll within current phase). Tavern, Barracks, and Blacksmith level benefits attach alongside their respective feature implementations.
- Shop (2026-11-12 prototype): Inventory items roll from shop tier item tables; locked items do not appear. Purchased items disappear from stock for current preparation phase. Selling price defaults to 50% of purchase gold price (configurable via data).
- Equipment (2026-11-12 prototype): Press I during preparation to open equipment UI, equipping/unequipping items between stash and player inventory (3 generic slots). Equipping immediately modifies stats; locked during combat. Hero equipment integration follows Tavern implementation.
- Selling price is fixed percentage of purchase price. Items obtained via combat or other means can be sold.

## Currencies & Items

| Category | Description |
|---|---|
| Gold | Primary currency |
| Monster Materials | Secondary currency with multiple sub-types. Dropped by defeated monsters upon stage completion. Equivalent to "Material Items". |
| Items | Equipment and consumable items |

- All cost requirements feature dual slots: Gold and Materials (unused slots set to 0), maintaining unified structure for gold-only, material-only, or hybrid costs.
- Equipment shared interchangeably between player character and heroes.

## Rewards

Awarded randomly upon victory: Gold, Monster Materials, Finished Equipment, Rescued Prisoners, Commander EXP, Unlock Points.

- Prototype rewards (user decision on 2026-11-12): Placeholder data-driven asset values. Victory awards base gold + stage scaling, per-enemy bounty gold/materials, drop-chance items, stage-scaling Commander EXP, and Unlock Points. Single victory rewards granted once.
- Prisoner rescue rewards deferred until recruitment systems are online. Unlock point utility and distribution policy (Q-07) pending decision; accumulated only for now.

## Meta Progression

Two persistent elements survive run failure (death):

| Retained Element | Description |
|---|---|
| Perk Point Count | Spent perk allocations reset upon death; accumulated point pool persists for re-allocation in new runs. |
| Unlocks | Items, heroes, etc. Unlocks persist even if corresponding hero dies in combat. |

### Perk Points & Commander Level

- Perk points earned via Commander Level and related milestones.
- Commander Level raised through:
  - Stage completion Commander EXP. Leveling mid-run grants instant perk points for current run power spikes.
  - Reincarnation Bonus: Percentage of run score converted into Commander EXP upon run termination.
- Perks allocatable at any time during a run, not just at start.
- Encourages diverse build paths and strategic adaptation per run.

Undecided: Allocating perks mid-combat; specific scoring metrics for runs.

### Unlocks

- Spend unlock points for randomized unlocks. E.g., spending 100 points unlocks a hero to appear in tavern pools.
- Duplicate protection: Already unlocked elements do not reappear.
- Guaranteed appearance on initial unlock may be implemented.
- Unlocking perks expands available perk talent pool.

### Perks

| Category | Effect | Profile |
|---|---|---|
| Combat | Enhances player character directly | Immediate power spike |
| Economy | Boosts gold, items, preparation mechanics | Late-game scaling |
| Unit Enhancement | Buffs minions or heroes | Squad-centric |

- Perks encompass stat buffs as well as unique active/passive traits (e.g., squad morale boost).
- Specific perk varieties undecided. Categorization of scouting perks undecided.

### Design Considerations

- Pool dilution: Excessive unlocks dilute hero pools, making specific heroes harder to roll. Mitigate with tavern re-rolls or locked slots.
- Power creep: Accumulating unlocks trivialize fixed stages. Mitigate with point caps or trait-oriented rather than stat-oriented perks.

## Art Style

Low-poly aesthetic using Synty assets. Built with placeholder graphics initially, swapped with Synty assets later. Specific Synty packs undecided.

Development proceeds prior to pack selection: Grid scale, cell footprints, heights, and visual references are decoupled into data assets rather than hardcoded to specific asset dimensions. Allows seamless scaling and aesthetic adjustments upon asset import.

## Future Additions

- Foraging / Expeditions: Venturing outside walls to harvest monster materials at risk. Deferred due to timeline.
- Hero Loyalty
- Difficulty Modes

## Open Design Questions

- Run length (max stage count, boss pacing) and ending specifics. Clearing all stages triggers ending is decided.
- Specific perk varieties and scouting perk categorization.
- Morale tuning values beyond the starting values in "Ally Morale: Breaking Point" (rules decided 2026-11-12). Prep-phase morale recovery facilities.
- Combat/building tuning values (fall damage coefficients, etc.).
- Feasibility of mid-combat perk allocation.
- Metrics determining run score.
- Actual values for squad counts and squad member caps.
- Layered building edge cases (stairs constraints, partial foundation collapse) and max vertical tiers.
- Equipment handling on hero permadeath; whether player character can serve as squad captain.
- Shop/tavern gold re-roll costs and locking slots (inventory refreshing per prep phase decided on 2026-11-12).
- Blacksmith crafting format (direct purchase vs material recipes) and upgrade definitions.
- Spawn mechanisms for recruited heroes/grunts into squads during combat.
