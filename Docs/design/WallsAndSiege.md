# Game Design: Castle Walls, Enemies & Siege Strategy

Last Updated: 2026-11-14 17:27:30 (KST)

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
  - Detailed rules: No stacking on stairs; walls may stack above main gate; gate is fixed and undemolishable; when the gate breaks, only the lower door breaks while the indestructible stone arch, gate-top walkable surface, and any upper stacked pieces remain intact and walkable (no falling/collapse, new placement atop the intact gate/upper stack follows normal rules, and `Repair All` restores the door during preparation); multi-cell pieces require uniform support height and are destroyed if partially unsupported; no stacking on weapon-holding pieces and no weapons on covered pieces; no aerial cantilevering.
  - *Planned wall-top cover*: **Battlement / Parapet** — defensive wall-top cover structure planned to shield defenders' full bodies and provide firing cover atop walls.
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
  - **SIEGE-02 scope decisions (2026-11-13)**: R rotates a selected cannon in 4 directions (90° steps) at placement; facing is saved in the castle record. Cannon HP carries over between battles like walls (fixed by `Repair All`). SIEGE-02 also wires wall-top ladder climbers to attack a cannon when no ally is in reach (coordinate shared enemy AI edits with the enemy-assault owner); flyer targeting stays with B-6.3. Blast damage applies target defense (`DamageRules.Calculate`, minimum 1) after distance falloff.
- **Construction Economy** (2026-11-12, D-2): Spend run gold on walls/stairs/cannons. Demolishing refunds 100% for current-prep builds and partial percentage for past-prep/starting walls (gate non-demolishable). Single `Repair All` button spends gold proportional to missing HP (prioritizes heaviest damaged if funds low). Instant construction currently; delayed multi-stage construction planned later.

## Enemies & Siege Strategy

- **Shared ladder traversal (approved 2026-11-13, Q-41; implementation SIEGE-04)**:
  - Allies and player may ascend and descend raised ladders. Current enemy-only ascent is an implementation limitation, not the shared traversal contract.
  - **Player controls**: Tap F at either endpoint to board; W/S moves up/down while aboard. Later confirmation on 2026-11-13: hold F at the upper endpoint to topple, rather than assigning a separate key. Boarding is triggered on release of a short press; a long press never boards on release. The short-press threshold is configured in `SiegeSettings` (temporary default 0.25s); raise/topple durations remain 3s/2s.
  - **Traffic**: One occupant total across both directions. Requests are served in arrival order; the player has no priority over units.
  - **Allied commands**: Automatically use a ladder when the route to the ordered destination requires it. A new order received during traversal takes effect after reaching an endpoint.
  - **Combat**: Occupants cannot attack, may receive damage, and fall if killed. Existing fall-damage rules apply when a ladder topples beneath a living occupant.
  - **Toppling safety**: Automatic allied toppling is prohibited while an ally/player is aboard or waiting to use the ladder. Enemy occupancy alone does not prohibit toppling. Existing nearby-enemy combat blocking remains; the player topples with the hold-F gesture. Deliberate friendly toppling was not approved; SIEGE-04 applies the friendly-use protection to both automatic and player pushes.
  - **Push feedback (user approved 2026-11-13, BUG-ladder-push-visual)**: Raising and pushing use continuous motion. Cancellation before the vertical balance point returns smoothly to the starting endpoint; once past balance, motion completes without further input, even if a nearby hostile arrives. Balance is derived from ladder geometry, not a fixed half-progress threshold. New boarding is blocked during push/settling; occupants pause climbing and follow ladder tilt while pushed or returning, resume once fully returned, and fall as soon as an outward push crosses vertical. Existing 3s raise / 2s push durations also determine settling speed. Support destruction and battle reset still force immediate logical collapse.
  - **Raising**: Allies automatically raise a lowered ladder when needed; the player holds F at the lower endpoint to raise it.
  - User confirmed shared-code edits are available for SIEGE-04 on 2026-11-13. The current implementation handles a ladder between two walkable endpoint regions; a complete walking route is preferred when already available. [Decision record](../history/Q/Q-41.md), [implementation verification](../history/SIEGE/SIEGE-04.md).

