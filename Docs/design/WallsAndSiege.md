# Game Design: Castle Walls, Enemies & Siege Strategy

Last Updated: 2026-11-13 15:56:45 (KST)

> Purpose: Construction stacking, build economy, enemy roles, and siege/ladder rules. | Parent: [design/README](README.md) | Related: [Combat](Combat.md), [MoraleAndWave](MoraleAndWave.md)

## Castle Walls & Construction

- **One-cell stairs (approved 2026-11-13, Q-42; SIEGE-03 Done; user accepted stair feel)**: Stairs occupy 1×1 grid cell (3×3m) and ascend one full tier (3m), replacing the former 1×2 footprint. The placeholder remains a continuous ramp. All four rotations support player and unit ascent/descent, including stairs stacked on walls. Existing scene placements retain their high landing positions. No stacking on stairs; existing construction costs are retained. [Implementation and verification](../history/SIEGE/SIEGE-03.md).
- **Battle start (approved 2026-11-13, Q-40)**: Player moves to the desired position during preparation and starts battle there. Battle start must not require returning to the gate banner or automatically relocate the player. FLOW-01 input approved: hold B continuously for 1 second in preparation exploration; show key hint and hold progress. Release or leave exploration to cancel; construction/facility/equipment panels block starting. Preserve position and view, and start only once. The old gate-banner F trigger is retired.

- **Map scale standard** (approved 2026-11-12): Total ground 240×300m, castle w/ outer walls 96×96m, courtyard 90×90m, northern approach 120m; 3m grid cells, up to 3 vertical tiers. One fixed main gate ("Wall Breached" on destruction).
- **Phase restriction**: Construction/demolition/repair allowed only during Preparation; prohibited during battle (2026-11-11).
- **Vertical Layer Stacking** (confirmed 2026-11-11, Q-27 Option A):
  - 3m pieces stack vertically (up to 3 tiers in Game scene). Pointing at a piece stacks above it; empty cell places on ground.
  - Stairs placed on tier-1 wall ascend to tier-2. Stairs can be placed before connecting to walls (shows warning prompt).
  - Lower piece destruction drops stacked pieces down 1 tier (retaining HP and mounted weapons); overlapping units fall and take height-based fall damage.
  - Detailed rules: No stacking on stairs; walls may stack above main gate; gate is fixed, undemolishable, and destroyed gate cell cannot receive new placement; multi-cell pieces require uniform support height and are destroyed if partially unsupported; no stacking on weapon-holding pieces and no weapons on covered pieces; no aerial cantilevering.
- **Build Controls**: 45-deg azimuth, 30-deg elevation orthographic camera; Q/E orbits 90 deg smoothly matching screen direction (Q left, E right); Left-click place, Right-click cancel, R rotate piece; translucent green/red preview.
- **Unit Overlap** (2026-11-11): Living units overlapping new placement are harmlessly lifted onto the new top surface.
- **Siege Weapons**: Mounted on wall mount points (decoupled from ground footprint). Cannons are one extensible type (`ISiegeWeaponModule`). Destroying supporting wall piece destroys mounted weapons simultaneously (no ground drop).
- **Cannon mechanics (approved 2026-11-13, Q-21; implementation SIEGE-02)**:
  - **Control**: Auto-fires at enemies in range when unmanned; the player can man it and aim/fire manually (more accurate than auto). Auto-fire pauses while manned.
  - **Shot**: Ballistic arc with area damage at impact; minimum range prevents hitting the wall base directly below.
  - **Aim arc**: 90° horizontal (±45°) around the facing set by R at placement.
  - **Durability**: Own HP; enemies that reach the wall top and flyers can destroy it. Repaired by the existing `Repair All`. Still destroyed together with its supporting wall piece.
  - **Passage**: Does not block movement on the wall top; no NavMesh carve.
  - **Friendly fire**: None for now (allies and own walls take no damage), behind a data/rule switch so it can be enabled later.
  - **Auto targeting**: Nearest enemy in range/arc, implemented as a swappable target-selection strategy for later patterns (e.g., densest cluster, gate/ladder priority).
  - **Extensibility**: Behavior lives in `ISiegeWeaponModule` components configured by data; new weapon types reuse the shared body and mount rules.
  - **Temporary values (data asset, tune after playtest; user had no preference)**: Range 6–60m, blast radius 3m, center damage 40 (50% at edge), reload 6s (same when manned), HP 150, cost 30 gold (unchanged).
  - **Detail defaults (confirmed 2026-11-13)**: Man/leave with the interact key (F) near the cannon, also leave with Esc; mouse aims within the arc, left-click fires. Auto-fire impact scatters up to 2m (via `IRandomSource`); manual fire has no scatter. Enemies target a cannon only when no ally is within their reach.
