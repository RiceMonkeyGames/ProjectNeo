# Game Design: Castle Walls, Enemies & Siege Strategy

Last Updated: 2026-11-13 13:17:37 (KST)

> Purpose: Construction stacking, build economy, enemy roles, and siege/ladder rules. | Parent: [design/README](README.md) | Related: [Combat](Combat.md), [MoraleAndWave](MoraleAndWave.md)

## Castle Walls & Construction

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
- **Construction Economy** (2026-11-12, D-2): Spend run gold on walls/stairs/cannons. Demolishing refunds 100% for current-prep builds and partial percentage for past-prep/starting walls (gate non-demolishable). Single `Repair All` button spends gold proportional to missing HP (prioritizes heaviest damaged if funds low). Instant construction currently; delayed multi-stage construction planned later.

## Enemies & Siege Strategy

| Category | Description |
|---|---|
| Ground | Destroys gates/walls or climbs ladders |
| Flying | Directly attacks allies atop walls (planned) |
| Traits / Hero / Boss | Aerial, corpse explosion, elite enemy heroes, dedicated boss AI (planned) |

- **Siege Assault Rules** (2026-11-11, 2026-11-12):
  - Ground melee cannot strike wall-top defenders from ground level. Enemies enter via gaps, un-repaired segments, or broken gate.
  - Gate destruction drops upper stacked walls; gate breakers keep attacking dropped pieces at the gate cell.
  - Wall-destroying enemies will be added later; currently only gates can be breached.
  - **Archetypes**: `Ladder Grunt` (enters breaches or raises/climbs ladders), `Gate Breaker` (enters breaches or attacks gate), `Archer` (shoots upward at 50% damage, enters castle once path opens; ally archers shoot downward at 100%).
  - **Ladders**: Pre-placed along wall perimeter (~every 5 cells; inactive if no wall piece at slot; attaches to topmost wall at cell). Raise ~3s, Climb ~2.5s (1 unit at a time, others queue at base), Topple ~2s (allies atop walls topple autonomously when clear of climbers, or player via interact key). Toppling drops climber with fall damage; toppled ladders can be re-erected in ~3s.

## Siege Defense Prototype Scope

`SiegePrototype.unity` (cloned from `WallCommandPrototype`) validates prep budget (40 temp budget), squad placement, breaches, ladders, and win/loss. Excluded from initial prototype: morale/routing, cannon firing, multi-wave sequences, rewards. F1–F10 test hotkeys.