| Category | Description |
|---|---|
| Ground | Destroys gates/walls or climbs ladders |
| Flying | Directly attacks allies atop walls (planned) |
| Traits / Hero / Boss | Aerial, corpse explosion, elite enemy heroes, dedicated boss AI (planned) |

- **Siege Assault Rules** (2026-11-11, 2026-11-12):
  - Ground melee cannot strike wall-top defenders from ground level. Enemies enter via gaps, un-repaired segments, or broken gate.
  - Gate destruction (2026-11-14, `SIEGE-06`): Only the lower door breaks and opens the ground passage for allies and enemies; the surrounding stone arch is indestructible and cannot be targeted after the door breaks, while the gate-top surface and any upper stacked walls remain intact and walkable. Once the passage opens, `Gate Breaker` and `Wall Breaker` switch to `Engage` instead of continuing to strike the arch or upper pieces at the gate cell.
  - Wall-destroying enemies follow Q-43; B-6.2 implementation on `feature/b-6.2` adds wall/gate bottom-piece attacks and breach entry. Prototype prefab is available with default roster count 0; formal/mixed encounter integration remains B-6.4/B-7.1. [Verification](../history/B/B-6.2.md).
  - **Archetypes**: `Ladder Grunt` (enters breaches or raises/climbs ladders), `Gate Breaker` (enters breaches or attacks gate), `Archer` (shoots upward at 50% damage, enters castle once path opens; ally archers shoot downward at 100%).

- **Additional enemy behaviors (approved 2026-11-13, Q-43 / B-6.1; implementation B-6.2~4)**:
  - **Extensibility (user requirement)**: Wall-target selection and flying movement/attack are swappable, data-selected modules (strategy/polymorphism), not hard-coded branches. Many flying enemy variants are planned; a new variant must be addable by combining existing modules plus data. Existing ladder/gate/archer behavior is kept, not re-implemented.
  - **Wall Breaker (ground)**: Target = nearest outer wall piece by the current selection module ("nearest" is the first module; others may be added later). The gate counts as a valid wall piece. In a stacked column it strikes the bottom piece; existing settle rules drop upper pieces one tier. Once a walkable path to any ally opens, it switches to normal assault (`Engage`) like other ground enemies.
  - **Flyer**: Flies over walls (not blocked by them; no ground NavMesh needed), then hovers at human height beside its target and attacks in melee. Target priority: nearest ally atop walls; if none, nearest ally anywhere inside the castle.
  - **Flyer details (user, 2026-11-13, B-6.3)**: Hover height stays a per-variant data value so future flyers can either hover low enough for melee or stay high and require ranged fire. "Atop walls" = standing at least 1m above world ground (same as cannon targeting). Agent defaults pending review: no-target hold point is the castle-piece centroid moved 6m inward at cruise height (tallest piece + 3m); dead flyers drop without fall damage. [Implementation](../history/B/B-6.3.md).
  - **Damage vs flyers**: The 50% upward-shot penalty does not apply when allies/player shoot flyers; they take 100%. The penalty remains only for enemies shooting up at wall tops.
  - **Ally melee vs flyers**: Allies attack a hovering flyer when it is within attack range, measured from its ground projection; they do not reach it while it travels at altitude.
  - **Failure handling (confirmed 2026-11-13)**: All roles re-decide every `AttackerDecisionInterval`; a dead or destroyed target triggers immediate re-selection. Wall Breaker with no reachable wall base holds and retries; Flyer with no valid target holds at altitude above the courtyard. Decisions pause during NavMesh rebuilds (existing rule).
  - **Ladders**: Pre-placed along wall perimeter (~every 5 cells; inactive if no wall piece at slot; attaches to topmost wall at cell). Raise ~3s, Climb ~2.5s (1 unit at a time, others queue at base), Topple ~2s (allies atop walls topple autonomously when clear of climbers, or player via interact key). Toppling drops climber with fall damage; toppled ladders can be re-erected in ~3s.

## Siege Defense Prototype Scope

`SiegePrototype.unity` (cloned from `WallCommandPrototype`) validates prep budget (40 temp budget), squad placement, breaches, ladders, and win/loss. Excluded from initial prototype: morale/routing, cannon firing, multi-wave sequences, rewards. F1–F10 test hotkeys.
