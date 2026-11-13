# Resolved Decisions Archive

Last Updated: 2026-11-13 13:22:55 (KST)

> Purpose: Reference archive of already-resolved `Q-*` decisions and detailed implementation records. | Parent: [INDEX](../INDEX.md) | Related: [Decisions-Pending](Decisions-Pending.md), [design/README](../design/README.md)

## Resolved `Q-*` Table

| ID | Topic | Scope | Resolution |
|---|---|---|---|
| Q-02 | Fall damage on wall collapse | B-4.5 | Resolved: Height-proportional fall damage; tuned in assets. |
| Q-03 | Stair placement for wall ascent | B-4.1, B-4.4 | Resolved: Placed manually by player inside castle. |
| Q-04 | Morale decay, rout thresholds, retreat | B-8.1, B-8.2 | Resolved (2026-11-12): 0–200 scale, start 150, once-per-battle check at 100 (Collapse / 25% Awaken), rout at 0 to courtyard point, +20 between battles (routed floor 80), enemy rout at 0 counted as exit w/ Q-30. See [MoraleAndWave](../design/MoraleAndWave.md). |
| Q-05 | Squad captain death impact | B-5, B-8 | Resolved (2026-11-12): Largest fixed morale drop; captain routs with squad; captain-less squads still obey orders. |
| Q-09 | Simultaneous victory and defeat | B-9.1 | Resolved (2026-11-12): Defeat takes precedence. |
| Q-16 | Build camera & placement controls | B-4.7 | Resolved: 45-deg diagonal ortho camera, translucent preview, Left place, Right cancel, R rotate. |
| Q-17 | Independent stairs & unit overlap | B-4.2, B-4.7 | Resolved: Stairs placeable independently; overlapping living units elevate harmlessly (revised 2026-11-11). |
| Q-18 | Vertical stacking & cantilevers | Future check | Superseded by Q-27 (2026-11-11): Layer stacking; no cantilevers; weapon mounts decoupled. |
| Q-19 | Construction camera rotation | Build camera | Resolved: Q 90 deg left, E 90 deg right around pivot (0.3s SmoothStep). |
| Q-20 | Mounted weapons on wall collapse | B-4.6 | Resolved (2026-11-11): Destroyed simultaneously with supporting piece. |
| Q-22 | Squad directives & targeting | B-5.1, B-5.2 | Resolved (2026-11-11, rev 2026-11-12): Move ignores enemies; Hold defends post (`HoldEngageRadius` 6m, `HoldRangedChaseExtra` 6m); Attack pursues visible enemies en route. Default Hold. |
| Q-23 | Unreachable destinations & stalls | B-5.4 | Resolved (2026-11-11, 2026-11-12 `BUG-wall-hold`): Halt at partial path end; stalled within 1.5m without 0.1m progress over 0.5s counts as arrived. |
| Q-24 | Camera perspective | B-5.4, Game | Resolved (2026-11-11): 1st-person default across combat/command/Game; ortho reserved for build. |
| Q-25 | Scene architecture | G-0 | Resolved (2026-11-11): `Title` and `Game` scenes only; single castle throughout run; facilities as pop-ups. |
| Q-26 | Collaboration workflow | G-0 | Resolved (2026-11-11): Separate PCs and git branches. |
| Q-27 | Castle modularity | Map, D-2 | Resolved (2026-11-11): Option A (3m grid + vertical stacking up to 3 tiers). |
| Q-28 | Siege prototype scope | B-6, B-9 | Resolved (2026-11-11): Dedicated prototype scene; prep build/placement, breach/ladders, 50% upward archer damage. |
| Q-29 | Ladder timings & enemy roles | Siege | Resolved (2026-11-11): Fixed slots; Raise 3s, Topple 2s, Climb 2.5s; Ladder Grunts, Gate Breakers, Archers. |
| Q-30 | Isolated surviving enemies | Siege, B-8 | Resolved (2026-11-12): With no siege-capable enemies and no walkable path to defenders, enemies retreat off-map and flee. |
| Q-33 | Facility pop-up UI framework | D-0 | Resolved (2026-11-12): uGUI + TMP modal pop-ups (title, level, gold, materials, body, close/Esc). |
| Q-34 | Shop/tavern refresh & re-rolls | D-5, D-3 | Resolved (2026-11-12): Seed-rolled per prep phase; gold re-rolling deferred. |
| Q-36 | Hired hero & recruit deployment | D-0b, D-3, D-4 | Resolved (2026-11-13): Pick squad in UI (auto-create up to cap; hiring swaps captain to reserve); units spawn in front of facility and walk to squad. |
| Q-37 | Barracks training & recruit costs | D-4 | Resolved (2026-11-13): Automatic by level (Lv2 +10%, Lv3 +20% HP/attack); melee 15g / ranged 20g from Lv1. |
| Q-38 | Awaken effect types | B-8.1 | Resolved (2026-11-13): Awaken picks 1 of 3 at random: Rally (self + nearby morale), Last Stand (attack up with share of members lost, start max +50%), Steadfast (no rout at 0 this battle). Amends Q-04. See [MoraleAndWave](../design/MoraleAndWave.md). |

## Detailed Decision Records

- **Stacking on the gate (2026-11-11)**: Walls stack on main gate during prep; drop by settle rule when gate breaks; gate floor remains fixed/non-demolishable/unbuildable once destroyed.
- **Player start & construction station (2026-11-11, wired 2026-11-12)**: Player starts in south courtyard. Prep defaults to 1st-person walking; build view opens via F at construction building (`Data/GameFlow/Interaction.asset`), Esc returns to matching pose. Overlapping units lift harmlessly (`LiftForConstruction`).
- **Real battle wiring & map expansion (2026-11-12)**: Gate banner F starts battle; ladders attach to top stacked wall; dropped pieces at gate remain attackable. Map expanded to 80×100 grid (240×300m, castle 96×96m, courtyard 90×90m, 120m northern field).
- **Q-27 Option A implementation details (2026-11-11)**: `BuildLayout` keys `(x, level, z)`; Game grid allows 3 tiers (prototype grids default 1 tier). No stacking on stairs or weapon pieces; multi-cell pieces require uniform support and break if partially unsupported; instant drop with no animation. Options B/C rejected for cost/Synty mismatch.
- **C-1a decisions (2026-11-12)**: Pure C# run data & EditMode tests first; survivors heal fully between battles (no unit HP stored in run data); unique in-run grunt IDs; `BattleResult` pending `C-1a-confirm`.
- **Temporary prototype values**: B-2/B-3 stats, camera settings, and "unreachable = no full NavMesh path" are prototype baselines expanding in B-6.