- **Construction Economy** (2026-11-12, D-2): Spend run gold on walls/stairs/cannons. Demolishing refunds 100% for current-prep builds and partial percentage for past-prep/starting walls (gate non-demolishable). Single `Repair All` button spends gold proportional to missing HP (prioritizes heaviest damaged if funds low). Instant construction currently; delayed multi-stage construction planned later.

## Enemies & Siege Strategy

- **Shared ladder traversal (approved scope 2026-11-13, Q-41)**: Allies and player can both ascend and descend ladders. Current enemy-only ascent is an implementation limitation. Player controls, allied path/command integration, opposing traffic/queues, combat and toppling during traversal remain pending decisions; existing enemy-only details below are not a finalized shared traversal contract.

| Category | Description |
|---|---|
| Ground | Destroys gates/walls or climbs ladders |
| Flying | Directly attacks allies atop walls (planned) |
| Traits / Hero / Boss | Aerial, corpse explosion, elite enemy heroes, dedicated boss AI (planned) |

- **Siege Assault Rules** (2026-11-11, 2026-11-12):
  - Ground melee cannot strike wall-top defenders from ground level. Enemies enter via gaps, un-repaired segments, or broken gate.
  - Gate destruction drops upper stacked walls; gate breakers keep attacking dropped pieces at the gate cell.
  - Wall-destroying enemies are specified below (B-6.1, Q-43) but not yet implemented; currently only gates can be breached.
  - **Archetypes**: `Ladder Grunt` (enters breaches or raises/climbs ladders), `Gate Breaker` (enters breaches or attacks gate), `Archer` (shoots upward at 50% damage, enters castle once path opens; ally archers shoot downward at 100%).

- **Additional enemy behaviors (approved 2026-11-13, Q-43 / B-6.1; implementation B-6.2~4)**:
  - **Extensibility (user requirement)**: Wall-target selection and flying movement/attack are swappable, data-selected modules (strategy/polymorphism), not hard-coded branches. Many flying enemy variants are planned; a new variant must be addable by combining existing modules plus data. Existing ladder/gate/archer behavior is kept, not re-implemented.
  - **Wall Breaker (ground)**: Target = nearest outer wall piece by the current selection module ("nearest" is the first module; others may be added later). The gate counts as a valid wall piece. In a stacked column it strikes the bottom piece; existing settle rules drop upper pieces one tier. Once a walkable path to any ally opens, it switches to normal assault (`Engage`) like other ground enemies.
  - **Flyer**: Flies over walls (not blocked by them; no ground NavMesh needed), then hovers at human height beside its target and attacks in melee. Target priority: nearest ally atop walls; if none, nearest ally anywhere inside the castle.
  - **Damage vs flyers**: The 50% upward-shot penalty does not apply when allies/player shoot flyers; they take 100%. The penalty remains only for enemies shooting up at wall tops.
  - **Ally melee vs flyers**: Allies attack a hovering flyer when it is within attack range, measured from its ground projection; they do not reach it while it travels at altitude.
  - **Failure handling (confirmed 2026-11-13)**: All roles re-decide every `AttackerDecisionInterval`; a dead or destroyed target triggers immediate re-selection. Wall Breaker with no reachable wall base holds and retries; Flyer with no valid target holds at altitude above the courtyard. Decisions pause during NavMesh rebuilds (existing rule).
  - **Ladders**: Pre-placed along wall perimeter (~every 5 cells; inactive if no wall piece at slot; attaches to topmost wall at cell). Raise ~3s, Climb ~2.5s (1 unit at a time, others queue at base), Topple ~2s (allies atop walls topple autonomously when clear of climbers, or player via interact key). Toppling drops climber with fall damage; toppled ladders can be re-erected in ~3s.

## Siege Defense Prototype Scope

`SiegePrototype.unity` (cloned from `WallCommandPrototype`) validates prep budget (40 temp budget), squad placement, breaches, ladders, and win/loss. Excluded from initial prototype: morale/routing, cannon firing, multi-wave sequences, rewards. F1–F10 test hotkeys.
